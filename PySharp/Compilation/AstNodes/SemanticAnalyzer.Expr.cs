using PySharp.Compilation.Primitives;
using System.Collections.Immutable;
using System.Diagnostics;

namespace PySharp.Compilation.AstNodes;

partial class SemanticAnalyzer
{
    private void VisitExpr(AstExprNode node)
    {
        switch (node)
        {
            case ConstantNode n: VisitConstant(n); break;
            case NameNode n: VisitName(n); break;
            case CallNode n: VisitCall(n); break;
            case BinOpNode n: VisitBinOp(n); break;
            case UnaryOpNode n: VisitUnaryOp(n); break;
            case CompareNode n: VisitCompare(n); break;
            case AttributeNode n: VisitAttribute(n); break;
            case ListNode n: VisitList(n); break;
            case TupleNode n: VisitTuple(n); break;
            case SetNode n: VisitSet(n); break;
            case DictNode n: VisitDict(n); break;
            case ListCompNode n: VisitListComp(n); break;
            case SetCompNode n: VisitSetComp(n); break;
            case DictCompNode n: VisitDictComp(n); break;
            case GeneratorExpNode n: VisitGeneratorExp(n); break;
            case YieldNode n: VisitYield(n); break;
            case YieldFromNode n: VisitYieldFrom(n); break;
            case NamedExprNode n: VisitNamedExpr(n); break;
            case SubscriptNode n: VisitSubscript(n); break;
            case SliceNode n: VisitSlice(n); break;
            case IfExpNode n: VisitIfExp(n); break;
            case LambdaNode n: VisitLambda(n); break;
            case FormattedValueNode n: VisitFormattedValue(n); break;
            case JoinedStrNode n: VisitJoinedStr(n); break;
            case BoolOpNode n: VisitBoolOp(n); break;
            case StarredNode n: VisitStarred(n); break;
            default: throw new NotImplementedException();
        }
    }

    private void VisitConstant(ConstantNode node)
    {
    }

    private void VisitName(string name, ExprContextType ctx)
    {
        _currentScopeStats.Scope.AppendVariable(name, ctx);
    }

    private void VisitName(NameNode node)
    {
        VisitName(node.Id, node.Ctx);
    }

    private void VisitCall(CallNode node)
    {
        for (int i = 0; i < node.Keywords.Length; i++)
        {
            var currentKeyword = node.Keywords[i];
            if (currentKeyword.Arg is null) continue;
            for (int j = 0; j < i; j++)
            {
                var previousKeyword = node.Keywords[j];
                if (previousKeyword.Arg == currentKeyword.Arg)
                    throw SyntaxError(PySR.InvalidSyntax_Semantic_KeywordArgumentRepeated, currentKeyword.Arg);
            }
        }
        VisitNode(node.Func);
        VisitNodes(node.Args);
        VisitNodes(node.Keywords);
    }

    private void VisitBinOp(BinOpNode node)
    {
        VisitNode(node.Left);
        VisitNode(node.Right);
    }

    private void VisitUnaryOp(UnaryOpNode node)
    {
        VisitNode(node.Operand);
    }

    private void VisitCompare(CompareNode node)
    {
        VisitNode(node.Left);
        VisitNodes(node.Comparators);
    }

    private void VisitAttribute(AttributeNode node)
    {
        VisitNode(node.Value);
    }

    private void VisitList(ListNode node)
    {
        if (node.Ctx is ExprContextType.Store)
            ValidateNonMultipleStarred(node.Elts);
        VisitNodes(node.Elts);
    }

    private void VisitTuple(TupleNode node)
    {
        if (node.Ctx is ExprContextType.Store)
            ValidateNonMultipleStarred(node.Elts);
        VisitNodes(node.Elts);
    }

    private void ValidateNonMultipleStarred(ImmutableArray<AstExprNode> targets)
    {
        if (targets.Count(static node => node is StarredNode) > 1)
            throw SyntaxError(PySR.InvalidSyntax_Semantic_MultipleStarredInAssignment);
    }

    private void VisitSet(SetNode node)
    {
        VisitNodes(node.Elts);
    }

    private void VisitDict(DictNode node)
    {
        VisitNullableNodes(node.Keys);
        VisitNodes(node.Values);
    }

    private void VisitListComp(ListCompNode node)
    {
        _currentNestedComprehensionStats.PushComprehension(node);
        _currentNestedComprehensionStats.CurrentComprehensionStats.VisitingPart = ComprehensionStatsVisitingPart.Element;
        VisitNode(node.Elt);
        VisitNodes(node.Generators);
        _currentNestedComprehensionStats.PopComprehension();
    }

