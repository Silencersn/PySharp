using PySharp.Compilation.CodeAnalysis;
using PySharp.Compilation.Primitives;
using PySharp.Modules.Builtins;
using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.Comparison;
using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace PySharp.Compilation.AstNodes;

internal sealed partial class SemanticAnalyzer : ICodeMetaInfoProvider
{
    public static SemanticModel Analyze(PyCallContext context, CodeSource source, AstModNode root, CompileSession session)
    {
        var scope = InternalAnalyze(context, source, root, session);
        var model = new SemanticModel(root);
        scope.Bind(model);
        return model;
    }

    internal static RootVariableScope InternalAnalyze(PyCallContext context, CodeSource source, AstModNode root, CompileSession session)
    {
        var analyzer = new SemanticAnalyzer(context, source, session);
        var scope = analyzer.BuildBasicScope(root);
        FillUnknownVariables(scope);
        analyzer.CheckClosureAndFillCapturedVariables(scope);
        FillCallableProperties(scope);
        return scope;
    }

    private readonly CodeSource _source;
    private readonly PyCallContext _context;
    private readonly CompileSession _session;
    private readonly Stack<AstNode> _nodesToRoot;

    private readonly Stack<ScopeStats> _scopeStatsStack;
    private ScopeStats _currentScopeStats;
    private readonly Stack<NestedComprehensionStats> _nestedComprehensionStatsStack;
    private NestedComprehensionStats _currentNestedComprehensionStats;

    // the closure pass reports unbound nonlocals after the tree walk, when
    // _nodesToRoot is empty, so the declaring statements are kept here to
    // locate the deferred error (CPython's symtable carries the node)
    private readonly Dictionary<(VariableScope Scope, string Name), NonlocalNode> _nonlocalDeclarations = [];

    CodeMetaInfo? ICodeMetaInfoProvider.MetaInfo => _nodesToRoot.TryPeek(out var node) ? CodeMetaInfo.FromSpan(_source, node.MetaInfo.Range, node.MetaInfo.CrucialRange) : null;

    private SemanticAnalyzer(PyCallContext context, CodeSource source, CompileSession session)
    {
        _nodesToRoot = [];
        _scopeStatsStack = [];
        _context = context;
        _source = source;
        _session = session;
        _currentScopeStats = null!;
        _nestedComprehensionStatsStack = [];
        _currentNestedComprehensionStats = null!;
    }

    public PyRuntimeException SyntaxError(string message = PySR.InvalidSyntax, params ReadOnlySpan<object?> args)
    {
        return _context.SyntaxError(this, message, args);
    }

    private PyRuntimeException SyntaxErrorAt(AstNode node, string message, params ReadOnlySpan<object?> args)
    {
        var metaInfo = CodeMetaInfo.FromSpan(_source, node.MetaInfo.Range, node.MetaInfo.CrucialRange);
        return _context.SyntaxError(new FixedMetaInfoProvider(metaInfo), message, args);
    }

    // CPython's symtable errors carry a location but no source text
    // (symtable_error passes a NULL text); the display re-reads the file
    // for the caret line, while e.text stays None
    private static PyRuntimeException WithoutSourceText(PyRuntimeException error)
    {
        error.PyException.SetMember("text", PyNoneObject.None);
        return error;
    }

    // Locates a deferred error at a node that is no longer on _nodesToRoot.
    private sealed class FixedMetaInfoProvider(CodeMetaInfo metaInfo) : ICodeMetaInfoProvider
    {
        public CodeMetaInfo? MetaInfo => metaInfo;
    }

    private sealed class ScopeStats
    {
        public int LoopDepth;
        public int FinallyDepth;
        public readonly VariableScope Scope;

        internal ScopeStats(VariableScope scope)
        {
            Scope = scope;
            LoopDepth = 0;
            FinallyDepth = 0;
        }
    }

    private sealed class NestedComprehensionStats
    {
        public Stack<ComprehensionStats> ComprehensionStatsStack => field ??= [];
        public ComprehensionStats CurrentComprehensionStats;
        public AstExprNode? CurrentComprehension => CurrentComprehensionStats.Node;
        [MemberNotNullWhen(true, nameof(CurrentComprehension))]
        public bool IsWithinComprehension => CurrentComprehensionStats.Node is not null;

        internal void PushComprehension(AstExprNode node)
        {
            ComprehensionStatsStack.Push(CurrentComprehensionStats);
            CurrentComprehensionStats = new ComprehensionStats(node);
        }

        internal void PopComprehension()
        {
            CurrentComprehensionStats = ComprehensionStatsStack.Pop();
        }

