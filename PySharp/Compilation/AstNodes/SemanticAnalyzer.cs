using PySharp.Compilation.CodeAnalysis;
using PySharp.Compilation.Primitives;
using PySharp.Modules.Builtins;
using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.Calls.Extensions;
using PySharp.Runtime.Comparison;
using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace PySharp.Compilation.AstNodes;

internal sealed partial class SemanticAnalyzer : ICodeMetaInfoProvider
{
    public static SemanticModel Analyze(PyCallContext context, CodeSource source, AstModNode root)
    {
        var scope = InternalAnalyze(context, source, root);
        var model = new SemanticModel(root);
        scope.Bind(model);
        return model;
    }

    internal static RootVariableScope InternalAnalyze(PyCallContext context, CodeSource source, AstModNode root)
    {
        var analyzer = new SemanticAnalyzer(context, source);
        var scope = analyzer.BuildBasicScope(root);
        FillUnknownVariables(scope);
        analyzer.CheckClosureAndFillCapturedVariables(scope);
        FillCallableProperties(scope);
        return scope;
    }

    private readonly CodeSource _source;
    private readonly PyCallContext _context;
    private readonly Stack<AstNode> _nodesToRoot;

    private readonly Stack<ScopeStats> _scopeStatsStack;
    private ScopeStats _currentScopeStats;
    private readonly Stack<NestedComprehensionStats> _nestedComprehensionStatsStack;
    private NestedComprehensionStats _currentNestedComprehensionStats;

    CodeMetaInfo? ICodeMetaInfoProvider.MetaInfo => _nodesToRoot.TryPeek(out var node) ? CodeMetaInfo.FromSpan(_source, node.MetaInfo.Range, node.MetaInfo.CrucialRange) : null;

    private SemanticAnalyzer(PyCallContext context, CodeSource source)
    {
        _nodesToRoot = [];
        _scopeStatsStack = [];
        _context = context;
        _source = source;
        _currentScopeStats = null!;
        _nestedComprehensionStatsStack = [];
        _currentNestedComprehensionStats = null!;
    }

    public PyRuntimeException SyntaxError(string message = PySR.InvalidSyntax, params ReadOnlySpan<object?> args)
    {
        return _context.SyntaxError(this, message, args);
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
                _ = _context.WarnSyntax(warningMessage, this).PyUnwrap(_context);
                return;
            }
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

        if (_currentScopeStats.Scope is not GeneratorExpVariableScope)
            _currentNestedComprehensionStats = _nestedComprehensionStatsStack.Pop();
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
        _nodesToRoot.Push(node);

        switch (node)
        {
            case AstModNode mod: VisitMod(mod); break;
            case AstStmtNode stmt: VisitStmt(stmt); break;
            case AstExprNode expr: VisitExpr(expr); break;
            default: VisitMisc(node); break;
        }

        var poppedNode = _nodesToRoot.Pop();
        Debug.Assert(ReferenceEquals(poppedNode, node));
    }

    private void AddParameter(string name)
    {
        _currentScopeStats.Scope.Variables.Add(name, PyVariableType.Parameter);
    }

    private void VisitMisc(AstNode node)
    {
        switch (node)
        {
            case AstArgNode n:
                if (_currentScopeStats.Scope.Variables.ContainsKey(n.Arg))
                    throw SyntaxError(PySR.InvalidSyntax_Semantic_DuplicateArgument, n.Arg);
                AddParameter(n.Arg);
                break;

            case AstAliasNode n:
                _currentScopeStats.Scope.AppendVariable(n.GetLocalName(), ExprContextType.Store);
                break;

            case AstPatternNode pattern:
                VisitPattern(pattern);
                break;

            case ExceptHandlerNode n:
                if (n.Name is not null)
                    _currentScopeStats.Scope.AppendVariable(n.Name, ExprContextType.Store);
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
                    _currentScopeStats.Scope.AppendVariable(n.Name, ExprContextType.Store);
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
                    _currentScopeStats.Scope.AppendVariable(n.Rest, ExprContextType.Store);
                VisitNodes(n.Keys);
                VisitNodes(n.Patterns);
                break;

            case MatchAsNode n:
                if (n.Name is not null)
                    _currentScopeStats.Scope.AppendVariable(n.Name, ExprContextType.Store);
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
                        throw SyntaxError(PySR.InvalidSyntax_Semantic_NonlocalNoBinding, name);

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
}
