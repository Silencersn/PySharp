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

public sealed class SemanticModel
{
    private readonly Dictionary<AstNode, VariableScope> _nodeToScope = [];
    private readonly AstModNode _root;

    internal AstModNode Root => _root;

    internal SemanticModel(AstModNode root)
    {
        _root = root;
    }

    internal void AppendScope(AstNode node, VariableScope scope)
    {
        _nodeToScope.Add(node, scope);
    }

    internal T? GetVariableScope<T>(AstNode node) where T : VariableScope
    {
        if (!_nodeToScope.TryGetValue(node, out var scope))
            return null;

        return scope as T;
    }
}

public sealed class SemanticAnalyzer : ICodeMetaInfoProvider
{
    public static SemanticModel Analyze(PyCallContext context, AstModNode root)
    {
        var scope = InternalAnalyze(context, root);
        var model = new SemanticModel(root);
        scope.Bind(model);
        return model;
    }

    internal static RootVariableScope InternalAnalyze(PyCallContext context, AstModNode root)
    {
        var analyzer = new SemanticAnalyzer(context);
        var scope = analyzer.BuildBasicScope(root);
        FillUnknownVariables(scope);
        analyzer.CheckClosureAndFillCapturedVariables(scope);
        FillCallableProperties(scope);
        return scope;
    }

    private readonly PyCallContext _context;
    private readonly Stack<AstNode> _nodesToRoot;

    CodeMetaInfo? ICodeMetaInfoProvider.MetaInfo => _nodesToRoot.TryPeek(out var node) ? node.MetaInfo : null;

    private SemanticAnalyzer(PyCallContext context)
    {
        _nodesToRoot = [];
        _context = context;
        _context.CurrentFrame.MetaInfoProvider = this;
    }

    public PyRuntimeException SyntaxError(string message = PySR.InvalidSyntax, params ReadOnlySpan<object?> args)
    {
        return _context.SyntaxError(message, args);
    }

    private sealed class ScopeStats
    {
        public readonly VariableScope Scope;
        public int LoopDepth;
        public readonly Stack<AstExprNode> ComprehensionDepth;
        public int FinallyDepth;
        public AstExprNode? CurrentComprehension => ComprehensionDepth.TryPeek(out var comp) ? comp : null;

        internal ScopeStats(VariableScope scope)
        {
            Scope = scope;
            LoopDepth = 0;
            ComprehensionDepth = [];
        }
    }