        // CPython's yield check tests the enclosing symtable block: while
        // the outermost iterables are being visited no comprehension block
        // has been entered yet — not even for nested comprehensions met
        // along the way — so a yield there belongs to the enclosing function
        public bool IsEntirelyWithinOutermostIterables()
        {
            if (CurrentComprehensionStats.VisitingPart is not ComprehensionStatsVisitingPart.GeneratorIter)
                return false;
            foreach (var stats in ComprehensionStatsStack)
            {
                // the bottom entry is the state outside any comprehension
                if (stats.Node is null)
                    continue;
                if (stats.VisitingPart is not ComprehensionStatsVisitingPart.GeneratorIter)
                    return false;
            }
            return true;
        }
    }

    private struct ComprehensionStats
    {
        public readonly AstExprNode? Node;
        public ComprehensionStatsVisitingPart VisitingPart;

        public ComprehensionStats(AstExprNode node)
        {
            Node = node;
        }
    }

    private enum ComprehensionStatsVisitingPart
    {
        None,
        Element,
        GeneratorTarget,
        GeneratorIter,
        GeneratorIfs
    }

    internal static void FillUnknownVariables(RootVariableScope root)
    {
        FillUnknownVariablesImpl(root);

        static void FillUnknownVariablesImpl(VariableScope scope)
        {
            foreach (var (name, type) in scope.Variables)
            {
                if (type is not PyVariableType.Unknown)
                    continue;

                var parent = scope.Parent;
                while (true)
                {
                    if (parent is null)
                    {
                        scope.Variables[name] = PyVariableType.Global;
                        break;
                    }

                    if (parent is CallableVariableScope &&
                        parent.Variables.TryGetValue(name, out var typeOfParentVariable))
                    {
                        scope.Variables[name] = typeOfParentVariable is PyVariableType.Global
                            ? PyVariableType.Global : PyVariableType.Closure;
                        break;
                    }

                    // Comprehension targets: the inline comprehension scope
                    // owns them, so a nested function's reference is a closure
                    // over that scope (CPython treats the comprehension as a
                    // function owning the cellvar).
                    if (parent is ComprehensionVariableScope comprehensionScope &&
                        comprehensionScope.Variables.TryGetValue(name, out var typeOfCompVariable) &&
                        typeOfCompVariable is not (PyVariableType.Global or PyVariableType.Closure))
                    {
                        scope.Variables[name] = PyVariableType.Closure;
                        break;
                    }

                    if (name is PySpecialNames.Class && parent is ClassVariableScope)
                    {
                        scope.Variables[name] = PyVariableType.Closure;
                        break;
                    }

                    // Type params from enclosing GenericParamVariableScope or
                    // regular variables from ClassVariableScope are visible to nested scopes.
                    if (parent is GenericParamVariableScope && parent.Variables.ContainsKey(name))
                    {
                        scope.Variables[name] = PyVariableType.Closure;
                        break;
                    }

                    parent = parent.Parent;
                }
            }

            foreach (var childScope in scope.Children)
                FillUnknownVariablesImpl(childScope);
        }
    }

