using PySharp.Compilation.Primitives;
using PySharp.Modules.Builtins;
using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Utility;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace PySharp.Compilation.AstNodes;

partial class Reducer
{
    private static bool CanFold(AstExprNode node)
    {
        return node is BinOpNode or UnaryOpNode;
    }

    [return: NotNullIfNotNull(nameof(node))]
    private static AstExprNode? FoldExpr(AstExprNode? node, out bool changed)
    {
        if (node is null || !CanFold(node))
        {
            changed = false;
            return node;
        }

        var reduced = (node switch
        {
            BinOpNode n => FoldBinOp(n),
            UnaryOpNode n => FoldUnaryOp(n),
            _ => throw new UnreachableException()
        }).With(node.MetaInfo);

        changed = !ReferenceEquals(reduced, node);
        return reduced;
    }

    // TODO: consider whether it's necessary to construct a new complete Node for only leftChanged or rightChanged
    private static AstExprNode FoldBinOp(BinOpNode node)
    {
        if (node.Left is not BinOpNode)
        {
            var left = FoldExpr(node.Left, out var leftChanged);
            var right = FoldExpr(node.Right, out var rightChanged);
            var result = InternalFold(node.Operator, left, right, leftChanged, rightChanged);
            if (result is not null)
                return result;
            if (leftChanged || rightChanged)
                return Ast.BinOp(node.Operator, left, right);
            return node;
        }

        {
            var currentNode = node;
            var count = 0;
            while (currentNode.Left is BinOpNode leftBinOpNode)
            {
                count++;
                currentNode = leftBinOpNode;
            }

            using var array = PoolHelper.Rent<BinOpNode>(count);
            var buffer = array.Span;

            currentNode = node;
            while (currentNode.Left is BinOpNode leftBinOpNode)
                buffer[--count] = currentNode = leftBinOpNode;

            var left = FoldExpr(currentNode.Left, out var leftChanged);
            foreach (var leftBinOpNode in buffer)
            {
                var right = FoldExpr(leftBinOpNode.Right, out var rightChanged);
                var result = InternalFold(leftBinOpNode.Operator, left, right, leftChanged, rightChanged);
                if (result is null)
                    return node;
                left = result;
            }

            {
                var right = FoldExpr(node.Right, out var rightChanged);
                var result = InternalFold(node.Operator, left, right, leftChanged, rightChanged);
                if (result is not null)
                    return result;
                if (leftChanged || rightChanged)
                    return Ast.BinOp(node.Operator, left, right);
                return node;
            }
        }

        static AstExprNode? InternalFold(OperatorType op, AstExprNode left, AstExprNode right, bool leftChanged, bool rightChanged)
        {
            if (left is ConstantNode constantLeft && right is ConstantNode constantRight)
            {
                // CPython never folds 'str % x' (str.__mod__ may raise / is not folded); keep it for runtime
                if (op is OperatorType.Mod && constantLeft.Value is PyStrObject)
                    return null;

                var result = PyCore.EvalOperator(PyCallContext.NonContextDependency, op, constantLeft.Value, constantRight.Value);
                if (result.IsSuccessful)
                    return Ast.Constant(result.Value);
            }

            return null;
        }
    }

    private static AstExprNode FoldUnaryOp(UnaryOpNode node)
    {
        var operand = FoldExpr(node.Operand, out var changed);
        if (operand is ConstantNode constantOperand)
        {
            var result = PyCore.EvalOperator(PyCallContext.NonContextDependency, node.Op, constantOperand.Value);
            if (result.IsSuccessful)
                return Ast.Constant(result.Value);
        }
        if (changed)
            return Ast.UnaryOp(node.Op, operand);
        return node;
    }
}