    internal RootVariableScope BuildBasicScope(AstModNode root)
    {
        Stack<ScopeStats> scopeStatsStack = [];
        var rootScope = new RootVariableScope(root);
        var currentScopeStats = new ScopeStats(rootScope);

        _nodesToRoot.Push(root);

        foreach (var subNode in root.EnumerateSubNodes())
            BuildBasicScopeImpl(subNode);
        Debug.Assert(scopeStatsStack.Count is 0);
        return rootScope;

        void BuildBasicScopeImpl(AstNode node)
        {
            CheckValid(node);
            TryAppendVariableTo(currentScopeStats.Scope, node);

            VariableScope? scope = node switch
            {
                ModuleNode n => throw new UnreachableException(),
                ClassDefNode n => new ClassVariableScope(n, currentScopeStats.Scope),
                FunctionDefNode n => new FunctionVariableScope(n, currentScopeStats.Scope),
                LambdaNode n => new LambdaVariableScope(n, currentScopeStats.Scope),
                _ => null
            };

            _nodesToRoot.Push(node);

            if (scope is not null)
            {
                if (node is IScopedSubNodesProvider provider)
                {
                    foreach (var subNode in provider.EnumerateSubNodesOuterScope())
                        BuildBasicScopeImpl(subNode);
                }

                scopeStatsStack.Push(currentScopeStats);
                currentScopeStats = new ScopeStats(scope);
            }

            if (node is ForNode or WhileNode)
            {
                currentScopeStats.LoopDepth++;
            }

            if (node is AstExprNode expr && expr is ListCompNode or SetCompNode or DictCompNode or GeneratorExpNode)
            {
                currentScopeStats.ComprehensionDepth.Push(expr);
            }

            {
                if (node is TryNode tryNode)
                {
                    foreach (var subNode in tryNode.Body)
                        BuildBasicScopeImpl(subNode);

                    foreach (var subNode in tryNode.Exceptors)
                        BuildBasicScopeImpl(subNode);

                    foreach (var subNode in tryNode.OrElse)
                        BuildBasicScopeImpl(subNode);

                    currentScopeStats.FinallyDepth++;
                    foreach (var subNode in tryNode.FinalBody)
                        BuildBasicScopeImpl(subNode);
                    currentScopeStats.FinallyDepth--;
                }
                else if (node is TryStarNode tryStarNode)
                {
                    foreach (var subNode in tryStarNode.Body)
                        BuildBasicScopeImpl(subNode);

                    foreach (var subNode in tryStarNode.Exceptors)
                        BuildBasicScopeImpl(subNode);

                    foreach (var subNode in tryStarNode.OrElse)
                        BuildBasicScopeImpl(subNode);

                    currentScopeStats.FinallyDepth++;
                    foreach (var subNode in tryStarNode.FinalBody)
                        BuildBasicScopeImpl(subNode);
                    currentScopeStats.FinallyDepth--;
                }
                else if (node is IScopedSubNodesProvider provider)
                {
                    foreach (var subNode in provider.EnumerateSubNodesInnerScope())
                        BuildBasicScopeImpl(subNode);
                }
                else
                {
                    foreach (var subNode in node.EnumerateSubNodes())
                        BuildBasicScopeImpl(subNode);
                }
            }

            if (node is ListCompNode or SetCompNode or DictCompNode or GeneratorExpNode)
            {
                currentScopeStats.ComprehensionDepth.Pop();
            }

            if (node is ForNode or WhileNode)
            {
                currentScopeStats.LoopDepth--;
            }

            if (scope is not null)
            {
                Debug.Assert(currentScopeStats.CurrentComprehension is null);
                Debug.Assert(currentScopeStats.LoopDepth is 0);

                currentScopeStats = scopeStatsStack.Pop();
            }

            var poppedNode = _nodesToRoot.Pop();
            Debug.Assert(ReferenceEquals(poppedNode, node));
        }

        void CheckValid(AstNode node)
        {
            switch (node)
            {
                case BreakNode:
                    if (currentScopeStats.LoopDepth is 0)
                        throw SyntaxError(PySR.InvalidSyntax_Semantic_BreakOutsideLoop);
                    if (currentScopeStats.FinallyDepth > 0)
                        CheckControlStmtNotInFinallyUntil(_nodesToRoot, static n => n is ForNode or WhileNode, PySR.InvalidSyntax_Semantic_BreakInFinally);
                    break;

                case ContinueNode:
                    if (currentScopeStats.LoopDepth is 0)
                        throw SyntaxError(PySR.InvalidSyntax_Semantic_ContinueOutsideLoop);
                    if (currentScopeStats.FinallyDepth > 0)
                        CheckControlStmtNotInFinallyUntil(_nodesToRoot, static n => n is ForNode or WhileNode, PySR.InvalidSyntax_Semantic_ContinueInFinally);
                    break;

                case ReturnNode:
                    if (currentScopeStats.Scope is not FunctionVariableScope)
                        throw SyntaxError(PySR.InvalidSyntax_Semantic_ReturnOutsideFunction);
                    if (currentScopeStats.FinallyDepth > 0)
                        CheckControlStmtNotInFinallyUntil(_nodesToRoot, static n => n is FunctionDefNode, PySR.InvalidSyntax_Semantic_ReturnInFinally);
                    break;

                case YieldNode:
                    if (currentScopeStats.CurrentComprehension is not null)
                        throw SyntaxError(PySR.InvalidSyntax_Semantic_YieldInsideComprehension, AstUtils.GetExprNodeName(currentScopeStats.CurrentComprehension));

                    if (currentScopeStats.Scope is not CallableVariableScope callableYieldScope)
                        throw SyntaxError(PySR.InvalidSyntax_Semantic_YieldOutsideFunction);

                    callableYieldScope.HasYield = true;
                    break;

                case YieldFromNode:
                    if (currentScopeStats.CurrentComprehension is not null)
                        throw SyntaxError(PySR.InvalidSyntax_Semantic_YieldFromInsideComprehension, AstUtils.GetExprNodeName(currentScopeStats.CurrentComprehension));

                    if (currentScopeStats.Scope is not CallableVariableScope callableYieldFromScope)
                        throw SyntaxError(PySR.InvalidSyntax_Semantic_YieldFromOutsideFunction);

                    callableYieldFromScope.HasYield = true;
                    break;

                case CallNode n:
                    for (int i = 0; i < n.Keywords.Length; i++)
                    {
                        var currentKeyword = n.Keywords[i];
                        foreach (var previousKeyword in n.Keywords.Take(i))
                        {
                            if (previousKeyword.Arg == currentKeyword.Arg)
                                throw SyntaxError(PySR.InvalidSyntax_Semantic_KeywordArgumentRepeated, currentKeyword.Arg);
                        }
                    }
                    break;

                case TryNode n:
                    for (int i = 0; i < n.Exceptors.Length; i++)
                    {
                        if (n.Exceptors[i].Type is not null)
                            continue;

                        if (i < n.Exceptors.Length - 1)
                            throw SyntaxError(PySR.InvalidSyntax_Semantic_NonLastDefaultExcept);
                    }
                    break;

                case MatchNode n:
                    for (int i = 0; i < n.Cases.Length; i++)
                    {
                        var c = n.Cases[i];

                        var enumerator = EnumeratePossiblyIrrefutablePatterns(c.Pattern).GetEnumerator();

                        MatchAsNode? irrefutablePattern = null;
                        while (enumerator.MoveNext())
                        {
                            if (enumerator.Current is not MatchAsNode { Pattern: null } matchAs)
                                continue;

                            // wildcard_pattern or capture_pattern
                            irrefutablePattern = matchAs;
                            break;
                        }

                        if (irrefutablePattern is null)
                            continue;

                        var isLast = !enumerator.MoveNext();

                        if (!isLast)
                            ThrowUnreachable(irrefutablePattern);

                        if (c.Guard is not null)
                            continue;

                        if (i < n.Cases.Length - 1)
                            ThrowUnreachable(irrefutablePattern);
                    }

                    static IEnumerable<AstPatternNode> EnumeratePossiblyIrrefutablePatterns(AstPatternNode pattern)
                    {
                        yield return pattern;

                        // After yield return pattern, we stop traversing child patterns except for MatchAsNode and MatchOrNode.
                        // This is because nodes other than MatchAsNode and MatchOrNode are considered always possibly non-irrefutable.
                        // Therefore, we only care about the existence of the pattern itself (which affects isLast).
                        if (pattern is not (MatchAsNode or MatchOrNode))
                            yield break;

                        foreach (var node in pattern.EnumerateSubNodes())
                        {
                            if (node is AstPatternNode subPattern)
                            {
                                foreach (var p in EnumeratePossiblyIrrefutablePatterns(subPattern))
                                    yield return p;
                            }
                        }
                    }

                    void ThrowUnreachable(MatchAsNode irrefutablePattern)
                    {
                        if (irrefutablePattern.Name is null)
                            throw SyntaxError(PySR.InvalidSyntax_Semantic_UnreachablePatterns_Wildcard);

                        throw SyntaxError(PySR.InvalidSyntax_Semantic_UnreachablePatterns_Capture, irrefutablePattern.Name);
                    }

                    break;

                case MatchOrNode n:
                    var bindNames = GetBindNames(n.Patterns[0]);
                    foreach (var p in n.Patterns.Skip(1))
                    {
                        var otherBindNames = GetBindNames(p);
                        if (!bindNames.SetEquals(otherBindNames))
                            throw SyntaxError(PySR.InvalidSyntax_Semantic_BindDifferentNames);
                    }

                    static IEnumerable<AstPatternNode> EnumeratePatterns(AstPatternNode pattern)
                    {
                        yield return pattern;

                        foreach (var node in pattern.EnumerateSubNodes())
                        {
                            if (node is AstPatternNode subPattern)
                            {
                                foreach (var p in EnumeratePatterns(subPattern))
                                    yield return p;
                            }
                        }
                    }

                    static HashSet<string> GetBindNames(AstPatternNode pattern)
                    {
                        HashSet<string> result = [];
                        foreach (var p in EnumeratePatterns(pattern))
                        {
                            if (p is MatchAsNode { Name: not null } matchAs)
                                result.Add(matchAs.Name);
                            else if (p is MatchStarNode { Name: not null } matchStar)
                                result.Add(matchStar.Name);
                        }
                        return result;
                    }

                    break;

                case MatchSequenceNode n:
                    if (n.Patterns.Count(static pattern => pattern is MatchStarNode) > 1)
                        throw SyntaxError(PySR.InvalidSyntax_Semantic_MultipleStarredNames);
                    break;

                case MatchMappingNode n:
                    var literalKeys = n.Keys.OfType<ConstantNode>().Select(static node => node.Value).ToArray();
                    foreach (var (key1, key2) in EnumeratePairs(literalKeys))
                    {
                        if (PyObjectComparer.Default.Equals(key1, key2))
                            throw SyntaxError(PySR.InvalidSyntax_Semantic_MappingDuplicateKey, PySpecialMethods.Str(_context, key1).PyUnwrap(_context).Value);
                    }
                    break;

                case MatchClassNode n:
                    foreach (var (attr1, attr2) in EnumeratePairs(n.KwdAttrs))
                    {
                        if (attr1.Equals(attr2, StringComparison.Ordinal))
                            throw SyntaxError(PySR.InvalidSyntax_Semantic_AttributeRepeated, attr1);
                    }
                    break;

                case ListNode n when n.Ctx is ExprContextType.Store:
                    ValidateNonMultipleStarred(n.Elts);
                    break;

                case TupleNode n when n.Ctx is ExprContextType.Store:
                    ValidateNonMultipleStarred(n.Elts);
                    break;

                case ImportFromNode n when currentScopeStats.Scope is not RootVariableScope:
                    if (n.IsImportStar())
                        throw SyntaxError(PySR.InvalidSyntax_Semantic_ImportStarNotAtModuleLevel);
                    break;
            }

            static IEnumerable<(T, T)> EnumeratePairs<T>(IReadOnlyList<T> items)
            {
                for (int i = 1; i < items.Count; i++)
                {
                    for (int j = 0; j < i; j++)
                        yield return (items[j], items[i]);
                }
            }

            void ValidateNonMultipleStarred(ImmutableArray<AstExprNode> targets)
            {
                if (targets.Count(static node => node is StarredNode) > 1)
                    throw SyntaxError(PySR.InvalidSyntax_Semantic_MultipleStarredInAssignment);
            }
        }
    }