    internal static void FillCallableProperties(RootVariableScope scope)
    {
        FillTempFreesClass(scope);
        FillTempFrees(scope);
        FillPropertiesImpl(scope);

        static void FillTempFreesClass(VariableScope scope)
        {
            if (scope is ClassVariableScope classScope)
            {
                foreach (var s in classScope.ScopesRequiringFree)
                    s.TempFrees.Add(PySpecialNames.Class);
            }

            foreach (var child in scope.Children)
                FillTempFreesClass(child);
        }

        static void FillTempFrees(VariableScope scope)
        {
            if (scope is CallableVariableScope callableScope)
            {
                foreach (var name in callableScope.Variables.Keys)
                {
                    if (!callableScope.ScopesRequiringFree.TryGetValue(name, out var scopes))
                        continue;

                    foreach (var s in scopes)
                        s.TempFrees.Add(name);
                }
            }

            // Comprehension targets captured by nested functions: distribute
            // the captured names to the referencing scopes' TempFrees.
            if (scope is ComprehensionVariableScope comprehensionScope)
            {
                foreach (var (name, scopes) in comprehensionScope.ScopesRequiringFree)
                {
                    foreach (var s in scopes)
                        s.TempFrees.Add(name);
                }
            }

            // GenericParamScope is processed AFTER CallableVariableScope, so type-param
            // names are appended to TempFrees AFTER outer captured variable names.
            // This guarantees FreeVars = [outer_vars..., type_params...] ordering, which
            // PyVariables.CreateForBuildingClass relies on (offset-based closure fill).
            if (scope is GenericParamVariableScope genericParamScope)
            {
                foreach (var name in genericParamScope.Variables.Keys)
                {
                    if (!genericParamScope.ScopesRequiringFree.TryGetValue(name, out var scopes))
                        continue;

                    foreach (var s in scopes)
                        s.TempFrees.Add(name);
                }
            }

            foreach (var child in scope.Children)
                FillTempFrees(child);
        }

        static void FillPropertiesImpl(VariableScope scope)
        {
            foreach (var child in scope.Children)
                FillPropertiesImpl(child);

            if (scope is GenericParamVariableScope genParamScope)
            {
                genParamScope.CellVars = [.. genParamScope.Variables
                    .Where(pair => pair.Value is PyVariableType.CapturedLocal)
                    .Select(pair => pair.Key)];

                genParamScope.FreeVars = [.. genParamScope.TempFrees.Distinct()];

                // Compute argCount matching CPython: at most 2 arg slots
                // (.defaults for positional defaults, .kwdefaults for keyword defaults).
                int argCount = 0;
                if (genParamScope.Owner is IFunctionDefNode fnNode2)
                {
                    if (fnNode2.Args.Defaults.Length > 0)
                        argCount++;
                    if (fnNode2.Args.KwDefaults.Length > 0)
                        argCount++;
                }
                genParamScope.ArgCount = argCount;

                if (argCount > 0)
                {
                    // Prepend dummy arg names matching CPython convention.
                    var argNames = new List<string>();
                    var fnNode = (IFunctionDefNode)genParamScope.Owner;
                    if (fnNode.Args.Defaults.Length > 0)
                        argNames.Add(".defaults");
                    if (fnNode.Args.KwDefaults.Length > 0)
                        argNames.Add(".kwdefaults");

                    genParamScope.VarNames = [.. argNames,
                        .. genParamScope.Variables
                            .Where(pair => pair.Value is PyVariableType.Local or PyVariableType.CapturedLocal)
                            .Select(pair => pair.Key)];

                    // Pad LocalsTable with dummy arg slots at indices 0..argCount-1
                    // so InitArgs writes into the local span without colliding
                    // with type-param entries (shifted by argCount).
                    var names = genParamScope.VarNames
                        .Concat(genParamScope.CellVars)
                        .Concat(genParamScope.FreeVars)
                        .Distinct()
                        .ToArray();
                    var items = new KeyValuePair<string, int>[names.Length];
                    for (int i = 0; i < names.Length; i++)
                        items[i] = new(names[i], i);
                    genParamScope.LocalsTable = items.ToFrozenDictionary();
                }
                else
                {
                    genParamScope.VarNames = [.. genParamScope.Variables
                        .Where(pair => pair.Value is PyVariableType.Local or PyVariableType.CapturedLocal)
                        .Select(pair => pair.Key)];

                    genParamScope.LocalsTable = genParamScope.VarNames
                        .Concat(genParamScope.CellVars)
                        .Concat(genParamScope.FreeVars)
                        .Distinct()
                        .Index()
                        .ToFrozenDictionary(static indexed => indexed.Item, static indexed => indexed.Index);
                }
                return;
            }

            if (scope is ClassVariableScope classScope)
            {
                classScope.FreeVars = [.. classScope.TempFrees.Distinct()];
                return;
            }

            if (scope is TypeAliasVariableScope aliasValueScope)
            {
                aliasValueScope.FreeVars = [.. aliasValueScope.TempFrees.Distinct()];
                return;
            }

            if (scope is ComprehensionVariableScope comprehensionScope)
            {
                comprehensionScope.CellVars = [.. comprehensionScope.Variables
                    .Where(pair => pair.Value is PyVariableType.CapturedLocal)
                    .Select(pair => pair.Key)];
                return;
            }

            if (scope is not CallableVariableScope callableScope)
                return;

            var varArg = callableScope.ArgumentsNode.VarArg?.Arg;
            var kwArg = callableScope.ArgumentsNode.KwArg?.Arg;
            callableScope.VarNames = [.. callableScope.Variables
                .Where(pair => pair.Value is PyVariableType.Local or PyVariableType.Parameter or PyVariableType.CapturedParameter)
                .OrderBy(pair =>
                {
                    if (pair.Value is PyVariableType.Local)
                        return 2;

                    if (pair.Key == varArg || pair.Key == kwArg)
                        return 1;

                    return 0;
                })
                .Select(pair => pair.Key)];

            callableScope.CellVars = [.. callableScope.Variables
                .Where(pair => pair.Value is PyVariableType.CapturedLocal or PyVariableType.CapturedParameter)
                .Select(pair => pair.Key)];

            callableScope.FreeVars = [.. callableScope.TempFrees.Distinct()];

            callableScope.LocalsTable = callableScope.VarNames
                .Concat(callableScope.CellVars)
                .Concat(callableScope.FreeVars)
                .Distinct()
                .Index()
                .ToFrozenDictionary(static indexed => indexed.Item, static indexed => indexed.Index);
        }
    }

    internal RootVariableScope BuildBasicScope(AstModNode root)
    {
        var rootScope = new RootVariableScope(root);

        PushScope(rootScope);
        VisitNode(root);
        PopScope();

        Debug.Assert(_scopeStatsStack.Count is 0);
        return rootScope;
    }

