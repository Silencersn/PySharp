using PySharp.PyModules;
using PySharp.PyModules.Builtins;
using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;
using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;

namespace PySharp.AstNodes;

public abstract class AstExprNode : AstNode
{
    public PyObject GetExprValue(PyCallContext context, PyFrame frame)
    {
        if (this is IAstExprNodeNoSelfPythonException)
            return ExecuteExpr(context, frame);

        using var withMetaInfo = new MetaInfoProviderSetter(frame, this);
        return ExecuteExpr(context, frame);
    }

    public abstract PyObject ExecuteExpr(PyCallContext context, PyFrame frame);
}

internal interface IAstExprNodeBool
{
    public (bool Result, PyObject Value) GetExprValueWithResult(PyCallContext context, PyFrame frame);
}

/// <summary>
/// All types that inherit this interface will not raise python exceptions themselves (exceptions may be thrown by child nodes)
/// </summary>
internal interface IAstExprNodeNoSelfPythonException;

public enum ExprContextType
{
    Unknown = 0,

    Load,
    Store,
    Del
}

public enum BoolOpType
{
    And,
    Or
}

public enum OperatorType
{
    Add,
    Sub,
    Mult,
    MatMult,
    Div,
    Mod,
    Pow,
    LShift,
    RShift,
    BitOr,
    BitXor,
    BitAnd,
    FloorDiv
}

public enum UnaryOpType
{
    Invert,
    Not,
    UAdd,
    USub
}

public enum CmpopType
{
    Eq,
    NotEq,
    Lt,
    LtE,
    Gt,
    GtE,
    Is,
    IsNot,
    In,
    NotIn
}

internal interface IExprContextNode
{
    public ExprContextType Ctx { get; set; }
}

internal interface ITargetNode
{
    void SetValue(PyCallContext context, PyObject value, PyFrame frame);
    void DeleteValue(PyCallContext context, PyFrame frame);
}

public sealed class NameNode : AstExprNode, IExprContextNode, ITargetNode
{
    internal NameNode(string identifier)
    {
        Id = identifier;
    }

    public string Id { get; }
    public ExprContextType Ctx { get; set; } = ExprContextType.Load;

    // TODO: FastIndex can be DANGEROUS if this node is used in different ast tree!!!
    internal int FastIndex { get; set; } = -1;

    public override PyObject ExecuteExpr(PyCallContext context, PyFrame frame)
    {
        if (FastIndex is not -1)
            return frame.LoadFast(FastIndex).PyUnwrap(context);

        return frame.GetVariable(Id).PyUnwrap(context);
    }

    void ITargetNode.DeleteValue(PyCallContext context, PyFrame frame)
    {
        if (FastIndex is not -1)
            frame.DeleteFast(FastIndex).PyUnwrap(context);
        else
            frame.DeleteVariable(Id).PyUnwrap(context);
    }

    void ITargetNode.SetValue(PyCallContext context, PyObject value, PyFrame frame)
    {
        if (FastIndex is not -1)
            frame.StoreFast(FastIndex, value).PyUnwrap(context);
        else
            frame.SetVariable(Id, value).PyUnwrap(context);
    }

    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        return [];
    }
}

public sealed class ConstantNode : AstExprNode, IAstExprNodeNoSelfPythonException
{
    internal ConstantNode(PyObject value)
    {
        Value = value;
    }

    public PyObject Value { get; }

    public override PyObject ExecuteExpr(PyCallContext context, PyFrame frame)
    {
        return Value;
    }

    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        return [];
    }
}

