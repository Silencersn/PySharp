using PySharp.Compilation.Primitives;
using PySharp.Modules.Builtins;
using PySharp.Runtime;
using PySharp.Runtime.Calls;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace PySharp.Compilation.AstNodes;

partial class Reducer
{
    [return: NotNullIfNotNull(nameof(node))]
    private static AstExprNode? FoldExpr(AstExprNode? node, out bool changed)
    {
        if (node is null)
        {
            changed = false;
            return null; 
        }

        var reduced = (node switch
        {
            BinOpNode n => FoldBinOp(n),
            UnaryOpNode n => FoldUnaryOp(n),
            _ => node
        }).With(node.MetaInfo);

        changed = !ReferenceEquals(reduced, node);
        return reduced;
    }

    private static AstExprNode FoldBinOp(BinOpNode node)
    {
        var left = FoldExpr(node.Left, out var leftChanged);
        var right = FoldExpr(node.Right, out var rightChanged);
        if (left is ConstantNode constantLeft && right is ConstantNode constantRight)
        {
            var result = PyCore.EvalOperator(PyCallContext.NonContextDependency, node.Operator, constantLeft.Value, constantRight.Value);
            if (result.IsSuccessful)
                return Ast.Constant(result.Value);
        }
        if (leftChanged || rightChanged)
            return Ast.BinOp(node.Operator, left, right);
        return node;
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