    internal void CheckControlStmtNotInFinallyUntil(Func<AstNode, bool> stopPredicate, string warningMessage)
    {
        foreach (var node in _nodesToRoot)
        {
            if (node == _currentScopeStats.Scope.Owner)
                return;

            if (stopPredicate(node))
                return;

            if (node is TryNode)
            {
                _ = _context.WarnSyntax(warningMessage, this, _session).PyUnwrap(_context);
                return;
            }
        }
    }

    // PEP 654: break/continue/return cannot cross an except* handler
    // boundary. A handler belongs to except* iff its parent (the next
    // ancestor) is a TryStarNode.
    internal void CheckControlStmtNotInExceptStarUntil(Func<AstNode, bool> stopPredicate)
    {
        ExceptHandlerNode? previousHandler = null;
        foreach (var node in _nodesToRoot)
        {
            if (node == _currentScopeStats.Scope.Owner)
                return;

            if (previousHandler is not null && node is TryStarNode)
                throw SyntaxError(PySR.InvalidSyntax_Semantic_ControlFlowInExceptStar);

            if (stopPredicate(node))
                return;

            previousHandler = node as ExceptHandlerNode;
        }
    }

    private void PushScope(VariableScope nextScope)
    {
        _scopeStatsStack.Push(_currentScopeStats);
        _currentScopeStats = new ScopeStats(nextScope);
        if (nextScope is not GeneratorExpVariableScope)
        {
            _nestedComprehensionStatsStack.Push(_currentNestedComprehensionStats);
            _currentNestedComprehensionStats = new NestedComprehensionStats();
        }
    }

    private void PopScope()
    {
        Debug.Assert(_currentScopeStats.LoopDepth is 0);

        if (_currentScopeStats.Scope is AsyncFunctionVariableScope { IsAsyncGenerator: true, ReturnWithValue: not null } asyncScope)
            throw SyntaxErrorAt(asyncScope.ReturnWithValue, PySR.InvalidSyntax_Semantic_ReturnWithValueInAsyncGenerator);

        if (_currentScopeStats.Scope is not GeneratorExpVariableScope)
            _currentNestedComprehensionStats = _nestedComprehensionStatsStack.Pop();
        _currentScopeStats = _scopeStatsStack.Pop();
    }

    // Like PushScope, but keeps the comprehension stats so that nested
    // comprehensions and walrus checks keep working across the body scope.
    private void PushComprehensionScope(VariableScope scope)
    {
        _scopeStatsStack.Push(_currentScopeStats);
        _currentScopeStats = new ScopeStats(scope);
    }

    private void PopComprehensionScope()
    {
        Debug.Assert(_currentScopeStats.LoopDepth is 0);

        _currentScopeStats = _scopeStatsStack.Pop();
    }

    private void VisitNullableNode(AstNode? node)
    {
        if (node is not null)
            VisitNode(node);
    }

    private void VisitNodes<T>(ImmutableArray<T> nodes) where T : AstNode
    {
        foreach (var node in nodes)
            VisitNode(node);
    }

    private void VisitNullableNodes<T>(ImmutableArray<T?> nodes) where T : AstNode
    {
        foreach (var node in nodes)
        {
            if (node is not null)
                VisitNode(node);
        }
    }

    private void VisitNode(AstNode node)
    {
        PreVisitNode(node);
        switch (node)
        {
            case AstModNode mod: VisitMod(mod); break;
            case AstStmtNode stmt: VisitStmt(stmt); break;
            case AstExprNode expr: VisitExpr(expr); break;
            default: VisitMisc(node); break;
        }
        PostVisitNode(node);
    }

    private void PreVisitNode(AstNode node)
    {
        _nodesToRoot.Push(node);
    }
    private void PostVisitNode(AstNode node)
    {
        var poppedNode = _nodesToRoot.Pop();
        Debug.Assert(ReferenceEquals(poppedNode, node));
    }

    private void AddParameter(string name)
    {
        _currentScopeStats.Scope.Variables.Add(name, PyVariableType.Parameter);
    }

    // CPython symtable check_name: a Store or Del binding of the reserved name
    // __debug__ is a compile-time SyntaxError. The load side is not checked —
    // it folds to a constant in the emitter instead (ast_preprocess.c folds
    // only Name nodes with ctx == Load, which is why the two coexist).
    private void CheckReservedName(string name, ExprContextType ctx, AstNode node)
    {
        // check_name only rejects writes, so a load (including obj.__debug__) is fine.
        if (name is not PySpecialNames.Debug || ctx is ExprContextType.Load)
            return;

        var message = ctx is ExprContextType.Del ? PySR.InvalidSyntax_DelStmt_CannotDelete : PySR.InvalidSyntax_InvalidTarget;
        throw SyntaxErrorAt(node, message, PySpecialNames.Debug);
    }

