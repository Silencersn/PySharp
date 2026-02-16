using PySharp.Compilation.Primitives;
using PySharp.Modules.Builtins;
using PySharp.Runtime;
using PySharp.Runtime.Calls;
using System.Collections.Immutable;
using System.Diagnostics;

namespace PySharp.Compilation.AstNodes;

public abstract class AstExprNode : AstNode;

internal interface IExprContextNode
{
    public ExprContextType Ctx { get; set; }
}

public sealed class NameNode : AstExprNode, IExprContextNode
{
    internal NameNode(string identifier)
    {
        Id = identifier;
    }

    public string Id { get; }
    public ExprContextType Ctx { get; set; } = ExprContextType.Load;

    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        return [];
    }
}

public sealed class ConstantNode : AstExprNode
{
    internal ConstantNode(PyObject value)
    {
        Value = value;
    }

    public PyObject Value { get; }
    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        return [];
    }
}

public sealed class AttributeNode : AstExprNode, IExprContextNode
{
    internal AttributeNode(AstExprNode value, string identifier)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(identifier);

        Value = value;
        Identifier = identifier;
    }

    public AstExprNode Value { get; }
    public string Identifier { get; }
    public ExprContextType Ctx { get; set; } = ExprContextType.Load;
    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        yield return Value;
    }
}

public sealed class SubscriptNode : AstExprNode, IExprContextNode
{
    internal SubscriptNode(AstExprNode value, AstExprNode slice)
    {
        Value = value;
        Slice = slice;
    }

    public AstExprNode Value { get; }
    public AstExprNode Slice { get; }
    public ExprContextType Ctx { get; set; } = ExprContextType.Load;

    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        yield return Value;
        yield return Slice;
    }
}

public sealed class SliceNode : AstExprNode
{
    internal SliceNode(AstExprNode? lower, AstExprNode? upper, AstExprNode? step)
    {
        Lower = lower;
        Upper = upper;
        Step = step;
    }

    public AstExprNode? Lower { get; }
    public AstExprNode? Upper { get; }
    public AstExprNode? Step { get; }
    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        if (Lower is not null) yield return Lower;
        if (Upper is not null) yield return Upper;
        if (Step is not null) yield return Step;
    }
}

public sealed class CallNode : AstExprNode
{
    internal CallNode(AstExprNode func, ImmutableArray<AstExprNode> args, ImmutableArray<AstKeywordNode> keywords)
    {
        Func = func;
        Args = args;
        Keywords = keywords;
    }

    public AstExprNode Func { get; }
    public ImmutableArray<AstExprNode> Args { get; }
    public ImmutableArray<AstKeywordNode> Keywords { get; }
    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        yield return Func;
        foreach (var arg in Args) yield return arg;
        foreach (var kw in Keywords) yield return kw;
    }
}

public sealed class ListNode : AstExprNode, IExprContextNode
{
    internal ListNode(ImmutableArray<AstExprNode> elts)
    {
        Elts = elts;
        Ctx = ExprContextType.Load;
    }

    public ImmutableArray<AstExprNode> Elts { get; }
    public ExprContextType Ctx
    {
        get => field;
        set
        {
            field = value;
            foreach (var elt in Elts)
            {
                if (elt is IExprContextNode node)
                    node.Ctx = value;
            }
        }
    }
    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        foreach (var elt in Elts) yield return elt;
    }
}

public sealed class TupleNode : AstExprNode, IExprContextNode
{
    internal TupleNode(ImmutableArray<AstExprNode> elts)
    {
        Elts = elts;
        Ctx = ExprContextType.Load;
    }

    public ImmutableArray<AstExprNode> Elts { get; }
    public ExprContextType Ctx
    {
        get => field;
        set
        {
            field = value;
            foreach (var elt in Elts)
            {
                if (elt is IExprContextNode node)
                    node.Ctx = value;
            }
        }
    }
    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        foreach (var elt in Elts) yield return elt;
    }
}

public sealed class DictNode : AstExprNode
{
    internal DictNode(ImmutableArray<AstExprNode?> keys, ImmutableArray<AstExprNode> values)
    {
        Debug.Assert(keys.Length == values.Length);

        Keys = keys;
        Values = values;
    }

    public ImmutableArray<AstExprNode?> Keys { get; }
    public ImmutableArray<AstExprNode> Values { get; }
    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        foreach (var k in Keys)
        {
            if (k is not null)
                yield return k;
        }

        foreach (var v in Values)
            yield return v;
    }
}

public sealed class SetNode : AstExprNode
{
    internal SetNode(ImmutableArray<AstExprNode> elts)
    {
        Elts = elts;
    }
    public ImmutableArray<AstExprNode> Elts { get; }
    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        foreach (var elt in Elts) yield return elt;
    }
}

public sealed class BoolOpNode : AstExprNode
{
    internal BoolOpNode(BoolOpType op, ImmutableArray<AstExprNode> values)
    {
        Op = op;
        Values = values;
    }