    private void VisitSetComp(SetCompNode node)
    {
        _currentNestedComprehensionStats.PushComprehension(node);
        _currentNestedComprehensionStats.CurrentComprehensionStats.VisitingPart = ComprehensionStatsVisitingPart.Element;
        VisitNode(node.Elt);
        VisitNodes(node.Generators);
        _currentNestedComprehensionStats.PopComprehension();
    }

    private void VisitDictComp(DictCompNode node)
    {
        _currentNestedComprehensionStats.PushComprehension(node);
        _currentNestedComprehensionStats.CurrentComprehensionStats.VisitingPart = ComprehensionStatsVisitingPart.Element;
        VisitNode(node.Key);
        VisitNode(node.Value);
        VisitNodes(node.Generators);
        _currentNestedComprehensionStats.PopComprehension();
    }

    private void VisitGeneratorExp(GeneratorExpNode node)
    {
        const string FirstIterVarName = ".0";

        _currentNestedComprehensionStats.PushComprehension(node);

        _currentNestedComprehensionStats.CurrentComprehensionStats.VisitingPart = ComprehensionStatsVisitingPart.GeneratorIter;
        var generators = node.Generators;
        Debug.Assert(generators.Length > 0);
        // first iter is passed as an argument named '.0'
        VisitNode(generators[0].Iter);
        _currentNestedComprehensionStats.CurrentComprehensionStats.VisitingPart = ComprehensionStatsVisitingPart.None;

        var scope = new GeneratorExpVariableScope(node, _currentScopeStats.Scope);
        PushScope(scope);

        AddParameter(FirstIterVarName);

        _currentNestedComprehensionStats.CurrentComprehensionStats.VisitingPart = ComprehensionStatsVisitingPart.Element;
        VisitNode(node.Elt);
        for (int i = 0; i < generators.Length; i++)
        {
            var gen = generators[i];

            _nodesToRoot.Push(gen);

            _currentNestedComprehensionStats.CurrentComprehensionStats.VisitingPart = ComprehensionStatsVisitingPart.GeneratorTarget;
            VisitNode(gen.Target);

            _currentNestedComprehensionStats.CurrentComprehensionStats.VisitingPart = ComprehensionStatsVisitingPart.GeneratorIter;
            if (i is 0)
                VisitName(FirstIterVarName, ExprContextType.Load);
            else
                VisitNode(gen.Iter);

            _currentNestedComprehensionStats.CurrentComprehensionStats.VisitingPart = ComprehensionStatsVisitingPart.GeneratorIfs;
            VisitNodes(gen.Ifs);
            _currentNestedComprehensionStats.CurrentComprehensionStats.VisitingPart = ComprehensionStatsVisitingPart.None;

            _nodesToRoot.Pop();
        }

        PopScope();

        _currentNestedComprehensionStats.PopComprehension();
    }

    private void VisitYield(YieldNode node)
    {
        if (_currentNestedComprehensionStats.IsWithinComprehension)
            throw SyntaxError(PySR.InvalidSyntax_Semantic_YieldInsideComprehension,
                AstUtils.GetExprNodeName(_currentNestedComprehensionStats.CurrentComprehension));

        if (_currentScopeStats.Scope is not CallableVariableScope callableYieldScope
            /* this callable scope is never genexpr because of the first if */)
            throw SyntaxError(PySR.InvalidSyntax_Semantic_YieldOutsideFunction);

        callableYieldScope.IsGenerator = true;
        VisitNullableNode(node.Value);
    }

    private void VisitYieldFrom(YieldFromNode node)
    {
        if (_currentNestedComprehensionStats.IsWithinComprehension)
            throw SyntaxError(PySR.InvalidSyntax_Semantic_YieldFromInsideComprehension,
                AstUtils.GetExprNodeName(_currentNestedComprehensionStats.CurrentComprehension));

        if (_currentScopeStats.Scope is not CallableVariableScope callableYieldFromScope
            /* this callable scope is never genexpr because of the first if */)
            throw SyntaxError(PySR.InvalidSyntax_Semantic_YieldFromOutsideFunction);

        callableYieldFromScope.IsGenerator = true;
        VisitNode(node.Value);
    }

    private void VisitNamedExpr(NamedExprNode node)
    {
        if (_currentScopeStats.Scope is ClassVariableScope && _currentNestedComprehensionStats.IsWithinComprehension)
            throw SyntaxError(PySR.InvalidSyntax_Semantic_NamedExprInComprehensionInClass);

        CheckNamedExprIfWithinComprehension(node.Target.Id);

        VisitNode(node.Target);
        VisitNode(node.Value);
    }