    internal void CheckControlStmtNotInFinallyUntil(IEnumerable<AstNode> nodesToRoot, Func<AstNode, bool> stopPredicate, string warningMessage)
    {
        foreach (var node in nodesToRoot)
        {
            if (node is ModuleNode or ClassDefNode or FunctionDefNode or LambdaNode)
                return;

            if (stopPredicate(node))
                return;

            if (node is TryNode)
            {
                _context.TryWarn(PySyntaxWarningObjectType.Shared, warningMessage);
                return;
            }
        }
    }

    internal void TryAppendVariableTo(VariableScope currentScope, AstNode node)
    {
        switch (node)
        {
            case NameNode n:
                currentScope.AppendVariable(n.Id, n.Ctx);
                break;

            case FunctionDefNode n:
                currentScope.AppendVariable(n.Name, ExprContextType.Store);
                break;

            case ClassDefNode n:
                currentScope.AppendVariable(n.Name, ExprContextType.Store);
                break;

            case GlobalNode n:
                if (currentScope.IsRoot)
                    break;

                foreach (var name in n.Names)
                {
                    if (!currentScope.Variables.TryGetValue(name, out var type))
                    {
                        currentScope.Variables.Add(name, PyVariableType.Global);
                        continue;
                    }

                    switch (type)
                    {
                        case PyVariableType.Global:
                            break;

                        case PyVariableType.Parameter:
                            throw SyntaxError(PySR.InvalidSyntax_Semantic_BothParameterAndGlobal, name);

                        case PyVariableType.Nonlocal:
                            throw SyntaxError(PySR.InvalidSyntax_Semantic_BothNonlocalAndGlobal, name);

                        default:
                            if (currentScope.FirstContext[name] is ExprContextType.Load)
                                throw SyntaxError(PySR.InvalidSyntax_Semantic_UsedPriorToGlobal, name);
                            else
                                throw SyntaxError(PySR.InvalidSyntax_Semantic_AssignToBeforeGlobal, name);
                    }
                }

                break;

            case NonlocalNode n:
                if (currentScope.IsRoot)
                    throw SyntaxError(PySR.InvalidSyntax_Semantic_NonlocalAtModule);

                foreach (var name in n.Names)
                {
                    if (!currentScope.Variables.TryGetValue(name, out var type))
                    {
                        currentScope.Variables.Add(name, PyVariableType.Nonlocal);
                        continue;
                    }

                    switch (type)
                    {
                        case PyVariableType.Nonlocal:
                            break;

                        case PyVariableType.Parameter:
                            throw SyntaxError(PySR.InvalidSyntax_Semantic_BothParameterAndNonlocal, name);

                        case PyVariableType.Global:
                            throw SyntaxError(PySR.InvalidSyntax_Semantic_BothNonlocalAndGlobal, name);

                        default:
                            if (currentScope.FirstContext[name] is ExprContextType.Load)
                                throw SyntaxError(PySR.InvalidSyntax_Semantic_UsedPriorToNonlocal, name);
                            else
                                throw SyntaxError(PySR.InvalidSyntax_Semantic_AssignToBeforeNonlocal, name);
                    }
                }

                break;

            case ExceptHandlerNode n when n.Name is not null:
                currentScope.AppendVariable(n.Name, ExprContextType.Store);
                break;

            case AstArgNode n:
                if (currentScope.Variables.ContainsKey(n.Arg))
                    throw SyntaxError(PySR.InvalidSyntax_Semantic_DuplicateArgument, n.Arg);
                currentScope.Variables[n.Arg] = PyVariableType.Parameter;
                break;

            case AstAliasNode n:
                currentScope.AppendVariable(n.GetLocalName(), ExprContextType.Store);
                break;

            case MatchStarNode n:
                if (n.Name is not null)
                    currentScope.AppendVariable(n.Name, ExprContextType.Store);
                break;

            case MatchMappingNode n:
                if (n.Rest is not null)
                    currentScope.AppendVariable(n.Rest, ExprContextType.Store);
                break;

            case MatchAsNode n:
                if (n.Name is not null)
                    currentScope.AppendVariable(n.Name, ExprContextType.Store);
                break;
        }
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

                    parent = parent.Parent;
                }
            }

            foreach (var childScope in scope.Children)
                FillUnknownVariablesImpl(childScope);
        }
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
                HashSet<CallableVariableScope> scopesRequiringFree = scope is CallableVariableScope c ? [c] : [];
                while (true)
                {
                    if (parent is null)
                        throw SyntaxError(PySR.InvalidSyntax_Semantic_NonlocalNoBinding, name);

                    if (parent is CallableVariableScope callableVariableScope)
                    {
                        if (parent.Variables.TryGetValue(name, out var typeOfParentVariable) &&
                            typeOfParentVariable is not PyVariableType.Closure)
                        {
                            callableVariableScope.CaptureVariable(name);
                            if (callableVariableScope.ScopesRequiringFree.TryGetValue(name, out var scopes))
                                scopes.UnionWith(scopesRequiringFree);
                            else
                                callableVariableScope.ScopesRequiringFree[name] = scopesRequiringFree;
                            break;
                        }
                        else
                        {
                            scopesRequiringFree.Add(callableVariableScope);
                        }
                    }

                    if (name is PySpecialNames.Class && parent is ClassVariableScope classVariableScope)
                    {
                        classVariableScope.ClassCaptured = true;
                        if (scope is CallableVariableScope cvs)
                            classVariableScope.ScopesRequiringFree.Add(cvs);
                        break;
                    }

                    parent = parent.Parent;
                }
            }

            foreach (var childScope in scope.Children)
                CheckClosureAndFillCapturedVariablesImpl(childScope);
        }
    }

    internal static void FillCallableProperties(RootVariableScope scope)
    {
        FillTempFreesClass(scope);
        FillTempFrees(scope);
        FillCallablePropertiesImpl(scope);

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

            foreach (var child in scope.Children)
                FillTempFrees(child);
        }

        static void FillCallablePropertiesImpl(VariableScope scope)
        {
            foreach (var child in scope.Children)
                FillCallablePropertiesImpl(child);

            if (scope is not CallableVariableScope callableScope)
                return;

            callableScope.VarNames = [.. callableScope.Variables
                .Where(pair => pair.Value is PyVariableType.Local or PyVariableType.Parameter or PyVariableType.CapturedParameter)
                .Select(pair => pair.Key)];

            callableScope.CellVars = [.. callableScope.Variables
                .Where(pair => pair.Value is PyVariableType.CapturedLocal or PyVariableType.CapturedParameter)
                .Select(pair => pair.Key)];

            callableScope.FreeVars = [.. callableScope.TempFrees.Distinct()];

            callableScope.LocalsTable = callableScope.Variables
                .Where(pair => pair.Value is PyVariableType.Local or PyVariableType.Parameter)
                .Select(pair => pair.Key)
                .Concat(callableScope.CellVars)
                .Concat(callableScope.FreeVars)
                .Distinct()
                .Index()
                .ToFrozenDictionary(static indexed => indexed.Item, static indexed => indexed.Index);
        }
    }
}

