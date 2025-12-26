using PySharp.PyModules;
using PySharp.PyModules.Builtins;
using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;
using System.Collections;
using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;
using static PySharp.AstNodes.ReturnNode;

namespace PySharp.AstNodes;

public abstract class AstExprNode : AstNode, IAstNodeLocation
{
    public PyObject GetExprValue(PyCallContext context, PyFrame frame)
    {
        if (this is IAstExprNodeNoSelfPythonException)
            return ExecuteExpr(context, frame);

        var previousProvider = frame.ExprMetaInfoProvider;
        frame.ExprMetaInfoProvider = this;
        var value = ExecuteExpr(context, frame);
        frame.ExprMetaInfoProvider = previousProvider;
        return value;
    }

    public virtual bool? NoSideEffects()
    {
        return null;
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

public enum ExprContext
{
    Unknown = 0,
    Load,
    Store,
    Del
}

internal interface IExprContextNode
{
    public ExprContext Ctx { get; set; }
}

internal interface ITargetNode
{
    void SetVaue(PyCallContext context, PyObject value, PyFrame frame);
    void DeleteValue(PyCallContext context, PyFrame frame);
}

public sealed class NameNode : AstExprNode, IExprContextNode, ITargetNode
{
    internal NameNode(string identifier)
    {
        Identifier = identifier;
    }

    public string Identifier { get; }
    public ExprContext Ctx { get; set; } = ExprContext.Load;

    public override PyObject ExecuteExpr(PyCallContext context, PyFrame frame)
    {
        return frame.GetValue(Identifier);
    }

    internal override void Dump(AstNodeDumper dumper)
    {
        dumper
            .AppendFormat("Name(id={0}, ctx={1}())", PyStrConverter.FromStringToLiteral(Identifier), Ctx);
    }

    void ITargetNode.DeleteValue(PyCallContext context, PyFrame frame)
    {
        frame.RemoveValue(Identifier);
    }

    void ITargetNode.SetVaue(PyCallContext context, PyObject value, PyFrame frame)
    {
        frame.SetValue(Identifier, value);
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

    public override bool? NoSideEffects()
    {
        return true;
    }

    internal override void Dump(AstNodeDumper dumper)
    {
        dumper
            .AppendFormat("Constant(value={0})", PySpecialMethods.TryGetRepr(PyCallContext.NonContextDependency, Value, out var s, out var result) ? s.Value : "<ast-format repr failed>");
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
    public ExprContext Ctx { get; set; } = ExprContext.Load;

    public override PyObject ExecuteExpr(PyCallContext context, PyFrame frame)
    {
        var value = Value.GetExprValue(context, frame);
        var attr = PyOperators.GetAttr(context, value, Identifier);
        return attr.PyUnwrap(context);
    }

    internal override void Dump(AstNodeDumper dumper)
    {
        dumper
            .Append("Attribute")
            .AppendFields(("value", Value), ("attr", PyStrConverter.FromStringToLiteral(Identifier)), ("ctx", Ctx));
    }

    public override void EnumerateNodes(Action<AstNode> action)
    {
        base.EnumerateNodes(action);
        Value.EnumerateNodes(action);
    }

    void ITargetNode.SetVaue(PyCallContext context, PyObject value, PyFrame frame)
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
    public new AstExprNode Slice { get; }
    public ExprContext Ctx { get; set; } = ExprContext.Load;

    public override PyObject ExecuteExpr(PyCallContext context, PyFrame frame)
    {
        return GetItem(context, frame);
    }

    public PyObject GetItem(PyCallContext context, PyFrame frame)
    {
        var value = Value.GetExprValue(context, frame);
        var slice = Slice.GetExprValue(context, frame);
        return value.GetItem(context, slice).PyUnwrap(context);
    }

    public PyObject SetItem(PyCallContext context, PyFrame frame, PyObject obj)
    {
        var value = Value.GetExprValue(context, frame);
        var slice = Slice.GetExprValue(context, frame);
        return value.SetItem(context, slice, obj).PyUnwrap(context);
    }

    public PyObject DelItem(PyFrame frame, PyCallContext context)
    {
        var value = Value.GetExprValue(context, frame);
        var slice = Slice.GetExprValue(context, frame);
        return value.Delete(context, slice).PyUnwrap(context);
    }

    internal override void Dump(AstNodeDumper dumper)
    {
        dumper
            .Append("Subscript")
            .AppendFields(("value", Value), ("slice", Slice), ("ctx", Ctx));
    }

    public override void EnumerateNodes(Action<AstNode> action)
    {
        base.EnumerateNodes(action);
        Value.EnumerateNodes(action);
        Slice.EnumerateNodes(action);
    }

    void ITargetNode.SetVaue(PyCallContext context, PyObject value, PyFrame frame)
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

    internal override void Dump(AstNodeDumper dumper)
    {
        dumper
            .Append("Slice")
            .AppendFields(("lower", Lower), ("upper", Upper), ("step", Step));
    }

    public override void EnumerateNodes(Action<AstNode> action)
    {
        base.EnumerateNodes(action);
        Lower?.EnumerateNodes(action);
        Upper?.EnumerateNodes(action);
        Step?.EnumerateNodes(action);
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
        var func = Func.GetExprValue(context, frame).PyThrowIfNull(context);

        IReadOnlyList<PyObject> args;
        IReadOnlyDictionary<string, PyObject> kwargs;

        switch (_argsType)
        {
            case CallArgumentsType.ArgsKwargs:
                args = [.. Args.Select(arg => arg.GetExprValue(context, frame))];
                kwargs = Keywords.ToDictionary(keyword => keyword.Arg, keyword => keyword.Value.GetExprValue(context, frame));
                break;

            case CallArgumentsType.NoArgsOrKwargs:
                args = [];
                kwargs = FrozenDictionary<string, PyObject>.Empty;
                break;

            case CallArgumentsType.ArgsOnly:
                args = [.. Args.Select(arg => arg.GetExprValue(context, frame))];
                kwargs = FrozenDictionary<string, PyObject>.Empty;
                break;

            case CallArgumentsType.KwargsOnly:
                args = [];
                kwargs = Keywords.ToDictionary(keyword => keyword.Arg, keyword => keyword.Value.GetExprValue(context, frame));
                break;

            default:
                throw new UnreachableException();
        }

        var result = func.Call(context, args, kwargs);
        return result.PyUnwrap(context);
    }

    internal override void Dump(AstNodeDumper dumper)
    {
        dumper
            .Append("Call")
            .AppendFields(("func", Func), ("args", Args), ("keywords", Keywords));
    }

    public override void EnumerateNodes(Action<AstNode> action)
    {
        base.EnumerateNodes(action);
        Func.EnumerateNodes(action);
        Args.EnumerateNodes(action);
        Keywords.EnumerateNodes(action);
    }
}

public sealed class ListNode : AstExprNode, IExprContextNode, IAstExprNodeNoSelfPythonException
{
    internal ListNode(ImmutableArray<AstExprNode> elts)
    {
        Elts = elts;
    }

    public ImmutableArray<AstExprNode> Elts { get; }
    public ExprContext Ctx { get; set; } = ExprContext.Load;

    public override PyListObject ExecuteExpr(PyCallContext context, PyFrame frame)
    {
        return new PyListObject(Elts.Select(item => item.GetExprValue(context, frame)));
    }

    public override bool? NoSideEffects()
    {
        if (Elts.All(elt => elt.NoSideEffects() is true))
            return true;

        return null;
    }

    internal override void Dump(AstNodeDumper dumper)
    {
        dumper
            .Append("List")
            .AppendFields(("elts", Elts), ("ctx", Ctx));
    }

    public override void EnumerateNodes(Action<AstNode> action)
    {
        base.EnumerateNodes(action);
        Elts.EnumerateNodes(action);
    }
}

public sealed class TupleNode : AstExprNode, IExprContextNode, IAstExprNodeNoSelfPythonException
{
    internal TupleNode(ImmutableArray<AstExprNode> elts)
    {
        Elts = elts;
    }

    public ImmutableArray<AstExprNode> Elts { get; }
    public ExprContext Ctx { get; set; } = ExprContext.Load;

    public override PyTupleObject ExecuteExpr(PyCallContext context, PyFrame frame)
    {
        return PyTupleObject.CreateTuple(Elts.Select(item => item.GetExprValue(context, frame)));
    }

    public override bool? NoSideEffects()
    {
        if (Elts.All(elt => elt.NoSideEffects() is true))
            return true;

        return null;
    }

    internal override void Dump(AstNodeDumper dumper)
    {
        dumper
            .Append("Tuple")
            .AppendFields(("elts", Elts), ("ctx", Ctx));
    }

    public override void EnumerateNodes(Action<AstNode> action)
    {
        base.EnumerateNodes(action);
        Elts.EnumerateNodes(action);
    }
}

public sealed class DictNode : AstExprNode, IAstExprNodeNoSelfPythonException
{
    internal DictNode(ImmutableArray<AstExprNode> keys, ImmutableArray<AstExprNode> values)
    {
        Debug.Assert(keys.Length == values.Length);

        Keys = keys;
        Values = values;
    }

    public ImmutableArray<AstExprNode> Keys { get; }
    public ImmutableArray<AstExprNode> Values { get; }

    public override PyDictObject ExecuteExpr(PyCallContext context, PyFrame frame)
    {
        return new PyDictObject(
            Keys
            .Zip(Values)
            .Select(item => KeyValuePair.Create(
                item.First.GetExprValue(context, frame),
                item.Second.GetExprValue(context, frame)))
            );
    }

    public override bool? NoSideEffects()
    {
        if (Keys.All(elt => elt.NoSideEffects() is true) && Values.All(elt => elt.NoSideEffects() is true))
            return true;

        return null;
    }

    internal override void Dump(AstNodeDumper dumper)
    {
        dumper
            .Append("Dict")
            .AppendFields(("keys", Keys), ("values", Values));
    }

    public override void EnumerateNodes(Action<AstNode> action)
    {
        base.EnumerateNodes(action);
        Keys.EnumerateNodes(action);
        Values.EnumerateNodes(action);
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
        return new PySetObject(Elts.Select(item => item.GetExprValue(context, frame)));
    }

    public override bool? NoSideEffects()
    {
        if (Elts.All(elt => elt.NoSideEffects() is true))
            return true;

        return null;
    }
    internal override void Dump(AstNodeDumper dumper)
    {
        dumper
            .Append("Set")
            .AppendFields(("elts", Elts));
    }

    public override void EnumerateNodes(Action<AstNode> action)
    {
        base.EnumerateNodes(action);
        Elts.EnumerateNodes(action);
    }
}

public sealed class BoolOpNode : AstExprNode, IAstExprNodeBool, IAstExprNodeNoSelfPythonException
{
    internal BoolOpNode(AstBoolOpNode op, ImmutableArray<AstExprNode> values)
    {
        Op = op;
        Values = values;
    }

    public AstBoolOpNode Op { get; }
    public ImmutableArray<AstExprNode> Values { get; }

    public override PyObject ExecuteExpr(PyCallContext context, PyFrame frame)
    {
        return GetExprValueWithResult(context, frame).Value;
    }

    public (bool Result, PyObject Value) GetExprValueWithResult(PyCallContext context, PyFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        var ret = Op.GetBoolOpValue(context, Values.Select(v => v.GetExprValue(context, frame)));
        ret.Value.PyThrowIfNull(context);
        return ret!;
    }

    public override void EnumerateNodes(Action<AstNode> action)
    {
        base.EnumerateNodes(action);
        Op.EnumerateNodes(action);
        Values.EnumerateNodes(action);
    }
}

public sealed class BinOpNode : AstExprNode
{
    internal BinOpNode(AstOperatorNode op, AstExprNode left, AstExprNode right)
    {
        Left = left;
        Right = right;
        Operator = op;
    }

    public AstOperatorNode Operator { get; }
    public AstExprNode Left { get; }
    public AstExprNode Right { get; }

    public override PyObject ExecuteExpr(PyCallContext context, PyFrame frame)
    {
        var left = Left.GetExprValue(context, frame);
        var right = Right.GetExprValue(context, frame);
        return Operator.GetOpValue(context, left, right).PyUnwrapIncludedNotImplemented(context);
    }

    public override void EnumerateNodes(Action<AstNode> action)
    {
        base.EnumerateNodes(action);
        Operator.EnumerateNodes(action);
        Left.EnumerateNodes(action);
        Right.EnumerateNodes(action);
    }
}

public sealed class UnaryOpNode : AstExprNode
{
    internal UnaryOpNode(AstUnaryOpNode op, AstExprNode operand)
    {
        Op = op;
        Operand = operand;
    }

    public AstUnaryOpNode Op { get; }
    public AstExprNode Operand { get; }

    public override PyObject ExecuteExpr(PyCallContext context, PyFrame frame)
    {
        return Op.GetUnaryOpValue(context, Operand.GetExprValue(context, frame)).PyUnwrap(context);
    }

    public override void EnumerateNodes(Action<AstNode> action)
    {
        base.EnumerateNodes(action);
        Op.EnumerateNodes(action);
        Operand.EnumerateNodes(action);
    }
}

public sealed class CompareNode : AstExprNode, IAstExprNodeBool
{
    internal CompareNode(AstExprNode left, ImmutableArray<AstCmpopNode> ops, ImmutableArray<AstExprNode> comparators)
    {
        Left = left;
        Ops = ops;
        Comparators = comparators;
    }

    public AstExprNode Left { get; }
    public ImmutableArray<AstCmpopNode> Ops { get; }
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
            var value = op.GetCompareValue(context, left, right).PyUnwrap(context);
            var boolValue = (PyBoolObject)PySpecialMethods.GetBool(context, value).PyUnwrap(context);

            if (!boolValue.BoolValue)
                return (boolValue.BoolValue, value);

            lastValue = value;
        }

        return (true, lastValue);
    }

    public override void EnumerateNodes(Action<AstNode> action)
    {
        base.EnumerateNodes(action);
        Left.EnumerateNodes(action);
        Ops.EnumerateNodes(action);
        Comparators.EnumerateNodes(action);
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
        if (Test.GetExprValue(context, frame).Bool(context).PyUnwrap(context).PyCast<PyBoolObject>(context).BoolValue)
            return Body.GetExprValue(context, frame).PyThrowIfNull(context);
        return OrElse.GetExprValue(context, frame).PyThrowIfNull(context);
    }

    public override void EnumerateNodes(Action<AstNode> action)
    {
        base.EnumerateNodes(action);
        Test.EnumerateNodes(action);
        Body.EnumerateNodes(action);
        OrElse.EnumerateNodes(action);
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
        var tempFrame = frame.TempFrame(FrameType.Comprehension);
        var list = new List<PyObject>();
        For(0);
        return new PyListObject(list);

        void For(int index)
        {
            var generator = Generators[index];
            if (!Utils.TryEnumerateIterable(context, generator.Iter.GetExprValue(context, tempFrame), out var iter, out var err))
                err.Value.PyThrow(context);
            foreach (var item in iter)
            {
                generator.Target.SetTargetValue(context, item.PyUnwrap(context), tempFrame);
                var shouldContinue = false;
                foreach (var test in generator.Ifs)
                {
                    if (!test.GetBoolValue(context, tempFrame))
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
                    list.Add(Elt.GetExprValue(context, tempFrame));
                }
            }
        }
    }

    public override void EnumerateNodes(Action<AstNode> action)
    {
        base.EnumerateNodes(action);
        Elt.EnumerateNodes(action);
        Generators.EnumerateNodes(action);
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
        var tempFrame = frame.TempFrame(FrameType.Comprehension);
        var list = new List<PyObject>();
        For(0);
        return new PySetObject(list);

        void For(int index)
        {
            var generator = Generators[index];
            if (!Utils.TryEnumerateIterable(context, generator.Iter.GetExprValue(context, tempFrame), out var iter, out var err))
                err.Value.PyThrow(context);
            foreach (var item in iter)
            {
                generator.Target.SetTargetValue(context, item.PyUnwrap(context), tempFrame);
                var shouldContinue = false;
                foreach (var test in generator.Ifs)
                {
                    if (!test.GetBoolValue(context, tempFrame))
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
                    list.Add(Elt.GetExprValue(context, tempFrame));
                }
            }
        }
    }

    public override void EnumerateNodes(Action<AstNode> action)
    {
        base.EnumerateNodes(action);
        Elt.EnumerateNodes(action);
        Generators.EnumerateNodes(action);
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
        var tempFrame = frame.TempFrame(FrameType.Comprehension);
        var list = new List<KeyValuePair<PyObject, PyObject>>();
        For(0);
        return new PyDictObject(list);

        void For(int index)
        {
            var generator = Generators[index];
            if (!Utils.TryEnumerateIterable(context, generator.Iter.GetExprValue(context, tempFrame), out var iter, out var err))
                err.Value.PyThrow(context);
            foreach (var item in iter)
            {
                generator.Target.SetTargetValue(context, item.PyUnwrap(context), tempFrame);
                var shouldContinue = false;
                foreach (var test in generator.Ifs)
                {
                    if (!test.GetBoolValue(context, tempFrame))
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
                    list.Add(KeyValuePair.Create(Key.GetExprValue(context, tempFrame), Value.GetExprValue(context, tempFrame)));
                }
            }
        }
    }

    public override void EnumerateNodes(Action<AstNode> action)
    {
        base.EnumerateNodes(action);
        Key.EnumerateNodes(action);
        Value.EnumerateNodes(action);
        Generators.EnumerateNodes(action);
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
        var tempFrame = frame.TempFrame(FrameType.Comprehension);
        return new PyGeneratorExpressionObject(tempFrame, For(0).GetEnumerator());

        IEnumerable<PyObject> For(int index)
        {
            var generator = Generators[index];
            if (!Utils.TryEnumerateIterable(context, generator.Iter.GetExprValue(context, tempFrame), out var iter, out var err))
                err.Value.PyThrow(context);
            foreach (var item in iter)
            {
                generator.Target.SetTargetValue(context, item.PyUnwrap(context), tempFrame);
                var shouldContinue = false;
                foreach (var test in generator.Ifs)
                {
                    if (!test.GetBoolValue(context, tempFrame))
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
                    var elt = Elt.GetExprValue(context, tempFrame);
                    yield return elt;
                }
            }
        }
    }

    public override void EnumerateNodes(Action<AstNode> action)
    {
        base.EnumerateNodes(action);
        Elt.EnumerateNodes(action);
        Generators.EnumerateNodes(action);
    }

}

public sealed class LambdaNode : AstExprNode, IFunctionOrLambda
{
    internal LambdaNode(AstArgumentsNode args)
    {
        Args = args;
        Body = null!;
    }

    public AstArgumentsNode Args { get; }
    public AstExprNode Body { get; internal set; }
    FrozenDictionary<string, PyVariableType> IAstVariableScopeOwner.Variables { get; set; } = null!;
    string[] IFunctionOrLambda.CapturedVariables { get; set; } = null!;
    string[] IFunctionOrLambda.LocalVariables { get; set; } = null!;
    bool IFunctionOrLambda.HasYield { get; set; } = false;

    public override PyObject ExecuteExpr(PyCallContext context, PyFrame frame)
    {
        ICaller caller = ((IFunctionOrLambda)this).HasYield ?
            new GeneratorCaller(context, this, frame, GetResult) :
            new FunctionCaller(context, this, frame, GetResult);
        var func = new PyFunctionObject("<lambda>", caller.Call, frame.InternalClosure?.Values, frame._globals);
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
            return PyResult.FromException(e.PyException);
        }
    }

    public override void EnumerateNodes(Action<AstNode> action)
    {
        base.EnumerateNodes(action);
        Args.EnumerateNodes(action);
        Body.EnumerateNodes(action);
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
            if (!PySpecialMethods.TryGetStr(context, expr.GetExprValue(context, frame), out var s, out var result))
            {
                result.PyUnwrap(context);
            }

            builder.Append(s!.Value);
        }

        return PyStrObject.FromString(builder.ToString());
    }