    private void CheckNamedExprIfWithinComprehension(string name)
    {
        if (!_currentNestedComprehensionStats.IsWithinComprehension)
            return;

        CheckNamedExprWithinComprehension(name, _currentNestedComprehensionStats.CurrentComprehensionStats);
        foreach (var stats in _currentNestedComprehensionStats.ComprehensionStatsStack)
        {
            if (stats.Node is null)
                // it is the root stats
                break;

            CheckNamedExprWithinComprehension(name, stats);
        }
    }

    private void CheckNamedExprWithinComprehension(string name, ComprehensionStats stats)
    {
        Debug.Assert(stats.Node is not null);

        var generators = stats.Node switch
        {
            ListCompNode n => n.Generators,
            SetCompNode n => n.Generators,
            DictCompNode n => n.Generators,
            GeneratorExpNode n => n.Generators,
            _ => throw new UnreachableException()
        };

        switch (stats.VisitingPart)
        {
            case ComprehensionStatsVisitingPart.Element:
                {
                    foreach (var generator in generators)
                    {
                        if (ContainsName(generator.Target, name))
                            throw SyntaxError(PySR.InvalidSyntax_Semantic_NamedExprRebindCompIterVar, name);
                    }
                }
                break;

            case ComprehensionStatsVisitingPart.GeneratorTarget:
                throw new UnreachableException("handled by parser");

            case ComprehensionStatsVisitingPart.GeneratorIter:
                throw SyntaxError(PySR.InvalidSyntax_Semantic_NamedExprUsedInCompIter);

            case ComprehensionStatsVisitingPart.GeneratorIfs:
                {
                    AstComprehensionNode? generator = null;
                    int index = -1;
                    foreach (var n in _nodesToRoot)
                    {
                        if (n is not AstComprehensionNode comp)
                            continue;

                        index = generators.IndexOf(comp);
                        if (index >= 0)
                        {
                            generator = comp;
                            break;
                        }
                    }
                    Debug.Assert(generator is not null);
                    Debug.Assert(index >= 0);

                    for (int i = 0; i < generators.Length; i++)
                    {
                        if (i <= index)
                        {
                            if (ContainsName(generators[i].Target, name))
                                throw SyntaxError(PySR.InvalidSyntax_Semantic_NamedExprRebindCompIterVar, name);
                        }
                        else
                        {
                            if (ContainsName(generators[i].Target, name))
                                throw SyntaxError(PySR.InvalidSyntax_Semantic_CompIterVarRebindNamedExpr, name);
                        }
                    }
                }
                break;

            default:
                throw new UnreachableException();
        }

        static bool ContainsName(AstExprNode target, string varName)
        {
            switch (target)
            {
                case NameNode n:
                    return n.Id == varName;

                case TupleNode n:
                    foreach (var elt in n.Elts)
                    {
                        if (ContainsName(elt, varName))
                            return true;
                    }
                    break;

                case ListNode n:
                    foreach (var elt in n.Elts)
                    {
                        if (ContainsName(elt, varName))
                            return true;
                    }
                    break;

                case StarredNode n:
                    return ContainsName(n.Value, varName);
            }
            return false;
        }
    }

    private void VisitSubscript(SubscriptNode node)
    {
        VisitNode(node.Value);
        VisitNode(node.Slice);
    }

    private void VisitSlice(SliceNode node)
    {
        VisitNullableNode(node.Lower);
        VisitNullableNode(node.Upper);
        VisitNullableNode(node.Step);
    }

    private void VisitIfExp(IfExpNode node)
    {
        VisitNode(node.Test);
        VisitNode(node.Body);
        VisitNode(node.OrElse);
    }

    private void VisitLambda(LambdaNode node)
    {
        VisitNullableNodes(node.Args.KwDefaults);
        VisitNodes(node.Args.Defaults);

        var scope = new LambdaVariableScope(node, _currentScopeStats.Scope);
        PushScope(scope);

        VisitNodes(node.Args.PosonlyArgs);
        VisitNodes(node.Args.Args);
        VisitNullableNode(node.Args.VarArg);
        VisitNodes(node.Args.KwonlyArgs);
        VisitNullableNode(node.Args.KwArg);
        VisitNode(node.Body);

        PopScope();
    }

    private void VisitFormattedValue(FormattedValueNode node)
    {
        VisitNode(node.Value);
        VisitNullableNode(node.FormatSpec);
    }

    private void VisitJoinedStr(JoinedStrNode node)
    {
        VisitNodes(node.Values);
    }

    private void VisitBoolOp(BoolOpNode node)
    {
        VisitNodes(node.Values);
    }

    private void VisitStarred(StarredNode node)
    {
        VisitNode(node.Value);
    }
}