internal abstract class VariableScope
{
    public abstract AstNode Owner { get; }
    public OrderedDictionary<string, PyVariableType> Variables { get; } = [];

    // used for detecting global stmt and nonlocal stmt
    // root scope does not need to maintain this property
    internal Dictionary<string, ExprContextType> FirstContext { get; } = [];

    public VariableScope? Parent { get; }
    public List<VariableScope> Children { get; } = [];

    [MemberNotNullWhen(false, nameof(Parent), nameof(Name), nameof(QualName))]
    public bool IsRoot => Parent is null;

    public abstract string? Name { get; }
    public string? QualName
    {
        get
        {
            if (IsRoot)
                return null;

            if (field is null)
            {
                Stack<string> nameToRoot = [];
                nameToRoot.Push(Name);

                var currentName = Name;
                var parent = Parent;
                while (!parent.IsRoot && (currentName is "<lambda>" || parent.Variables[currentName] is not PyVariableType.Global))
                {
                    if (parent is CallableVariableScope)
                        nameToRoot.Push("<locals>");
                    nameToRoot.Push(parent.Name);

                    currentName = parent.Name;
                    parent = parent.Parent;
                }
                field = string.Join('.', nameToRoot);
            }

            return field;
        }
    }

    protected VariableScope(VariableScope? parent)
    {
        Parent = parent;
        Parent?.Children.Add(this);
    }

