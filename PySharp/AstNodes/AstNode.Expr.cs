using PySharp.CodeAnalysis;
using PySharp.PyModules.Builtins;
using System.Numerics;

namespace PySharp.AstNodes;

partial class AstNode
{
    public static NameNode Name(string name, CodeMetaInfo? metaInfo)
    {
        return new NameNode(name) { MetaInfo = metaInfo };
    }

    public static ConstantNode Constant(PyObject value, CodeMetaInfo? metaInfo)
    {
        ArgumentNullException.ThrowIfNull(value);

        return new ConstantNode(value) { MetaInfo = metaInfo };
    }
    public static ConstantNode Constant(string value, CodeMetaInfo? metaInfo)
    {
        ArgumentNullException.ThrowIfNull(value);

        return Constant(PyStrObject.FromString(value), metaInfo);
    }
    public static ConstantNode Constant(long value, CodeMetaInfo? metaInfo)
    {
        return Constant(PyIntObject.FromInteger(value), metaInfo);
    }
    public static ConstantNode Constant(BigInteger value, CodeMetaInfo? metaInfo)
    {
        return Constant(PyIntObject.FromInteger(value), metaInfo);
    }
    public static ConstantNode Constant(bool value, CodeMetaInfo? metaInfo)
    {
        return Constant(PyBoolObject.FromBoolean(value), metaInfo);
    }
    public static ConstantNode Constant(double value, CodeMetaInfo? metaInfo)
    {
        return Constant(PyFloatObject.FromDouble(value), metaInfo);
    }

    public static AttributeNode Attribute(AstExprNode value, string attr, CodeMetaInfo? metaInfo)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(attr);

        return new AttributeNode(value, attr) { MetaInfo = metaInfo };
    }

    public static SubscriptNode Subscript(AstExprNode value, AstExprNode slice, CodeMetaInfo? metaInfo)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(slice);

        return new SubscriptNode(value, slice) { MetaInfo = metaInfo };
    }

    public static SliceNode Slice(AstExprNode? lower, AstExprNode? upper, AstExprNode? step)
    {
        return new SliceNode(lower, upper, step);
    }

    public static CallNode Call(AstExprNode func, IEnumerable<AstExprNode> args, IEnumerable<AstKeywordNode> keywords, CodeMetaInfo? metaInfo)
    {
        ArgumentNullException.ThrowIfNull(func);
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(keywords);

        return new CallNode(func, [.. args], [.. keywords]) { MetaInfo = metaInfo };
    }

    public static ListNode List(IEnumerable<AstExprNode> elts, CodeMetaInfo? metaInfo)
    {
        ArgumentNullException.ThrowIfNull(elts);

        return new ListNode([.. elts]) { MetaInfo = metaInfo };
    }
    public static TupleNode Tuple(IEnumerable<AstExprNode> elts, CodeMetaInfo? metaInfo)
    {
        ArgumentNullException.ThrowIfNull(elts);

        return new TupleNode([.. elts]) { MetaInfo = metaInfo };
    }
    public static DictNode Dict(IEnumerable<KeyValuePair<AstExprNode, AstExprNode>> pairs, CodeMetaInfo? metaInfo)
    {
        ArgumentNullException.ThrowIfNull(pairs);

        return new DictNode([.. pairs.Select(static pair => pair.Key)], [.. pairs.Select(static pair => pair.Value)]) { MetaInfo = metaInfo };
    }
    public static SetNode Set(IEnumerable<AstExprNode> elts, CodeMetaInfo? metaInfo)
    {
        ArgumentNullException.ThrowIfNull(elts);

        return new SetNode([.. elts]) { MetaInfo = metaInfo };
    }

    public static BoolOpNode BoolOp(AstBoolOpNode op, IEnumerable<AstExprNode> values, CodeMetaInfo? metaInfo)
    {
        ArgumentNullException.ThrowIfNull(op);
        ArgumentNullException.ThrowIfNull(values);

        return new BoolOpNode(op, [.. values]) { MetaInfo = metaInfo };
    }
    public static BoolOpNode BoolAnd(IEnumerable<AstExprNode> values, CodeMetaInfo? metaInfo)
    {
        return BoolOp(AndNode.Shared, values, metaInfo);
    }
    public static BoolOpNode BoolOr(IEnumerable<AstExprNode> values, CodeMetaInfo? metaInfo)
    {
        return BoolOp(OrNode.Shared, values, metaInfo);
    }

    public static BinOpNode BinOp(AstOperatorNode op, AstExprNode left, AstExprNode right, CodeMetaInfo? metaInfo)
    {
        ArgumentNullException.ThrowIfNull(op);
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        return new BinOpNode(op, left, right) { MetaInfo = metaInfo };
    }

    public static UnaryOpNode UnaryOp(AstUnaryOpNode op, AstExprNode operand, CodeMetaInfo? metaInfo)
    {
        ArgumentNullException.ThrowIfNull(op);
        ArgumentNullException.ThrowIfNull(operand);

        return new UnaryOpNode(op, operand) { MetaInfo = metaInfo };
    }

    public static CompareNode Compare(AstExprNode left, IEnumerable<(AstCmpopNode Op, AstExprNode Comparator)> opsAndComparators, CodeMetaInfo? metaInfo)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(opsAndComparators);

        return new CompareNode(left, [.. opsAndComparators.Select(t => t.Op)], [.. opsAndComparators.Select(t => t.Comparator)]) { MetaInfo = metaInfo };
    }

    public static IfExpNode IfExp(AstExprNode test, AstExprNode body, AstExprNode orElse, CodeMetaInfo? metaInfo)
    {
        ArgumentNullException.ThrowIfNull(test);
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(orElse);

        return new IfExpNode(test, body, orElse) { MetaInfo = metaInfo };
    }

    public static ListCompNode ListComp(AstExprNode elt, IEnumerable<AstComprehensionNode> generators, CodeMetaInfo? metaInfo)
    {
        ArgumentNullException.ThrowIfNull(elt);
        ArgumentNullException.ThrowIfNull(generators);

        return new ListCompNode(elt, [.. generators]) { MetaInfo = metaInfo };
    }
    public static SetCompNode SetComp(AstExprNode elt, IEnumerable<AstComprehensionNode> generators, CodeMetaInfo? metaInfo)
    {
        ArgumentNullException.ThrowIfNull(elt);
        ArgumentNullException.ThrowIfNull(generators);

        return new SetCompNode(elt, [.. generators]) { MetaInfo = metaInfo };
    }
    public static GeneratorExpNode GeneratorExp(AstExprNode elt, IEnumerable<AstComprehensionNode> generators, CodeMetaInfo? metaInfo)
    {
        ArgumentNullException.ThrowIfNull(elt);
        ArgumentNullException.ThrowIfNull(generators);

        return new GeneratorExpNode(elt, [.. generators]) { MetaInfo = metaInfo };
    }
    public static DictCompNode DictComp(AstExprNode key, AstExprNode value, IEnumerable<AstComprehensionNode> generators, CodeMetaInfo? metaInfo)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(generators);

        return new DictCompNode(key, value, [.. generators]) { MetaInfo = metaInfo };
    }

    public static JoinedStrNode JoinedStr(IEnumerable<AstExprNode> values, CodeMetaInfo? metaInfo)
    {
        ArgumentNullException.ThrowIfNull(values);

        return new JoinedStrNode([.. values]) { MetaInfo = metaInfo };
    }
}