public sealed class AttributeNode : AstExprNode, IExprContextNode, ITargetNode
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

    public override PyObject ExecuteExpr(PyCallContext context, PyFrame frame)
    {
        var value = Value.GetExprValue(context, frame);
        var attr = PyOperators.GetAttr(context, value, Identifier);
        return attr.PyUnwrap(context);
    }

    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        yield return Value;
    }

    void ITargetNode.SetValue(PyCallContext context, PyObject value, PyFrame frame)
    {
        var obj = Value.GetExprValue(context, frame);
        PyOperators.SetAttr(context, obj, Identifier, value).PyUnwrap(context);
    }

    void ITargetNode.DeleteValue(PyCallContext context, PyFrame frame)
    {
        var obj = Value.GetExprValue(context, frame);
        PyOperators.DelAttr(context, obj, Identifier).PyUnwrap(context);
    }
}

public sealed class SubscriptNode : AstExprNode, IExprContextNode, ITargetNode
{
    internal SubscriptNode(AstExprNode value, AstExprNode slice)
    {
        Value = value;
        Slice = slice;
    }

    public AstExprNode Value { get; }
    public AstExprNode Slice { get; }
    public ExprContextType Ctx { get; set; } = ExprContextType.Load;

    public override PyObject ExecuteExpr(PyCallContext context, PyFrame frame)
    {
        return GetItem(context, frame);
    }

    public PyObject GetItem(PyCallContext context, PyFrame frame)
    {
        var value = Value.GetExprValue(context, frame);
        var slice = Slice.GetExprValue(context, frame);
        return PySpecialMethods.GetItem(context, value, slice).PyUnwrap(context);
    }

    public PyObject SetItem(PyCallContext context, PyFrame frame, PyObject obj)
    {
        var value = Value.GetExprValue(context, frame);
        var slice = Slice.GetExprValue(context, frame);
        return PySpecialMethods.SetItem(context, value, slice, obj).PyUnwrap(context);
    }

    public PyObject DelItem(PyFrame frame, PyCallContext context)
    {
        var value = Value.GetExprValue(context, frame);
        var slice = Slice.GetExprValue(context, frame);
        return PySpecialMethods.DelItem(context, value, slice).PyUnwrap(context);
    }

    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        yield return Value;
        yield return Slice;
    }

    void ITargetNode.SetValue(PyCallContext context, PyObject value, PyFrame frame)
    {
        SetItem(context, frame, value);
    }

    void ITargetNode.DeleteValue(PyCallContext context, PyFrame frame)
    {
        DelItem(frame, context);
    }
}

public sealed class SliceNode : AstExprNode, IAstExprNodeNoSelfPythonException
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

    public override PyObject ExecuteExpr(PyCallContext context, PyFrame frame)
    {
        return new PySliceObject(
            Lower?.GetExprValue(context, frame) ?? PyNoneObject.None,
            Upper?.GetExprValue(context, frame) ?? PyNoneObject.None,
            Step?.GetExprValue(context, frame) ?? PyNoneObject.None
        );
    }

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

        if (args.Length > 0 && keywords.Length > 0)
            _argsType = CallArgumentsType.ArgsKwargs;
        else if (args.Length > 0)
            _argsType = CallArgumentsType.ArgsOnly;
        else if (keywords.Length > 0)
            _argsType = CallArgumentsType.KwargsOnly;
        else
            _argsType = CallArgumentsType.NoArgsOrKwargs;
    }

    private enum CallArgumentsType
    {
        Unknown = 0,

        NoArgsOrKwargs,
        ArgsOnly,
        KwargsOnly,
        ArgsKwargs
    }
    private readonly CallArgumentsType _argsType;

    public AstExprNode Func { get; }
    public ImmutableArray<AstExprNode> Args { get; }
    public ImmutableArray<AstKeywordNode> Keywords { get; }

    public override PyObject ExecuteExpr(PyCallContext context, PyFrame frame)
    {
        var func = Func.GetExprValue(context, frame);

        IReadOnlyList<PyObject> args;
        IReadOnlyDictionary<string, PyObject> kwargs;

        switch (_argsType)
        {
            case CallArgumentsType.ArgsKwargs:
                args = AstUtils.EvalPyObjects(context, frame, Args);
                kwargs = AstUtils.EvalKeywords(context, frame, Keywords);
                break;

            case CallArgumentsType.NoArgsOrKwargs:
                args = [];
                kwargs = FrozenDictionary<string, PyObject>.Empty;
                break;

            case CallArgumentsType.ArgsOnly:
                args = AstUtils.EvalPyObjects(context, frame, Args);
                kwargs = FrozenDictionary<string, PyObject>.Empty;
                break;

            case CallArgumentsType.KwargsOnly:
                args = [];
                kwargs = AstUtils.EvalKeywords(context, frame, Keywords);
                break;

            default:
                throw new UnreachableException();
        }

        var result = func.Call(context, args, kwargs);
        return result.PyUnwrap(context);
    }

    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        yield return Func;
        foreach (var arg in Args) yield return arg;
        foreach (var kw in Keywords) yield return kw;
    }
}

