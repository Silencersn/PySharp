using System;
using System.Collections.Generic;
using System.Text;
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
using System.Linq;

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

    CodeMetaInfo? ICodeMetaInfoProvider.MetaInfo => _nodesToRoot.TryPeek(out var node) ? CodeMetaInfo.FromSpan(_source, node.MetaInfo.Range, node.MetaInfo.CrucialRange) : null;

    private SemanticAnalyzer(PyCallContext context, CodeSource source)
    {
        _nodesToRoot = [];
        _scopeStatsStack = [];
        _context = context;
        _source = source;
        _currentScopeStats = null!;
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
        public Stack<AstExprNode> ComprehensionDepth => field ??= [];
        public AstExprNode? CurrentComprehension => ComprehensionDepth.TryPeek(out var comp) ? comp : null;

        internal ScopeStats(VariableScope scope)
        {
            Scope = scope;
            LoopDepth = 0;
            FinallyDepth = 0;
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

            foreach (var child in scope.Children)
                FillTempFrees(child);
        }

        static void FillPropertiesImpl(VariableScope scope)
        {
            foreach (var child in scope.Children)
                FillPropertiesImpl(child);

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
                .OrderBy(pair => {
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
        _currentScopeStats = new ScopeStats(rootScope);

        VisitNode(root);

        Debug.Assert(_scopeStatsStack.Count is 0);
        return rootScope;
    }

    internal void CheckControlStmtNotInFinallyUntil(Func<AstNode, bool> stopPredicate, string warningMessage)
    {
        foreach (var node in _nodesToRoot)
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

    private void PushScope(VariableScope nextScope)
    {
        _scopeStatsStack.Push(_currentScopeStats);
        _currentScopeStats = new ScopeStats(nextScope);
    }

    private void PopScope()
    {
        Debug.Assert(_currentScopeStats.CurrentComprehension is null);
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

    private void VisitMisc(AstNode node)
    {
        switch (node)
        {
            case AstArgNode n: 
                if (_currentScopeStats.Scope.Variables.ContainsKey(n.Arg))
                    throw SyntaxError(PySR.InvalidSyntax_Semantic_DuplicateArgument, n.Arg);
                _currentScopeStats.Scope.Variables[n.Arg] = PyVariableType.Parameter;
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
                VisitNode(n.Target);
                VisitNode(n.Iter);
                VisitNodes(n.Ifs);
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

    private static IEnumerable<AstPatternNode> EnumeratePatterns(AstPatternNode pattern)
    {
        yield return pattern;

        foreach (var node in pattern.EnumerateSubNodes())
        {
            if (node is not AstPatternNode subPattern)
                continue;

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

                    if (name is PySpecialNames.Class &&
                        parent is ClassVariableScope classVariableScope)
                    {
                        classVariableScope.ClassCaptured = true;
                        if (scope is CallableVariableScope cvs)
                            classVariableScope.ScopesRequiringFree.Add(cvs);
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