    private void BindVariable(string name, ExprContextType ctx, AstNode node)
    {
        // CPython folds a load of __debug__ to a constant in ast_preprocess.c,
        // which runs before the symbol table is built, so the name never enters
        // a scope. The emitter does that fold here.
        if (name is PySpecialNames.Debug && ctx is ExprContextType.Load)
            return;

        CheckReservedName(name, ctx, node);
        _currentScopeStats.Scope.AppendVariable(name, ctx);
    }

    // CPython check_keywords, reached from both the Call and the ClassDef
    // visitor: keyword argument names are name-bound as Store.
    private void CheckKeywords(ImmutableArray<AstKeywordNode> keywords)
    {
        foreach (var keyword in keywords)
        {
            if (keyword.Arg is not null)
                CheckReservedName(keyword.Arg, ExprContextType.Store, keyword);
        }
    }

    // CPython's restricted-expression blocks (symtable.c ste_type): while one
    // is current, a yield, named expression or await raises through
    // symtable_raise_if_annotation_block. Each entry is the block's own
    // phrase in the message — "an annotation", "a type alias", "the
    // definition of a generic" or a type-variable position — plus the scope
    // it belongs to: entering a nested scope (a lambda body, a comprehension)
    // pushes a different block type the way CPython's block stack does, so
    // the restriction only fires while the owning scope is current.
    private readonly Stack<(string Block, VariableScope Scope)> _restrictedExprBlocks = [];

    private void CheckRestrictedExpr(AstExprNode node)
    {
        if (_restrictedExprBlocks.TryPeek(out var top) && ReferenceEquals(top.Scope, _currentScopeStats.Scope))
            throw SyntaxErrorAt(node, PySR.InvalidSyntax_Semantic_ExprNotAllowedInRestricted, AstUtils.GetExprNodeName(node), top.Block);
    }

    // The restricted-expression check for lazily-evaluated positions that
    // never enter the normal semantic walk (type-param bounds/defaults and
    // annotations outside a class body): the expression is visited for its
    // static errors only, without binding any names. Nested scopes behave
    // like CPython's block stack — a lambda's defaults evaluate in the
    // current block while its body gets a function block of its own, and a
    // comprehension (its outermost iterable included) is a block of its own.
    private void CheckRestrictedExprIn(AstExprNode expr, string block)
    {
        switch (expr)
        {
            case YieldNode or YieldFromNode or AwaitNode or NamedExprNode:
                throw SyntaxErrorAt(expr, PySR.InvalidSyntax_Semantic_ExprNotAllowedInRestricted, AstUtils.GetExprNodeName(expr), block);

            case LambdaNode lambda:
                foreach (var node in ((IScopedSubNodesProvider)lambda).EnumerateSubNodesOuterScope().Cast<AstExprNode>())
                    CheckRestrictedExprIn(node, block);
                break;

            case ListCompNode or SetCompNode or DictCompNode or GeneratorExpNode:
                break;

            case AttributeNode n: CheckRestrictedExprIn(n.Value, block); break;
            case BinOpNode n: CheckRestrictedExprIn(n.Left, block); CheckRestrictedExprIn(n.Right, block); break;
            case BoolOpNode n: CheckExprs(n.Values); break;
            case CallNode n: CheckRestrictedExprIn(n.Func, block); CheckExprs(n.Args); CheckKeywords(n.Keywords); break;
            case CompareNode n: CheckRestrictedExprIn(n.Left, block); CheckExprs(n.Comparators); break;
            case DictNode n:
                foreach (var key in n.Keys)
                {
                    if (key is not null)
                        CheckRestrictedExprIn(key, block);
                }
                CheckExprs(n.Values);
                break;
            case FormattedValueNode n: CheckRestrictedExprIn(n.Value, block); CheckNullable(n.FormatSpec); break;
            case IfExpNode n: CheckRestrictedExprIn(n.Test, block); CheckRestrictedExprIn(n.Body, block); CheckRestrictedExprIn(n.OrElse, block); break;
            case InterpolationNode n: CheckRestrictedExprIn(n.Value, block); CheckNullable(n.FormatSpec); break;
            case JoinedStrNode n: CheckExprs(n.Values); break;
            case ListNode n: CheckExprs(n.Elts); break;
            case TupleNode n: CheckExprs(n.Elts); break;
            case SetNode n: CheckExprs(n.Elts); break;
            case SliceNode n: CheckNullable(n.Lower); CheckNullable(n.Upper); CheckNullable(n.Step); break;
            case StarredNode n: CheckRestrictedExprIn(n.Value, block); break;
            case SubscriptNode n: CheckRestrictedExprIn(n.Value, block); CheckRestrictedExprIn(n.Slice, block); break;
            case TemplateStrNode n: CheckExprs(n.Values); break;
            case UnaryOpNode n: CheckRestrictedExprIn(n.Operand, block); break;

            // ConstantNode and NameNode carry no sub-expressions
        }

        void CheckExprs(ImmutableArray<AstExprNode> nodes)
        {
            foreach (var node in nodes)
                CheckRestrictedExprIn(node, block);
        }

        void CheckKeywords(ImmutableArray<AstKeywordNode> nodes)
        {
            foreach (var node in nodes)
                CheckRestrictedExprIn(node.Value, block);
        }

        void CheckNullable(AstExprNode? node)
        {
            if (node is not null)
                CheckRestrictedExprIn(node, block);
        }
    }

