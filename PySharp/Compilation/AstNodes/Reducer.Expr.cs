using PySharp.Runtime;
using PySharp.Runtime.Calls;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace PySharp.Compilation.AstNodes;

partial class Reducer
{
    [return: NotNullIfNotNull(nameof(node))]
    private static AstExprNode? ReduceExpr(AstExprNode? node, out bool changed)
    {
        if (node is null)
        {
            changed = false;
            return null; 
        }

        if (node._reduced is not null)
        {
            changed = !ReferenceEquals(node._reduced, node);
            return node._reduced;
        }

        var reduced = (node switch
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
        changed = !ReferenceEquals(reduced, node);
        reduced._reduced = reduced;
        return reduced;
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
        var left = ReduceExpr(node.Left, out var leftChanged);
        var right = ReduceExpr(node.Right, out var rightChanged);
        if (left is ConstantNode constntLeft && right is ConstantNode constantRight)
        {
            var result = PyCore.EvalOperator(PyCallContext.NonContextDependency, node.Operator, constntLeft.Value, constantRight.Value);
            if (result.IsSuccessful)
                return Ast.Constant(result.Value);
        }
        if (leftChanged || rightChanged)
            return Ast.BinOp(node.Operator, left, right);
        return node;
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