public sealed class ListNode : AstExprNode, IExprContextNode, IAstExprNodeNoSelfPythonException
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

    public override PyListObject ExecuteExpr(PyCallContext context, PyFrame frame)
    {
        return new PyListObject(AstUtils.EvalPyObjects(context, frame, Elts));
    }

    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        foreach (var elt in Elts) yield return elt;
    }
}

public sealed class TupleNode : AstExprNode, IExprContextNode, IAstExprNodeNoSelfPythonException
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
    public override PyTupleObject ExecuteExpr(PyCallContext context, PyFrame frame)
    {
        return PyTupleObject.CreateTuple(AstUtils.EvalPyObjects(context, frame, Elts));
    }

    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        foreach (var elt in Elts) yield return elt;
    }
}

public sealed class DictNode : AstExprNode, IAstExprNodeNoSelfPythonException
{
    internal DictNode(ImmutableArray<AstExprNode?> keys, ImmutableArray<AstExprNode> values)
    {
        Debug.Assert(keys.Length == values.Length);

        Keys = keys;
        Values = values;
    }

    public ImmutableArray<AstExprNode?> Keys { get; }
    public ImmutableArray<AstExprNode> Values { get; }

    public override PyDictObject ExecuteExpr(PyCallContext context, PyFrame frame)
    {
        List<KeyValuePair<PyObject, PyObject>> pairs = new(Keys.Length);
        for (int i = 0; i < Keys.Length; i++)
        {
            var key = Keys[i]?.GetExprValue(context, frame);
            var value = Values[i].GetExprValue(context, frame);
            if (key is null)
                pairs.AddRange(AstUtils.ExtractMapping(context, value));
            else
                pairs.Add(KeyValuePair.Create(key, value));
        }

        return PyDictObject.CreateDict(pairs);
    }

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

public sealed class SetNode : AstExprNode, IAstExprNodeNoSelfPythonException
{
    internal SetNode(ImmutableArray<AstExprNode> elts)
    {
        Elts = elts;
    }

    public ImmutableArray<AstExprNode> Elts { get; }

    public override PySetObject ExecuteExpr(PyCallContext context, PyFrame frame)
    {
        return new PySetObject(AstUtils.EvalPyObjects(context, frame, Elts));
    }

    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        foreach (var elt in Elts) yield return elt;
    }
}

public sealed class BoolOpNode : AstExprNode, IAstExprNodeBool, IAstExprNodeNoSelfPythonException
{
    internal BoolOpNode(BoolOpType op, ImmutableArray<AstExprNode> values)
    {
        Op = op;
        Values = values;
    }

    public BoolOpType Op { get; }
    public ImmutableArray<AstExprNode> Values { get; }

    public override PyObject ExecuteExpr(PyCallContext context, PyFrame frame)
    {
        return GetExprValueWithResult(context, frame).Value;
    }

    public (bool Result, PyObject Value) GetExprValueWithResult(PyCallContext context, PyFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        var (result, value) = Op is BoolOpType.And
            ? GetBoolAndValue(context, Values.Select(v => v.GetExprValue(context, frame)))
            : GetBoolOrValue(context, Values.Select(v => v.GetExprValue(context, frame)));
        return (result, value.PyUnwrap(context));
    }

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