    // symtable.c ste_scope_info: a TypeVar's bound reads as a constraint when
    // it is a tuple, and every default names its own parameter kind
    private static string TypeVariableBlockOf(AstTypeParamNode tp, AstExprNode value) => tp switch
    {
        TypeVarNode tv when ReferenceEquals(value, tv.Bound) =>
            value is TupleNode ? "a TypeVar constraint" : "a TypeVar bound",
        TypeVarNode => "a TypeVar default",
        TypeVarTupleNode => "a TypeVarTuple default",
        ParamSpecNode => "a ParamSpec default",
        _ => throw new UnreachableException(),
    };

    // symtable_visit_type_param: the bound and default of each parameter
    // kind are visited inside their own TypeVariableBlock
    private void CheckRestrictedTypeParam(AstTypeParamNode tp)
    {
        switch (tp)
        {
            case TypeVarNode n:
                if (n.Bound is not null)
                    CheckRestrictedExprIn(n.Bound, TypeVariableBlockOf(tp, n.Bound));
                if (n.DefaultValue is not null)
                    CheckRestrictedExprIn(n.DefaultValue, TypeVariableBlockOf(tp, n.DefaultValue));
                break;

            case TypeVarTupleNode n:
                if (n.DefaultValue is not null)
                    CheckRestrictedExprIn(n.DefaultValue, TypeVariableBlockOf(tp, n.DefaultValue));
                break;

            case ParamSpecNode n:
                if (n.DefaultValue is not null)
                    CheckRestrictedExprIn(n.DefaultValue, TypeVariableBlockOf(tp, n.DefaultValue));
                break;
        }
    }

    // Binds a parameter. CPython runs check_name from symtable_add_def as each
    // parameter is added, before the duplicate-argument pass, so a reserved
    // name outranks a duplicate.
    private void BindParameter(string name, AstNode node)
    {
        CheckReservedName(name, ExprContextType.Store, node);

        if (_currentScopeStats.Scope.Variables.ContainsKey(name))
            throw SyntaxError(PySR.InvalidSyntax_Semantic_DuplicateArgument, name);

        AddParameter(name);
    }

    private void VisitMisc(AstNode node)
    {
        switch (node)
        {
            case AstArgNode n:
                BindParameter(n.Arg, n);
                break;

            case AstAliasNode n:
                BindVariable(n.GetLocalName(), ExprContextType.Store, n);
                break;

            case AstPatternNode pattern:
                VisitPattern(pattern);
                break;

            case ExceptHandlerNode n:
                if (n.Name is not null)
                    BindVariable(n.Name, ExprContextType.Store, n);
                VisitNullableNode(n.Type);
                VisitNodes(n.Body);
                break;

            case AstComprehensionNode n:
                if (n.IsAsync)
                {
                    var outerComp = _currentNestedComprehensionStats.CurrentComprehension;
                    if (outerComp is not GeneratorExpNode && _currentScopeStats.Scope is not AsyncFunctionVariableScope)
                        throw SyntaxError(PySR.InvalidSyntax_Semantic_AsyncCompOutsideAsyncFunc);
                }
                ref var part = ref _currentNestedComprehensionStats.CurrentComprehensionStats.VisitingPart;
                part = ComprehensionStatsVisitingPart.GeneratorTarget;
                VisitNode(n.Target);
                part = ComprehensionStatsVisitingPart.GeneratorIter;
                VisitNode(n.Iter);
                part = ComprehensionStatsVisitingPart.GeneratorIfs;
                VisitNodes(n.Ifs);
                part = ComprehensionStatsVisitingPart.None;
                break;

            case AstKeywordNode n:
                VisitNode(n.Value);
                break;

            case AstWithItemNode n:
                VisitNode(n.ContextExpr);
                VisitNullableNode(n.OptionalVars);
                break;

            case AstMatchCaseNode n:
                VisitNode(n.Pattern);
                VisitNullableNode(n.Guard);
                VisitNodes(n.Body);
                break;

            case AstArgumentsNode:
            case TypeVarNode:
            case ParamSpecNode:
            case TypeVarTupleNode:
                throw new UnreachableException($"'{node.GetType().Name}' is destructured and handled separately by its parent node or is useless, and should not be visited directly.");

            default:
                throw new UnreachableException();
        }
    }