    public BoolOpType Op { get; }
    public ImmutableArray<AstExprNode> Values { get; }
    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        foreach (var v in Values)
            yield return v;
    }

    public static (bool Result, PyResult Value) GetBoolAndValue(PyCallContext context, IEnumerable<PyObject> values)
    {
        PyObject lastValue = null!;

        foreach (var value in values)
        {
            var result = PySpecialMethods.Bool(context, value);
            if (result.IsError)
                return (false, result);

            if (!result.Value.BoolValue)
                return (false, value);

            lastValue = value;
        }

        return (true, lastValue);
    }

    public static (bool Result, PyResult Value) GetBoolOrValue(PyCallContext context, IEnumerable<PyObject> values)
    {
        PyObject lastValue = null!;

        foreach (var value in values)
        {
            var result = PySpecialMethods.Bool(context, value);
            if (result.IsError)
                return (false, result);

            if (result.Value.BoolValue)
                return (true, value);

            lastValue = value;
        }

        return (false, lastValue);
    }
}

public sealed class BinOpNode : AstExprNode
{
    internal BinOpNode(OperatorType op, AstExprNode left, AstExprNode right)
    {
        Left = left;
        Right = right;
        Operator = op;
    }

    public OperatorType Operator { get; }
    public AstExprNode Left { get; }
    public AstExprNode Right { get; }

    internal static PyResult EvalOperator(PyCallContext context, OperatorType op, PyObject left, PyObject right)
    {
        return op switch
        {
            OperatorType.Add => PyOperators.Add(context, left, right),
            OperatorType.Sub => PyOperators.Sub(context, left, right),
            OperatorType.Mult => PyOperators.Mult(context, left, right),
            OperatorType.MatMult => throw new NotImplementedException(), // PyOperators.MatMult(context, left, right),
            OperatorType.Div => PyOperators.TrueDiv(context, left, right),
            OperatorType.Mod => PyOperators.Mod(context, left, right),
            OperatorType.Pow => PyOperators.Pow(context, left, right, PyNoneObject.None),
            OperatorType.LShift => PyOperators.LShift(context, left, right),
            OperatorType.RShift => PyOperators.RShift(context, left, right),
            OperatorType.BitOr => PyOperators.BitOr(context, left, right),
            OperatorType.BitXor => PyOperators.BitXor(context, left, right),
            OperatorType.BitAnd => PyOperators.BitAnd(context, left, right),
            OperatorType.FloorDiv => PyOperators.FloorDiv(context, left, right),
            _ => throw new UnreachableException(),
        };
    }

    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        yield return Left;
        yield return Right;
    }
}

public sealed class UnaryOpNode : AstExprNode
{
    internal UnaryOpNode(UnaryOpType op, AstExprNode operand)
    {
        Op = op;
        Operand = operand;
    }

    public UnaryOpType Op { get; }
    public AstExprNode Operand { get; }

    internal static PyResult EvalOperator(PyCallContext context, UnaryOpType op, PyObject value)
    {
        return op switch
        {
            UnaryOpType.Invert => PyOperators.Invert(context, value),
            UnaryOpType.Not => PyOperators.Not(context, value),
            UnaryOpType.UAdd => PyOperators.UAdd(context, value),
            UnaryOpType.USub => PyOperators.USub(context, value),
            _ => throw new UnreachableException(),
        };
    }

    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        yield return Operand;
    }
}

public sealed class CompareNode : AstExprNode
{
    internal CompareNode(AstExprNode left, ImmutableArray<CmpopType> ops, ImmutableArray<AstExprNode> comparators)
    {
        Left = left;
        Ops = ops;
        Comparators = comparators;
    }

    public AstExprNode Left { get; }
    public ImmutableArray<CmpopType> Ops { get; }
    public ImmutableArray<AstExprNode> Comparators { get; }

    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        yield return Left;
        foreach (var cmp in Comparators) yield return cmp;
    }

    internal static PyResult EvalOperator(PyCallContext context, CmpopType op, PyObject left, PyObject right)
    {
        return op switch
        {
            CmpopType.Eq => PyOperators.Eq(context, left, right),
            CmpopType.NotEq => PyOperators.NotEq(context, left, right),
            CmpopType.Lt => PyOperators.Lt(context, left, right),
            CmpopType.LtE => PyOperators.LtE(context, left, right),
            CmpopType.Gt => PyOperators.Gt(context, left, right),
            CmpopType.GtE => PyOperators.GtE(context, left, right),
            CmpopType.Is => PyOperators.Is(left, right),
            CmpopType.IsNot => PyOperators.IsNot(left, right),
            CmpopType.In => PyOperators.In(context, left, right),
            CmpopType.NotIn => PyOperators.NotIn(context, left, right),
            _ => throw new UnreachableException(),
        };
    }
}

public sealed class IfExpNode : AstExprNode
{
    internal IfExpNode(AstExprNode test, AstExprNode body, AstExprNode orElse)
    {
        Test = test;
        Body = body;
        OrElse = orElse;
    }