    public override PyObject ExecuteExpr(PyCallContext context, PyFrame frame)
    {
        var left = Left.GetExprValue(context, frame);
        var right = Right.GetExprValue(context, frame);

        return EvalOperator(context, Operator, left, right).PyUnwrap(context);
    }

    private static PyResult EvalOperator(PyCallContext context, OperatorType op, PyObject left, PyObject right)
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

    public override PyObject ExecuteExpr(PyCallContext context, PyFrame frame)
    {
        var value = Operand.GetExprValue(context, frame);
        return (Op switch
        {
            UnaryOpType.Invert => PyOperators.Invert(context, value),
            UnaryOpType.Not => PyOperators.Not(context, value),
            UnaryOpType.UAdd => PyOperators.UAdd(context, value),
            UnaryOpType.USub => PyOperators.USub(context, value),
            _ => throw new UnreachableException(),
        }).PyUnwrap(context);
    }

    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        yield return Operand;
    }
}

public sealed class CompareNode : AstExprNode, IAstExprNodeBool
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

    public override PyObject ExecuteExpr(PyCallContext context, PyFrame frame)
    {
        return GetExprValueWithResult(context, frame).Value;
    }

    public (bool Result, PyObject Value) GetExprValueWithResult(PyCallContext context, PyFrame frame)
    {
        PyObject lastValue = null!;

        var left = Left.GetExprValue(context, frame);
        for (int i = 0; i < Ops.Length; i++)
        {
            var op = Ops[i];
            var right = Comparators[i].GetExprValue(context, frame);
            var value = (op switch
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
            }).PyUnwrap(context);

            var boolValue = PySpecialMethods.Bool(context, value).PyUnwrap(context);

            if (!boolValue.BoolValue)
                return (boolValue.BoolValue, value);

            lastValue = value;
        }

        return (true, lastValue);
    }

    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        yield return Left;
        foreach (var cmp in Comparators) yield return cmp;
    }
}

public sealed class IfExpNode : AstExprNode, IAstExprNodeNoSelfPythonException
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

    public override PyObject ExecuteExpr(PyCallContext context, PyFrame frame)
    {
        if (PySpecialMethods.Bool(context, Test.GetExprValue(context, frame)).PyUnwrap(context).PyCast<PyBoolObject>(context).BoolValue)
            return Body.GetExprValue(context, frame);
        return OrElse.GetExprValue(context, frame);
    }

    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        yield return Test;
        yield return Body;
        yield return OrElse;
    }
}

public sealed class ListCompNode : AstExprNode, IAstExprNodeNoSelfPythonException
{
    internal ListCompNode(AstExprNode elt, ImmutableArray<AstComprehensionNode> generators)
    {
        Elt = elt;
        Generators = generators;
    }

    public AstExprNode Elt { get; }
    public ImmutableArray<AstComprehensionNode> Generators { get; }

    public override PyObject ExecuteExpr(PyCallContext context, PyFrame frame)
    {
        var inlineFrame = frame.CreateInlineFrame(FrameType.Comprehension);
        var list = new List<PyObject>();
        For(0);
        return new PyListObject(list);

        void For(int index)
        {
            var generator = Generators[index];
            if (!Utils.TryEnumerateIterable(context, generator.Iter.GetExprValue(context, inlineFrame), out var iter, out var err))
                err.Value.PyThrow(context);
            foreach (var item in iter)
            {
                generator.Target.SetTargetValue(context, item.PyUnwrap(context), inlineFrame);
                var shouldContinue = false;
                foreach (var test in generator.Ifs)
                {
                    if (!test.GetBoolValue(context, inlineFrame))
                    {
                        shouldContinue = true;
                        break;
                    }
                }
                if (shouldContinue)
                    continue;

                if (index < Generators.Length - 1)
                {
                    For(index + 1);
                }
                else if (index == Generators.Length - 1)
                {
                    list.Add(Elt.GetExprValue(context, inlineFrame));
                }
            }
        }
    }

    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        yield return Elt;
        foreach (var gen in Generators) yield return gen;
    }
}