    private void VisitPattern(AstPatternNode pattern)
    {
        switch (pattern)
        {
            case MatchStarNode n:
                if (n.Name is not null)
                    BindVariable(n.Name, ExprContextType.Store, n);
                break;

            case MatchMappingNode n:
                var literalKeys = n.Keys.OfType<ConstantNode>().Select(static node => node.Value).ToArray();
                for (int i = 1; i < literalKeys.Length; i++)
                {
                    for (int j = 0; j < i; j++)
                    {
                        if (PyObjectComparer.Default.Equals(literalKeys[j], literalKeys[i]))
                            throw SyntaxError(PySR.InvalidSyntax_Semantic_MappingDuplicateKey, PySpecialMethods.Str(_context, literalKeys[j]).PyUnwrap(_context).Value);
                    }
                }

                if (n.Rest is not null)
                    BindVariable(n.Rest, ExprContextType.Store, n);
                VisitNodes(n.Keys);
                VisitNodes(n.Patterns);
                break;

            case MatchAsNode n:
                if (n.Name is not null)
                    BindVariable(n.Name, ExprContextType.Store, n);
                VisitNullableNode(n.Pattern);
                break;

            case MatchOrNode n:
                var bindNames = GetBindNames(n.Patterns[0]);
                foreach (var p in n.Patterns.Skip(1))
                {
                    var otherBindNames = GetBindNames(p);
                    if (!bindNames.SetEquals(otherBindNames))
                        throw SyntaxError(PySR.InvalidSyntax_Semantic_BindDifferentNames);
                }
                VisitNodes(n.Patterns);
                break;

            case MatchSequenceNode n:
                if (n.Patterns.Count(static pattern => pattern is MatchStarNode) > 1)
                    throw SyntaxError(PySR.InvalidSyntax_Semantic_MultipleStarredNames);
                VisitNodes(n.Patterns);
                break;

            case MatchClassNode n:
                // CPython check_kwd_patterns runs before the repeated-attribute
                // check. The keyword is an attribute name, not a binding, so
                // only the reserved-name check applies.
                foreach (var kwdAttr in n.KwdAttrs)
                    CheckReservedName(kwdAttr, ExprContextType.Store, n);

                for (int i = 1; i < n.KwdAttrs.Length; i++)
                {
                    for (int j = 0; j < i; j++)
                    {
                        if (n.KwdAttrs[j].Equals(n.KwdAttrs[i], StringComparison.Ordinal))
                            throw SyntaxError(PySR.InvalidSyntax_Semantic_AttributeRepeated, n.KwdAttrs[j]);
                    }
                }
                VisitNode(n.Cls);
                VisitNodes(n.Patterns);
                VisitNodes(n.KwdPatterns);
                break;

            case MatchValueNode n:
                VisitNode(n.Value);
                break;

            case MatchSingletonNode n:
                break;

            default:
                throw new UnreachableException();
        }
    }

    private void VisitArgumentsDefaults(AstArgumentsNode args)
    {
        VisitNullableNodes(args.KwDefaults);
        VisitNodes(args.Defaults);
    }

    private void VisitArgumentsArgs(AstArgumentsNode args)
    {
        VisitNodes(args.PosonlyArgs);
        VisitNodes(args.Args);
        VisitNullableNode(args.VarArg);
        VisitNodes(args.KwonlyArgs);
        VisitNullableNode(args.KwArg);
    }

    private static IEnumerable<AstPatternNode> EnumeratePatterns(AstPatternNode pattern)
    {
        yield return pattern;

        foreach (var subPattern in pattern.EnumerateSubPatterns())
        {
            foreach (var p in EnumeratePatterns(subPattern))
                yield return p;
        }
    }

    private static HashSet<string> GetBindNames(AstPatternNode pattern)
    {
        HashSet<string> result = [];
        foreach (var p in EnumeratePatterns(pattern))
        {
            if (p is MatchAsNode { Name: string asName })
                result.Add(asName);
            else if (p is MatchStarNode { Name: string starName })
                result.Add(starName);
        }
        return result;
    }