    public AstExprNode Test { get; }
    public AstExprNode Body { get; }
    public AstExprNode OrElse { get; }

    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        yield return Test;
        yield return Body;
        yield return OrElse;
    }
}

public sealed class ListCompNode : AstExprNode
{
    internal ListCompNode(AstExprNode elt, ImmutableArray<AstComprehensionNode> generators)
    {
        Elt = elt;
        Generators = generators;
    }

    public AstExprNode Elt { get; }
    public ImmutableArray<AstComprehensionNode> Generators { get; }
    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        yield return Elt;
        foreach (var gen in Generators) yield return gen;
    }
}

public sealed class SetCompNode : AstExprNode
{
    internal SetCompNode(AstExprNode elt, ImmutableArray<AstComprehensionNode> generators)
    {
        Elt = elt;
        Generators = generators;
    }

    public AstExprNode Elt { get; }
    public ImmutableArray<AstComprehensionNode> Generators { get; }
    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        yield return Elt;
        foreach (var gen in Generators) yield return gen;
    }
}

public sealed class DictCompNode : AstExprNode
{
    internal DictCompNode(AstExprNode key, AstExprNode value, ImmutableArray<AstComprehensionNode> generators)
    {
        Key = key;
        Value = value;
        Generators = generators;
    }

    public AstExprNode Key { get; }
    public AstExprNode Value { get; }
    public ImmutableArray<AstComprehensionNode> Generators { get; }
    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        yield return Key;
        yield return Value;
        foreach (var gen in Generators) yield return gen;
    }
}

public sealed class GeneratorExpNode : AstExprNode
{
    internal GeneratorExpNode(AstExprNode elt, ImmutableArray<AstComprehensionNode> generators)
    {
        Elt = elt;
        Generators = generators;
    }

    public AstExprNode Elt { get; }
    public ImmutableArray<AstComprehensionNode> Generators { get; }
    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        yield return Elt;
        foreach (var gen in Generators) yield return gen;
    }
}

public sealed class LambdaNode : AstExprNode, IScopedSubNodesProvider
{
    internal LambdaNode(AstArgumentsNode args, AstExprNode body)
    {
        Args = args;
        Body = body;
    }

    public AstArgumentsNode Args { get; }
    public AstExprNode Body { get; }

    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        yield return Args;
        yield return Body;
    }

    IEnumerable<AstNode> IScopedSubNodesProvider.EnumerateSubNodesOuterScope()
    {
        foreach (var d in Args.KwDefaults)
            if (d is not null)
                yield return d;

        foreach (var d in Args.Defaults)
            yield return d;
    }

    IEnumerable<AstNode> IScopedSubNodesProvider.EnumerateSubNodesInnerScope()
    {
        foreach (var n in Args.PosonlyArgs)
            yield return n;

        foreach (var n in Args.Args)
            yield return n;

        if (Args.VarArg is not null)
            yield return Args.VarArg;

        foreach (var n in Args.KwonlyArgs)
            yield return n;

        if (Args.KwArg is not null)
            yield return Args.KwArg;

        yield return Body;
    }
}

public sealed class JoinedStrNode : AstExprNode
{
    internal JoinedStrNode(ImmutableArray<AstExprNode> values)
    {
        Values = values;
    }

    public ImmutableArray<AstExprNode> Values { get; }

    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        foreach (var v in Values) yield return v;
    }
}

public sealed class FormattedValueNode : AstExprNode
{
    internal FormattedValueNode(AstExprNode value, int conversion, AstExprNode? formatSpec)
    {
        Value = value;
        Conversion = conversion;
        FormatSpec = formatSpec;
    }

    public AstExprNode Value { get; }
    public int Conversion { get; }
    public AstExprNode? FormatSpec { get; }
    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        yield return Value;
        if (FormatSpec is not null) yield return FormatSpec;
    }
}

public sealed class YieldNode : AstExprNode
{
    internal YieldNode(AstExprNode? value)
    {
        Value = value;
    }
    public AstExprNode? Value { get; }
    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        if (Value is not null)
            yield return Value;
    }
}

public sealed class YieldFromNode : AstExprNode
{
    internal YieldFromNode(AstExprNode value)
    {
        Value = value;
    }
    public AstExprNode Value { get; }
    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        yield return Value;
    }
}


public sealed class StarredNode : AstExprNode, IExprContextNode
{
    internal StarredNode(AstExprNode value)
    {
        Value = value;
        Ctx = ExprContextType.Load;
    }

    public AstExprNode Value { get; }
    public ExprContextType Ctx
    {
        get => field;
        set
        {
            field = value;
            if (Value is IExprContextNode node)
                node.Ctx = value;
        }
    }

    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        yield return Value;
    }
}

public sealed class NamedExprNode : AstExprNode
{
    internal NamedExprNode(NameNode target, AstExprNode value)
    {
        Target = target;
        Value = value;
    }

    public NameNode Target { get; }
    public AstExprNode Value { get; }

    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        yield return Target;
        yield return Value;
    }
}