public sealed class SetCompNode : AstExprNode, IAstExprNodeNoSelfPythonException
{
    internal SetCompNode(AstExprNode elt, ImmutableArray<AstComprehensionNode> generators)
    {
        Elt = elt;
        Generators = generators;
    }

    public AstExprNode Elt { get; }
    public ImmutableArray<AstComprehensionNode> Generators { get; }

    public override PyObject ExecuteExpr(PyCallContext context, PyFrame frame)
    {
        var inlineFrame = frame.CreateInlineFrame(FrameType.Comprehension);
        var list = new List<PyObject>();
        For(0);
        return new PySetObject(list);

        void For(int index)
        {
            var generator = Generators[index];
            if (!Utils.TryEnumerateIterable(context, generator.Iter.GetExprValue(context, inlineFrame), out var iter, out var err))
                err.Value.PyThrow(context);
            foreach (var item in iter)
            {
                generator.Target.SetTargetValue(context, item.PyUnwrap(context), inlineFrame);
                var shouldContinue = false;
                foreach (var test in generator.Ifs)
                {
                    if (!test.GetBoolValue(context, inlineFrame))
                    {
                        shouldContinue = true;
                        break;
                    }
                }
                if (shouldContinue)
                    continue;

                if (index < Generators.Length - 1)
                {
                    For(index + 1);
                }
                else if (index == Generators.Length - 1)
                {
                    list.Add(Elt.GetExprValue(context, inlineFrame));
                }
            }
        }
    }

    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        yield return Elt;
        foreach (var gen in Generators) yield return gen;
    }
}

public sealed class DictCompNode : AstExprNode, IAstExprNodeNoSelfPythonException
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

    public override PyObject ExecuteExpr(PyCallContext context, PyFrame frame)
    {
        var inlineFrame = frame.CreateInlineFrame(FrameType.Comprehension);
        var list = new List<KeyValuePair<PyObject, PyObject>>();
        For(0);
        return new PyDictObject(list);

        void For(int index)
        {
            var generator = Generators[index];
            if (!Utils.TryEnumerateIterable(context, generator.Iter.GetExprValue(context, inlineFrame), out var iter, out var err))
                err.Value.PyThrow(context);
            foreach (var item in iter)
            {
                generator.Target.SetTargetValue(context, item.PyUnwrap(context), inlineFrame);
                var shouldContinue = false;
                foreach (var test in generator.Ifs)
                {
                    if (!test.GetBoolValue(context, inlineFrame))
                    {
                        shouldContinue = true;
                        break;
                    }
                }
                if (shouldContinue)
                    continue;

                if (index < Generators.Length - 1)
                {
                    For(index + 1);
                }
                else if (index == Generators.Length - 1)
                {
                    list.Add(KeyValuePair.Create(Key.GetExprValue(context, inlineFrame), Value.GetExprValue(context, inlineFrame)));
                }
            }
        }
    }

    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        yield return Key;
        yield return Value;
        foreach (var gen in Generators) yield return gen;
    }

}

public sealed class GeneratorExpNode : AstExprNode, IAstExprNodeNoSelfPythonException
{
    internal GeneratorExpNode(AstExprNode elt, ImmutableArray<AstComprehensionNode> generators)
    {
        Elt = elt;
        Generators = generators;
    }

    public AstExprNode Elt { get; }
    public ImmutableArray<AstComprehensionNode> Generators { get; }

