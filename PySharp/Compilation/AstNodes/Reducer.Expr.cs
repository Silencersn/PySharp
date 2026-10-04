using PySharp.Compilation.Primitives;
using PySharp.Modules.Builtins;
using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Utility;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;

namespace PySharp.Compilation.AstNodes;

partial class Reducer
{
    private static bool CanFold(AstExprNode node)
    {
        return node is BinOpNode or UnaryOpNode or TupleNode;
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
            TupleNode n => FoldTuple(n),
            _ => throw new UnreachableException()
        }).With(node.MetaInfo);

        changed = !ReferenceEquals(reduced, node);
        return reduced;
    }

    /// <summary>
    /// A tuple display whose elements are all constant becomes one constant,
    /// the LOAD_CONST (1, 2) CPython emits (codegen.c codegen_tuple /
    /// flowgraph.c fold_tuple_of_constants). A store target is not an
    /// expression, and a starred element is never a constant display, so
    /// neither is folded. Folding an element without the tuple itself (a mixed
    /// display) is left to the per-element fold in the emitter.
    /// </summary>
    private static AstExprNode FoldTuple(TupleNode node)
    {
        if (node.Ctx is not ExprContextType.Load)
            return node;

        var elts = node.Elts;
        var values = new PyObject[elts.Length];
        for (int i = 0; i < elts.Length; i++)
        {
            if (elts[i] is StarredNode || FoldExpr(elts[i], out _) is not ConstantNode constant)
                return node;

            values[i] = constant.Value;
        }

        return Ast.Constant(PyTupleObject.CreateTuple(values));
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
                if (!SafeToFold(op, constantLeft.Value, constantRight.Value))
                    return null;

                var result = PyCore.EvalOperator(PyCallContext.NonContextDependency, op, constantLeft.Value, constantRight.Value);
                if (result.IsSuccessful)
                    return Ast.Constant(result.Value);
            }

            return null;
        }
    }

    // CPython's constant folder (flowgraph.c) refuses folds that would
    // eagerly allocate a huge string, sequence or int during compilation,
    // leaving them to runtime instead.
    private const int MaxIntSize = 128;         // bits
    private const int MaxCollectionSize = 256;  // items
    private const int MaxStrSize = 4096;        // characters
    private const int MaxTotalItems = 1024;     // including nested collections

    // eval_const_binop: which binary folds are attempted at all. A false
    // result keeps the operation at runtime, exactly like the
    // const_folding_safe_* guards returning NULL in CPython.
    private static bool SafeToFold(OperatorType op, PyObject left, PyObject right) => op switch
    {
        OperatorType.Mult => SafeMultiply(left, right),
        OperatorType.Pow => SafePower(left, right),
        OperatorType.LShift => SafeLShift(left, right),
        // %-formatting may raise (str.__mod__ / bytes.__mod__), so
        // 'str % x' and 'bytes % x' never fold (const_folding_safe_mod)
        OperatorType.Mod => left is not (PyStrObject or PyBytesObject),
        _ => true,
    };

    // const_folding_safe_multiply
    private static bool SafeMultiply(PyObject left, PyObject right)
    {
        if (left is PyIntObject leftInt && right is PyIntObject rightInt)
        {
            // int * int: refuse when the product would exceed MaxIntSize bits
            return leftInt.Value.IsZero || rightInt.Value.IsZero
                || BigInteger.Abs(leftInt.Value).GetBitLength() + BigInteger.Abs(rightInt.Value).GetBitLength() <= MaxIntSize;
        }

        if (left is PyIntObject count && right is PyTupleObject tuple)
        {
            if (tuple.Count is 0)
                return true;
            if (!RepeatCountWithin(count, MaxCollectionSize / tuple.Count, out long n))
                return false;
            return n is 0 || TupleComplexity(tuple, MaxTotalItems / n) >= 0;
        }

        if (left is PyIntObject seqCount && right is PyStrObject or PyBytesObject)
        {
            int size = right is PyStrObject str ? str.PyLength : ((PyBytesObject)right).Length;
            return size is 0 || RepeatCountWithin(seqCount, MaxStrSize / size, out _);
        }

        if (right is PyIntObject && left is PyTupleObject or PyStrObject or PyBytesObject)
            return SafeMultiply(right, left);

        return true;
    }

    // PyLong_AsLong followed by the "n < 0 || n > limit" refusal: a count
    // outside long range (CPython's overflow reads as -1) never folds.
    private static bool RepeatCountWithin(PyIntObject count, long limit, out long n)
    {
        var value = count.Value;
        if (value.Sign < 0 || value > long.MaxValue)
        {
            n = 0;
            return false;
        }
        n = (long)value;
        return n <= limit;
    }

    // const_folding_check_complexity: the budget left after the (possibly
    // nested) tuple's total item count; negative means over budget.
    private static long TupleComplexity(PyObject obj, long limit)
    {
        if (obj is PyTupleObject tuple)
        {
            limit -= tuple.Count;
            for (int i = 0; limit >= 0 && i < tuple.Count; i++)
            {
                limit = TupleComplexity(tuple[i], limit);
                if (limit < 0)
                    return limit;
            }
        }
        return limit;
    }

    // const_folding_safe_power
    private static bool SafePower(PyObject left, PyObject right)
    {
        // int ** int with a positive exponent: refuse when the power
        // would exceed MaxIntSize bits (bits(v) * exponent > MaxIntSize)
        if (left is PyIntObject leftInt && right is PyIntObject rightInt
            && !leftInt.Value.IsZero && rightInt.Value.Sign > 0)
            return BigInteger.Abs(leftInt.Value).GetBitLength() <= MaxIntSize / rightInt.Value;
        return true;
    }

    // const_folding_safe_lshift
    private static bool SafeLShift(PyObject left, PyObject right)
    {
        if (left is PyIntObject leftInt && right is PyIntObject rightInt
            && !leftInt.Value.IsZero && !rightInt.Value.IsZero)
        {
            // refuse when the shift would exceed MaxIntSize bits; a
            // negative shift raises at runtime and never folds
            var shift = rightInt.Value;
            return shift.Sign > 0
                && shift <= MaxIntSize
                && BigInteger.Abs(leftInt.Value).GetBitLength() <= MaxIntSize - (long)shift;
        }
        return true;
    }

    private static AstExprNode FoldUnaryOp(UnaryOpNode node)
    {
        var operand = FoldExpr(node.Operand, out var changed);
        if (operand is ConstantNode constantOperand)
        {
            // CPython refuses to fold '~bool' so the deprecation warning
            // stays at runtime (Python/flowgraph.c eval_const_unaryop
            // returns NULL for PyBool_Check under UNARY_INVERT); folding here
            // would swallow it, since the fold runs on a context without a
            // warning sink.
            if (node.Op is UnaryOpType.Invert && constantOperand.Value is PyBoolObject)
                return changed ? Ast.UnaryOp(node.Op, operand) : node;

            var result = PyCore.EvalOperator(PyCallContext.NonContextDependency, node.Op, constantOperand.Value);
            if (result.IsSuccessful)
                return Ast.Constant(result.Value);
        }
        if (changed)
            return Ast.UnaryOp(node.Op, operand);
        return node;
    }
}
