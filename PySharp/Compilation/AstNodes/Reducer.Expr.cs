using PySharp.Runtime;
using PySharp.Runtime.Calls;
using System.Diagnostics;

namespace PySharp.Compilation.AstNodes;

internal sealed partial class Reducer
{
    private static AstExprNode ReduceExpr(AstExprNode node)
    {
        return (node switch
        {
            NameNode n => ReduceName(n),
            ConstantNode n => ReduceConstant(n),
            AttributeNode n => ReduceAttribute(n),
            SubscriptNode n => ReduceSubscript(n),
            SliceNode n => ReduceSlice(n),
            CallNode n => ReduceCall(n),
            ListNode n => ReduceList(n),
            TupleNode n => ReduceTuple(n),
            DictNode n => ReduceDict(n),
            SetNode n => ReduceSet(n),
            BoolOpNode n => ReduceBoolOp(n),
            BinOpNode n => ReduceBinOp(n),
            UnaryOpNode n => ReduceUnaryOp(n),
            CompareNode n => ReduceCompare(n),
            IfExpNode n => ReduceIfExp(n),
            ListCompNode n => ReduceListComp(n),
            SetCompNode n => ReduceSetComp(n),
            DictCompNode n => ReduceDictComp(n),
            GeneratorExpNode n => ReduceGeneratorExp(n),
            LambdaNode n => ReduceLambda(n),
            JoinedStrNode n => ReduceJoinedStr(n),
            FormattedValueNode n => ReduceFormattedValue(n),
            YieldNode n => ReduceYield(n),
            YieldFromNode n => ReduceYieldFrom(n),
            StarredNode n => ReduceStarred(n),
            NamedExprNode n => ReduceNamedExpr(n),
            _ => throw new UnreachableException(),
        }).With(node.MetaInfo);
    }


    private static AstExprNode ReduceName(NameNode node)
    {
        return node;
    }
    private static AstExprNode ReduceConstant(ConstantNode node)
    {
        return node;
    }
    private static AstExprNode ReduceAttribute(AttributeNode node)
    {
        return node;
    }
    private static AstExprNode ReduceSubscript(SubscriptNode node)
    {
        return node;
    }
    private static AstExprNode ReduceSlice(SliceNode node)
    {
        return node;
    }
    private static AstExprNode ReduceCall(CallNode node)
    {
        return node;
    }
    private static AstExprNode ReduceList(ListNode node)
    {
        return node;
    }
    private static AstExprNode ReduceTuple(TupleNode node)
    {
        return node;
    }
    private static AstExprNode ReduceDict(DictNode node)
    {
        return node;
    }
    private static AstExprNode ReduceSet(SetNode node)
    {
        return node;
    }
    private static AstExprNode ReduceBoolOp(BoolOpNode node)
    {
        return node;
    }
    private static AstExprNode ReduceBinOp(BinOpNode node)
    {
        var left = ReduceExpr(node.Left);
        var right = ReduceExpr(node.Right);
        if (left is ConstantNode constntLeft && right is ConstantNode constantRight)
        {
            var result = PyCore.EvalOperator(PyCallContext.NonContextDependency, node.Operator, constntLeft.Value, constantRight.Value);
            if (result.IsSuccessful)
                return Ast.Constant(result.Value);
        }
        if (ReferenceEquals(left, node.Left) && ReferenceEquals(right, node.Right))
            return node;

        return Ast.BinOp(node.Operator, left, right);
    }
    private static AstExprNode ReduceUnaryOp(UnaryOpNode node)
    {
        return node;
    }
    private static AstExprNode ReduceCompare(CompareNode node)
    {
        return node;
    }
    private static AstExprNode ReduceIfExp(IfExpNode node)
    {
        return node;
    }
    private static AstExprNode ReduceListComp(ListCompNode node)
    {
        return node;
    }
    private static AstExprNode ReduceSetComp(SetCompNode node)
    {
        return node;
    }
    private static AstExprNode ReduceDictComp(DictCompNode node)
    {
        return node;
    }
    private static AstExprNode ReduceGeneratorExp(GeneratorExpNode node)
    {
        return node;
    }
    private static AstExprNode ReduceLambda(LambdaNode node)
    {
        return node;
    }
    private static AstExprNode ReduceJoinedStr(JoinedStrNode node)
    {
        return node;
    }
    private static AstExprNode ReduceFormattedValue(FormattedValueNode node)
    {
        return node;
    }
    private static AstExprNode ReduceYield(YieldNode node)
    {
        return node;
    }
    private static AstExprNode ReduceYieldFrom(YieldFromNode node)
    {
        return node;
    }
    private static AstExprNode ReduceStarred(StarredNode node)
    {
        return node;
    }
    private static AstExprNode ReduceNamedExpr(NamedExprNode node)
    {
        return node;
    }
}
