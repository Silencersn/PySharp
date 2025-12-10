using PySharp.PyModules.Builtins;
using PySharp.PyRuntime.Metadata;
using System.Numerics;

namespace PySharp.AstNodes;

partial class AstNode
{
    public static NameNode Name(string name, MetaInfo? metaInfo)
    {
        return new NameNode(name) { MetaInfo = metaInfo };
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
    public static ConstantNode Constant(long value)
    {
        return Constant(PyIntObject.FromInteger(value));
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

    public static AttributeNode Attribute(AstExprNode value, string attr)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(attr);

        return new AttributeNode(value, attr);
    }

    public static SubscriptNode Subscript(AstExprNode value, AstExprNode slice)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(slice);

        return new SubscriptNode(value, slice);
    }

    public static SliceNode Slice(AstExprNode? lower, AstExprNode? upper, AstExprNode? step)
    {
        return new SliceNode(lower, upper, step);
    }

    public static CallNode Call(AstExprNode func, IEnumerable<AstExprNode> args, IEnumerable<AstKeywordNode> keywords)
    {
        ArgumentNullException.ThrowIfNull(func);
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(keywords);

        return new CallNode(func, [.. args], [.. keywords]);
    }

    public static ListNode List(params IEnumerable<AstExprNode> elts)
    {
        ArgumentNullException.ThrowIfNull(elts);

        return new ListNode([.. elts]);
    }
    public static TupleNode Tuple(params IEnumerable<AstExprNode> elts)
    {
        ArgumentNullException.ThrowIfNull(elts);

        return new TupleNode([.. elts]);
    }
    public static DictNode Dict(params IEnumerable<KeyValuePair<AstExprNode, AstExprNode>> pairs)
    {
        ArgumentNullException.ThrowIfNull(pairs);

        return new DictNode([.. pairs.Select(static pair => pair.Key)], [.. pairs.Select(static pair => pair.Value)]);
    }
    public static SetNode Set(params IEnumerable<AstExprNode> elts)
    {
        ArgumentNullException.ThrowIfNull(elts);

        return new SetNode([.. elts]);
    }

    public static BoolOpNode BoolOp(AstBoolOpNode op, params IEnumerable<AstExprNode> values)
    {
        ArgumentNullException.ThrowIfNull(op);
        ArgumentNullException.ThrowIfNull(values);

        return new BoolOpNode(op, [.. values]);
    }
    public static BoolOpNode BoolAnd(params IEnumerable<AstExprNode> values)
    {
        return BoolOp(AndNode.Shared, values);
    }
    public static BoolOpNode BoolOr(params IEnumerable<AstExprNode> values)
    {
        return BoolOp(OrNode.Shared, values);
    }

    public static BinOpNode BinOp(AstOperatorNode op, AstExprNode left, AstExprNode right)
    {
        ArgumentNullException.ThrowIfNull(op);
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        return new BinOpNode(op, left, right);
    }

    public static UnaryOpNode UnaryOp(AstUnaryOpNode op, AstExprNode operand)
    {
        ArgumentNullException.ThrowIfNull(op);
        ArgumentNullException.ThrowIfNull(operand);

        return new UnaryOpNode(op, operand);
    }

    public static CompareNode Compare(AstExprNode left, params IEnumerable<(AstCmpopNode Op, AstExprNode Comparator)> opsAndComparators)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(opsAndComparators);

        return new CompareNode(left, [.. opsAndComparators.Select(t => t.Op)], [.. opsAndComparators.Select(t => t.Comparator)]);
    }

    public static IfExpNode IfExp(AstExprNode test, AstExprNode body, AstExprNode orElse)
    {
        ArgumentNullException.ThrowIfNull(test);
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(orElse);

        return new IfExpNode(test, body, orElse);
    }

    public static ListCompNode ListComp(AstExprNode elt, params IEnumerable<AstComprehensionNode> generators)
    {
        ArgumentNullException.ThrowIfNull(elt);
        ArgumentNullException.ThrowIfNull(generators);

        return new ListCompNode(elt, [.. generators]);
    }
    public static SetCompNode SetComp(AstExprNode elt, params IEnumerable<AstComprehensionNode> generators)
    {
        ArgumentNullException.ThrowIfNull(elt);
        ArgumentNullException.ThrowIfNull(generators);

        return new SetCompNode(elt, [.. generators]);
    }
    public static GeneratorExpNode GeneratorExp(AstExprNode elt, params IEnumerable<AstComprehensionNode> generators)
    {
        ArgumentNullException.ThrowIfNull(elt);
        ArgumentNullException.ThrowIfNull(generators);

        return new GeneratorExpNode(elt, [.. generators]);
    }
    public static DictCompNode DictComp(AstExprNode key, AstExprNode value, params IEnumerable<AstComprehensionNode> generators)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(generators);

        return new DictCompNode(key, value, [.. generators]);
    }

    public static JoinedStrNode JoinedStr(params IEnumerable<AstExprNode> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        return new JoinedStrNode([.. values]);
    }
}
