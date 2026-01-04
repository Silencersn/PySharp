using PySharp.CodeAnalysis;
using PySharp.PyModules.Builtins;
using System.Numerics;

namespace PySharp.AstNodes;

partial class AstNodeFactory
{
    public static AttributeNode Attribute(AstExprNode value, string attr)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(attr);

        return new AttributeNode(value, attr);
    }

    public static BinOpNode BinOp(AstOperatorNode op, AstExprNode left, AstExprNode right)
    {
        ArgumentNullException.ThrowIfNull(op);
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        return new BinOpNode(op, left, right);
    }
    public static BoolOpNode BoolAnd(IEnumerable<AstExprNode> values)
    {
        return BoolOp(AndNode.Shared, values);
    }

    public static BoolOpNode BoolOp(AstBoolOpNode op, IEnumerable<AstExprNode> values)
    {
        ArgumentNullException.ThrowIfNull(op);
        ArgumentNullException.ThrowIfNull(values);

        return new BoolOpNode(op, values.ToImmutableArray(true));
    }
    public static BoolOpNode BoolOr(IEnumerable<AstExprNode> values)
    {
        return BoolOp(OrNode.Shared, values);
    }

    public static CallNode Call(AstExprNode func, IEnumerable<AstExprNode> args, IEnumerable<AstKeywordNode> keywords)
    {
        ArgumentNullException.ThrowIfNull(func);
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(keywords);

        return new CallNode(func, args.ToImmutableArray(true), keywords.ToImmutableArray(true));
    }

    public static CompareNode Compare(AstExprNode left, IEnumerable<(AstCmpopNode Op, AstExprNode Comparator)> opsAndComparators)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(opsAndComparators);

        return new CompareNode(left, opsAndComparators.Select(t => t.Op).ToImmutableArray(true), opsAndComparators.Select(t => t.Comparator).ToImmutableArray(true));
    }

    public static ConstantNode Constant(PyObject value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return new ConstantNode(value);
    }
    public static ConstantNode Constant(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return Constant(PyStrObject.FromString(value));
    }
    public static ConstantNode Constant(BigInteger value)
    {
        return Constant(PyIntObject.FromInteger(value));
    }
    public static ConstantNode Constant(bool value)
    {
        return Constant(PyBoolObject.FromBoolean(value));
    }
    public static ConstantNode Constant(double value)
    {
        return Constant(PyFloatObject.FromDouble(value));
    }
    public static DictNode Dict(IEnumerable<KeyValuePair<AstExprNode, AstExprNode>> pairs)
    {
        ArgumentNullException.ThrowIfNull(pairs);

        return new DictNode(pairs.Select(static pair => pair.Key).ToImmutableArray(true), pairs.Select(static pair => pair.Value).ToImmutableArray(true));
    }
    public static DictCompNode DictComp(AstExprNode key, AstExprNode value, IEnumerable<AstComprehensionNode> generators)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(generators);

        return new DictCompNode(key, value, generators.ToImmutableArray(true));
    }

    public static FormattedValueNode FormattedValue(AstExprNode value, int conversion, AstExprNode? formatSpec)
    {
        ArgumentNullException.ThrowIfNull(value);

        return new FormattedValueNode(value, conversion, formatSpec);
    }
    public static GeneratorExpNode GeneratorExp(AstExprNode elt, IEnumerable<AstComprehensionNode> generators)
    {
        ArgumentNullException.ThrowIfNull(elt);
        ArgumentNullException.ThrowIfNull(generators);

        return new GeneratorExpNode(elt, generators.ToImmutableArray(true));
    }

    public static IfExpNode IfExp(AstExprNode test, AstExprNode body, AstExprNode orElse)
    {
        ArgumentNullException.ThrowIfNull(test);
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(orElse);

        return new IfExpNode(test, body, orElse);
    }

    public static JoinedStrNode JoinedStr(IEnumerable<AstExprNode> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        return new JoinedStrNode(values.ToImmutableArray(true));
    }

    public static ListNode List(IEnumerable<AstExprNode> elts)
    {
        ArgumentNullException.ThrowIfNull(elts);

        return new ListNode(elts.ToImmutableArray(true));
    }

    public static ListCompNode ListComp(AstExprNode elt, IEnumerable<AstComprehensionNode> generators)
    {
        ArgumentNullException.ThrowIfNull(elt);
        ArgumentNullException.ThrowIfNull(generators);

        return new ListCompNode(elt, generators.ToImmutableArray(true));
    }
    public static NameNode Name(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        return new NameNode(name);
    }
    public static SetNode Set(IEnumerable<AstExprNode> elts)
    {
        ArgumentNullException.ThrowIfNull(elts);

        return new SetNode(elts.ToImmutableArray(true));
    }
    public static SetCompNode SetComp(AstExprNode elt, IEnumerable<AstComprehensionNode> generators)
    {
        ArgumentNullException.ThrowIfNull(elt);
        ArgumentNullException.ThrowIfNull(generators);

        return new SetCompNode(elt, generators.ToImmutableArray(true));
    }

    public static SliceNode Slice(AstExprNode? lower, AstExprNode? upper, AstExprNode? step)
    {
        return new SliceNode(lower, upper, step);
    }

    public static SubscriptNode Subscript(AstExprNode value, AstExprNode slice)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(slice);

        return new SubscriptNode(value, slice);
    }
    public static TupleNode Tuple(IEnumerable<AstExprNode> elts)
    {
        ArgumentNullException.ThrowIfNull(elts);

        return new TupleNode(elts.ToImmutableArray(true));
    }

    public static UnaryOpNode UnaryOp(AstUnaryOpNode op, AstExprNode operand)
    {
        ArgumentNullException.ThrowIfNull(op);
        ArgumentNullException.ThrowIfNull(operand);

        return new UnaryOpNode(op, operand);
    }

    public static YieldNode Yield(AstExprNode? value)
    {
        return new YieldNode(value);
    }

    public static YieldFromNode YieldFrom(AstExprNode value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return new YieldFromNode(value);
    }
}