    public override PyObject ExecuteExpr(PyCallContext context, PyFrame frame)
    {
        var inlineFrame = frame.CreateInlineFrame(FrameType.Comprehension);
        return new PyGeneratorExpressionObject(inlineFrame, For(0).GetEnumerator());

        IEnumerable<PyObject> For(int index)
        {
            var generator = Generators[index];
            if (!Utils.TryEnumerateIterable(context, generator.Iter.GetExprValue(context, inlineFrame), out var iter, out var err))
                err.Value.PyThrow(context);
            foreach (var item in iter)
            {
                generator.Target.SetTargetValue(context, item.PyUnwrap(context), inlineFrame);
                var shouldContinue = false;
                foreach (var test in generator.Ifs)
                {
                    if (!test.GetBoolValue(context, inlineFrame))
                    {
                        shouldContinue = true;
                        break;
                    }
                }
                if (shouldContinue)
                    continue;

                if (index < Generators.Length - 1)
                {
                    foreach (var elt in For(index + 1))
                        yield return elt;
                }
                else if (index == Generators.Length - 1)
                {
                    var elt = Elt.GetExprValue(context, inlineFrame);
                    yield return elt;
                }
            }
        }
    }

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

    public override PyObject ExecuteExpr(PyCallContext context, PyFrame frame)
    {
        var variableScope = frame.SemanticModel?.GetVariableScope<LambdaVariableScope>(this)
            ?? throw new InvalidOperationException();
        Debug.Assert(variableScope.CodeObject is not null);

        Caller caller = variableScope.HasYield ?
            new GeneratorCaller(context, variableScope, frame, GetResult) :
            new FunctionCaller(context, variableScope, frame, GetResult);

        var func = new PyFunctionObject(
            "<lambda>",
            caller.Call,
            caller.GetFreeVars(frame),
            frame._globals,
            variableScope.CodeObject);

        Debug.Assert(variableScope.QualName is not null);
        func.PyAttributes.Add(PySpecialNames.QualName, PyStrObject.FromString(variableScope.QualName));

        caller.Func = func;
        return func;
    }

    private PyResult GetResult(PyCallContext context, PyFrame frame)
    {
        try
        {
            return Body.GetExprValue(context, frame);
        }
        catch (PyRuntimeException e)
        {
            e.PyException.WithTraceback(context, overwriteExisting: false);
            context.EnsureFrameState(frame);

            return PyResult.FromException(e.PyException);
        }
    }

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

public sealed class JoinedStrNode : AstExprNode, IAstExprNodeNoSelfPythonException
{
    internal JoinedStrNode(ImmutableArray<AstExprNode> values)
    {
        Values = values;
    }

    public ImmutableArray<AstExprNode> Values { get; }

    public override PyObject ExecuteExpr(PyCallContext context, PyFrame frame)
    {
        var builder = new StringBuilder();

        foreach (var expr in Values)
        {
            var result = PySpecialMethods.Str(context, expr.GetExprValue(context, frame));
            builder.Append(result.PyUnwrap(context).Value);
        }

        return PyStrObject.FromString(builder.ToString());
    }

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

    public override PyObject ExecuteExpr(PyCallContext context, PyFrame frame)
    {
        var result = Value.GetExprValue(context, frame);

        if (Conversion is 's')
            result = PySpecialMethods.Str(context, result).PyUnwrap(context);
        else if (Conversion is 'r')
            result = PySpecialMethods.Repr(context, result).PyUnwrap(context);
        else if (Conversion is 'a')
            result = PyBuiltinFunctions.Ascii.Call(context, [result]).PyUnwrap(context);
        else if (Conversion is not -1)
            throw new UnreachableException();

        if (FormatSpec is not null)
        {
            var spec = FormatSpec.GetExprValue(context, frame);
            Debug.Assert(spec is PyStrObject);
            result = PySpecialMethods.Format(context, result, spec).PyUnwrap(context);
        }

        return result;
    }

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