    public override void EnumerateNodes(Action<AstNode> action)
    {
        base.EnumerateNodes(action);
        Values.EnumerateNodes(action);
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

        if (FormatSpec is not null)
        {
            var spec = FormatSpec.GetExprValue(context, frame);
            Debug.Assert(spec is PyStrObject);
            result = result.Format(context, ((PyStrObject)spec).Value).PyUnwrap(context);
        }

        if (Conversion is -1 or 's') // TODO: does case -1 need convert?
            result = PySpecialMethods.GetStr(context, result).PyUnwrap(context);
        else if (Conversion is 'r')
            result = PySpecialMethods.GetRepr(context, result).PyUnwrap(context);
        else if (Conversion is 'a')
            throw new NotImplementedException();
        else
            throw new UnreachableException();

        return result;
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
                throw PyCallContext.ThrowException(PyStandardExceptionTypes.GeneratorExit);

            default:
                throw new UnreachableException();
        }
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

        var iter = Value.GetExprValue(context, frame).Iter(context).PyUnwrap(context);
        var value = iter.Next(context).PyUnwrap(context);
        while (true)
        {
            frame._tcsWaitAtStartOrYield = new();
            Debug.Assert(frame._tcsWaitAtSend is not null);
            frame._tcsWaitAtSend.SetResult(value);
            var callerAction = frame._tcsWaitAtStartOrYield.Task.Result;
            switch (callerAction.Type)
            {
                case YieldCallerAction.ActionType.Next:
                    var iterNextRet = iter.Next(context);
                    if (iterNextRet.IsStopIteration)
                        return iterNextRet.Exception.Args.ElementAtOrDefault(0) ?? PyNoneObject.None;
                    value = iterNextRet.PyUnwrap(context);
                    break;

                case YieldCallerAction.ActionType.Send:
                    Debug.Assert(callerAction.Value is not null);
                    var iterSendRet = PyOperators.GetAttr(context, iter, "send").PyUnwrap(context)
                        .Call(context, [callerAction.Value], FrozenDictionary<string, PyObject>.Empty);
                    if (iterSendRet.IsStopIteration)
                        return iterSendRet.Exception.Args.ElementAtOrDefault(0) ?? PyNoneObject.None;
                    value = iterSendRet.PyUnwrap(context);
                    break;

                case YieldCallerAction.ActionType.Throw:
                    Debug.Assert(callerAction.Value is not null);
                    var iterThrowRet = PyOperators.GetAttr(context, iter, "throw").PyUnwrap(context)
                        .Call(context, [callerAction.Value], FrozenDictionary<string, PyObject>.Empty);
                    if (iterThrowRet.IsStopIteration)
                        return iterThrowRet.Exception.Args.ElementAtOrDefault(0) ?? PyNoneObject.None;
                    value = iterThrowRet.PyUnwrap(context);
                    break;

                case YieldCallerAction.ActionType.Close:
                    var close = PyOperators.GetAttr(context, iter, "close");
                    if (!close.IsAttributeError)
                        _ = close.PyUnwrap(context).Call(context, [], FrozenDictionary<string, PyObject>.Empty).PyUnwrap(context);
                    throw PyCallContext.ThrowException(PyStandardExceptionTypes.GeneratorExit);

                default:
                    throw new UnreachableException();
            }
        }
    }
}