    internal void CheckClosureAndFillCapturedVariables(RootVariableScope root)
    {
        CheckClosureAndFillCapturedVariablesImpl(root);

        void CheckClosureAndFillCapturedVariablesImpl(VariableScope scope)
        {
            foreach (var (name, type) in scope.Variables)
            {
                if (type is not PyVariableType.Closure)
                    continue;

                var parent = scope.Parent;
                HashSet<IScopeWithFreeVars> scopesRequiringFree = scope is IScopeWithFreeVars c ? [c] : [];
                while (true)
                {
                    if (parent is null)
                    {
                        var declaration = _nonlocalDeclarations.TryGetValue((scope, name), out var nonlocalNode)
                            ? SyntaxErrorAt(nonlocalNode, PySR.InvalidSyntax_Semantic_NonlocalNoBinding, name)
                            : SyntaxError(PySR.InvalidSyntax_Semantic_NonlocalNoBinding, name);
                        throw WithoutSourceText(declaration);
                    }

                    if (parent is CallableVariableScope callableVariableScope &&
                            parent.Variables.TryGetValue(name, out var typeOfParentVariable) &&
                            typeOfParentVariable is not PyVariableType.Closure)
                    {
                        callableVariableScope.CaptureVariable(name);
                        if (callableVariableScope.ScopesRequiringFree.TryGetValue(name, out var scopes))
                            scopes.UnionWith(scopesRequiringFree);
                        else
                            callableVariableScope.ScopesRequiringFree[name] = scopesRequiringFree;
                        break;
                    }

                    // A comprehension target captured by a nested function: the
                    // inline comprehension scope owns the cell (CapturedLocal)
                    // and the referencing scopes read it through their closure.
                    if (parent is ComprehensionVariableScope compScope &&
                        compScope.Variables.TryGetValue(name, out var typeOfCompVariable) &&
                        typeOfCompVariable is not (PyVariableType.Global or PyVariableType.Closure))
                    {
                        compScope.CaptureVariable(name);
                        if (compScope.ScopesRequiringFree.TryGetValue(name, out var scopes))
                            scopes.UnionWith(scopesRequiringFree);
                        else
                            compScope.ScopesRequiringFree[name] = scopesRequiringFree;
                        break;
                    }

                    // Type params defined in GenericParamVariableScope (e.g. class C[T]:)
                    // are accessible to the class body as free vars. The type param remains
                    // Local in the producer scope (not CapturedLocal) so StoreName works,
                    // while the consumer (class body) uses LoadDeref on cell objects.
                    if (parent is GenericParamVariableScope genericParamScope && parent.Variables.ContainsKey(name))
                    {
                        if (genericParamScope.ScopesRequiringFree.TryGetValue(name, out var scopes))
                            scopes.UnionWith(scopesRequiringFree);
                        else
                            genericParamScope.ScopesRequiringFree[name] = scopesRequiringFree;
                        break;
                    }

                    if (name is PySpecialNames.Class &&
                        parent is ClassVariableScope classVariableScope)
                    {
                        classVariableScope.ClassCaptured = true;
                        // Register ALL intermediate scopes (including the initiating scope)
                        // so FillTempFreesClass propagates __class__ through the entire
                        // nested function chain. Without this, intermediate functions
                        // (like `outer` in `new->outer->inner`) would miss __class__
                        // in their FreeVars, causing GetFreeVars to fall into the class
                        // branch and crash on a function frame with no _locals dict.
                        classVariableScope.ScopesRequiringFree.UnionWith(scopesRequiringFree);
                        break;
                    }

                    if (parent is IScopeWithFreeVars scopeWithFreeVars)
                        scopesRequiringFree.Add(scopeWithFreeVars);

                    parent = parent.Parent;
                }
            }

            foreach (var childScope in scope.Children)
                CheckClosureAndFillCapturedVariablesImpl(childScope);
        }
    }

    private void VisitMod(AstModNode node)
    {
        switch (node)
        {
            case ModuleNode n: VisitModule(n); break;
            case ExpressionNode n: VisitExpression(n); break;
            case InteractiveNode n: VisitInteractive(n); break;
        }
    }

    private void VisitModule(ModuleNode node)
    {
        VisitNodes(node.Body);
    }

    private void VisitExpression(ExpressionNode node)
    {
        VisitNode(node.Body);
    }

    private void VisitInteractive(InteractiveNode node)
    {
        VisitNodes(node.Body);
    }

    private readonly ref struct NodeScope : IDisposable
    {
        private readonly SemanticAnalyzer _analyzer;
        private readonly AstNode _node;

        internal NodeScope(SemanticAnalyzer analyzer, AstNode node)
        {
            _analyzer = analyzer;
            _node = node;
            _analyzer._nodesToRoot.Push(node);
        }

        void IDisposable.Dispose()
        {
            var poppedNode = _analyzer._nodesToRoot.Pop();
            Debug.Assert(ReferenceEquals(poppedNode, _node));
        }
    }

    // Marks a normally-visited position as a restricted-expression block,
    // the symtable equivalent of symtable_enter_block(..., AnnotationBlock/
    // TypeAliasBlock/...) paired with its exit
    private readonly ref struct RestrictedExprBlockGuard : IDisposable
    {
        private readonly SemanticAnalyzer _analyzer;

        internal RestrictedExprBlockGuard(SemanticAnalyzer analyzer, string block)
        {
            _analyzer = analyzer;
            _analyzer._restrictedExprBlocks.Push((block, analyzer._currentScopeStats.Scope));
        }

        void IDisposable.Dispose() => _analyzer._restrictedExprBlocks.Pop();
    }
}