    public override PyObject ExecuteExpr(PyCallContext context, PyFrame frame)
    {
        var value = Value?.GetExprValue(context, frame) ?? PyNoneObject.None;
        Debug.Assert(frame.FrameType is FrameType.YieldFunction or FrameType.YieldLambda);
        frame._tcsWaitAtStartOrYield = new();
        Debug.Assert(frame._tcsWaitAtSend is not null);
        frame._tcsWaitAtSend.SetResult(value);
        var callerAction = frame._tcsWaitAtStartOrYield.Task.Result;
        switch (callerAction.Type)
        {
            case YieldCallerAction.ActionType.Next:
                return PyNoneObject.None;

            case YieldCallerAction.ActionType.Send:
                Debug.Assert(callerAction.Value is not null);
                return callerAction.Value;

            case YieldCallerAction.ActionType.Throw:
                Debug.Assert(callerAction.Value is not null);
                PyResult.RaiseExceptionFromTypeOrInstance(callerAction.Value).PyThrow(context);
                throw new UnreachableException();

            case YieldCallerAction.ActionType.Close:
                throw context.GeneratorExit(string.Empty);

            default:
                throw new UnreachableException();
        }
    }

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

    public override PyObject ExecuteExpr(PyCallContext context, PyFrame frame)
    {
        Debug.Assert(frame.FrameType is FrameType.YieldFunction or FrameType.YieldLambda);

        var iter = PySpecialMethods.Iter(context, Value.GetExprValue(context, frame)).PyUnwrap(context);
        var value = PySpecialMethods.Next(context, iter).PyUnwrap(context);
        while (true)
        {
            frame._tcsWaitAtStartOrYield = new();
            Debug.Assert(frame._tcsWaitAtSend is not null);
            frame._tcsWaitAtSend.SetResult(value);
            var callerAction = frame._tcsWaitAtStartOrYield.Task.Result;
            switch (callerAction.Type)
            {
                case YieldCallerAction.ActionType.Next:
                    var iterNextRet = PySpecialMethods.Next(context, iter);
                    if (iterNextRet.IsStopIteration)
                        return iterNextRet.Exception.Args.ElementAtOrDefault(0) ?? PyNoneObject.None;
                    value = iterNextRet.PyUnwrap(context);
                    break;

                case YieldCallerAction.ActionType.Send:
                    Debug.Assert(callerAction.Value is not null);
                    var iterSendRet = iter.CallMethod(context, "send", [callerAction.Value]);
                    if (iterSendRet.IsStopIteration)
                        return iterSendRet.Exception.Args.ElementAtOrDefault(0) ?? PyNoneObject.None;
                    value = iterSendRet.PyUnwrap(context);
                    break;

                case YieldCallerAction.ActionType.Throw:
                    Debug.Assert(callerAction.Value is not null);
                    var iterThrowRet = iter.CallMethod(context, "throw", [callerAction.Value]);
                    if (iterThrowRet.IsStopIteration)
                        return iterThrowRet.Exception.Args.ElementAtOrDefault(0) ?? PyNoneObject.None;
                    value = iterThrowRet.PyUnwrap(context);
                    break;

                case YieldCallerAction.ActionType.Close:
                    var close = PyOperators.GetAttr(context, iter, "close");
                    if (!close.IsAttributeError)
                        _ = close.PyUnwrap(context).Call(context, [], FrozenDictionary<string, PyObject>.Empty).PyUnwrap(context);
                    throw context.GeneratorExit(string.Empty);

                default:
                    throw new UnreachableException();
            }
        }
    }

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

    public override PyObject ExecuteExpr(PyCallContext context, PyFrame frame)
    {
        throw new NotSupportedException();
    }

    internal IReadOnlyList<PyObject> Unpack(PyCallContext context, PyFrame frame)
    {
        var value = Value.GetExprValue(context, frame);
        if (!Utils.TryEnumeratedIterable(context, value, out var result, out var err))
            err.Value.PyThrow(context);
        return result;
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

    public override PyObject ExecuteExpr(PyCallContext context, PyFrame frame)
    {
        var value = Value.GetExprValue(context, frame);
        Target.SetTargetValue(context, value, frame);
        if (frame._outerNonInlineFrame is not null)
            Target.SetTargetValue(context, value, frame._outerNonInlineFrame);
        return value;
    }
}