    public void AppendVariable(string name, ExprContextType ctx)
    {
        if (IsRoot)
        {
            Variables[name] = PyVariableType.Global;
            return;
        }

        FirstContext.TryAdd(name, ctx);
        switch (ctx)
        {
            case ExprContextType.Load:
                Variables.TryAdd(name, PyVariableType.Unknown);
                if (name is "super" && this is CallableVariableScope)
                {
                    var parent = Parent;
                    while (true)
                    {
                        if (parent is null)
                            break;

                        if (parent is not CallableVariableScope)
                        {
                            if (parent is ClassVariableScope)
                                AppendVariable(PySpecialNames.Class, ExprContextType.Load);
                            break;
                        }

                        parent = parent.Parent;
                    }
                }
                break;

            case ExprContextType.Store:
            case ExprContextType.Del:
                if (!Variables.TryGetValue(name, out var type) || type is PyVariableType.Unknown)
                    Variables[name] = PyVariableType.Local;
                break;

            default:
                throw new UnreachableException();
        }
    }

    public void Bind(SemanticModel model)
    {
        model.AppendScope(Owner, this);

        foreach (var childScope in Children)
            childScope.Bind(model);
    }
}

internal sealed class RootVariableScope : VariableScope
{
    public override AstModNode Owner { get; }
    public override string? Name => null;

