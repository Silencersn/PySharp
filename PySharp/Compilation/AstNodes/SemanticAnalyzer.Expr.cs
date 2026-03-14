using System;
using System.Collections.Generic;
using System.Text;
using PySharp.Compilation.Primitives;
using PySharp.Runtime;
using PySharp.Modules.Builtins;
using System.Collections.Immutable;
using System.Linq;

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

    private void VisitName(NameNode node)
    {
        _currentScopeStats.Scope.AppendVariable(node.Id, node.Ctx);
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
        _currentScopeStats.ComprehensionDepth.Push(node);
        VisitNode(node.Elt);
        VisitNodes(node.Generators);
        _currentScopeStats.ComprehensionDepth.Pop();
    }

    private void VisitSetComp(SetCompNode node)
    {
        _currentScopeStats.ComprehensionDepth.Push(node);
        VisitNode(node.Elt);
        VisitNodes(node.Generators);
        _currentScopeStats.ComprehensionDepth.Pop();
    }

    private void VisitDictComp(DictCompNode node)
    {
        _currentScopeStats.ComprehensionDepth.Push(node);
        VisitNode(node.Key);
        VisitNode(node.Value);
        VisitNodes(node.Generators);
        _currentScopeStats.ComprehensionDepth.Pop();
    }

    private void VisitGeneratorExp(GeneratorExpNode node)
    {
        _currentScopeStats.ComprehensionDepth.Push(node);
        VisitNode(node.Elt);
        VisitNodes(node.Generators);
        _currentScopeStats.ComprehensionDepth.Pop();
    }

    private void VisitYield(YieldNode node)
    {
        if (_currentScopeStats.CurrentComprehension is not null)
            throw SyntaxError(PySR.InvalidSyntax_Semantic_YieldInsideComprehension, AstUtils.GetExprNodeName(_currentScopeStats.CurrentComprehension));

        if (_currentScopeStats.Scope is not CallableVariableScope callableYieldScope)
            throw SyntaxError(PySR.InvalidSyntax_Semantic_YieldOutsideFunction);

        callableYieldScope.HasYield = true;
        VisitNullableNode(node.Value);
    }

    private void VisitYieldFrom(YieldFromNode node)
    {
        if (_currentScopeStats.CurrentComprehension is not null)
            throw SyntaxError(PySR.InvalidSyntax_Semantic_YieldFromInsideComprehension, AstUtils.GetExprNodeName(_currentScopeStats.CurrentComprehension));

        if (_currentScopeStats.Scope is not CallableVariableScope callableYieldFromScope)
            throw SyntaxError(PySR.InvalidSyntax_Semantic_YieldFromOutsideFunction);

        callableYieldFromScope.HasYield = true;
        VisitNode(node.Value);
    }

    private void VisitNamedExpr(NamedExprNode node)
    {
        if (_currentScopeStats is { Scope: ClassVariableScope, ComprehensionDepth.Count: > 0 })
            throw SyntaxError(PySR.InvalidSyntax_Semantic_NamedExprInComprehensionInClass);
        VisitNode(node.Target);
        VisitNode(node.Value);
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