    public RootVariableScope(AstModNode owner) : base(null)
    {
        Owner = owner;
    }
}

internal sealed class ClassVariableScope : VariableScope
{
    public override ClassDefNode Owner { get; }
    public override string? Name => Owner.Name;
    public bool ClassCaptured { get; set; }
    internal HashSet<CallableVariableScope> ScopesRequiringFree = [];
    public PyCodeObject? CodeObject { get; set; }

    public ClassVariableScope(ClassDefNode owner, VariableScope parent) : base(parent)
    {
        Owner = owner;
    }
}

internal abstract class CallableVariableScope : VariableScope
{
    internal abstract AstArgumentsNode ArgumentsNode { get; }
    public bool HasYield { get; internal set; }
    public FrozenDictionary<string, int> LocalsTable { get; internal set; } = FrozenDictionary<string, int>.Empty;
    public string[] VarNames { get; internal set; } = [];
    public string[] CellVars { get; internal set; } = [];
    public string[] FreeVars { get; internal set; } = [];
    public List<string> TempFrees = [];
    public Dictionary<string, HashSet<CallableVariableScope>> ScopesRequiringFree = [];
    public PyCodeObject? CodeObject { get; set; }

    protected CallableVariableScope(VariableScope? parent) : base(parent)
    {
    }

    internal void CaptureVariable(string name)
    {
        var type = Variables[name];
        if (type is PyVariableType.CapturedLocal or PyVariableType.CapturedParameter)
            return;

        Debug.Assert(type is PyVariableType.Local or PyVariableType.Parameter);
        Variables[name] = type is PyVariableType.Local
            ? PyVariableType.CapturedLocal : PyVariableType.CapturedParameter;
    }
}

internal sealed class FunctionVariableScope : CallableVariableScope
{
    internal override AstArgumentsNode ArgumentsNode => Owner.Args;
    public override FunctionDefNode Owner { get; }
    public override string? Name => Owner.Name;

    public FunctionVariableScope(FunctionDefNode owner, VariableScope parent) : base(parent)
    {
        Owner = owner;
    }
}

internal sealed class LambdaVariableScope : CallableVariableScope
{
    internal override AstArgumentsNode ArgumentsNode => Owner.Args;
    public override LambdaNode Owner { get; }
    public override string? Name => "<lambda>";

    public LambdaVariableScope(LambdaNode owner, VariableScope parent) : base(parent)
    {
        Owner = owner;
    }
}