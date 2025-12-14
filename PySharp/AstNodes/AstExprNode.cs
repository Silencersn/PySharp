using PySharp.PyModules;
using PySharp.PyModules.Builtins;
using PySharp.PyRuntime;
using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;

namespace PySharp.AstNodes;

public abstract class AstExprNode : AstNode, IAstNodeLocation
{
    public PyObject GetExprValue(PyFrame frame)
    {
        if (this is IAstExprNodeNoSelfPythonException)
            return ExecuteExpr(frame);

        var previousProvider = frame.ExprMetaInfoProvider;
        frame.ExprMetaInfoProvider = this;
        var value = ExecuteExpr(frame);
        frame.ExprMetaInfoProvider = previousProvider;
        return value;
    }
    public override AstExprNode Reduce(OptimizationOptions options)
    {
        return this;
    }
    public virtual bool? NoSideEffects()
    {
        return null;
    }
    public abstract PyObject ExecuteExpr(PyFrame frame);
}

internal interface IAstExprNodeBool
{
    public (bool Result, PyObject Value) GetExprValueWithResult(PyFrame frame);
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
    void SetVaue(PyObject value, PyFrame frame);
    void DeleteValue(PyFrame frame);
}

public sealed class NameNode : AstExprNode, IExprContextNode, ITargetNode
{
    internal NameNode(string identifier)
    {
        Identifier = identifier;
    }

    public string Identifier { get; }
    public ExprContext Ctx { get; set; }

    public override PyObject ExecuteExpr(PyFrame frame)
    {
        return frame.GetValue(Identifier);
    }

    internal override void Dump(AstNodeDumper dumper)
    {
        dumper
            .AppendFormat("Name(id={0}, ctx={1}())", PyStrConverter.FromStringToLiteral(Identifier), Ctx);
    }

    void ITargetNode.DeleteValue(PyFrame frame)
    {
        frame.RemoveValue(Identifier);
    }

    void ITargetNode.SetVaue(PyObject value, PyFrame frame)
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

    public override PyObject ExecuteExpr(PyFrame frame)
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
            .AppendFormat("Constant(value={0})", PySpecialMethods.TryGetRepr(Value, out var s) ? s.Value : "<ast-format repr failed>");
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
    public ExprContext Ctx { get; set; }

    public override PyObject ExecuteExpr(PyFrame frame)
    {
        var value = Value.GetExprValue(frame);
        var attr = PyOperators.GetAttr(value, Identifier);
        return attr.PyThrowIfNull();
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

    void ITargetNode.SetVaue(PyObject value, PyFrame frame)
    {
        var obj = Value.GetExprValue(frame);
        PyOperators.SetAttr(obj, Identifier, value).PyThrowIfNull();
    }

    void ITargetNode.DeleteValue(PyFrame frame)
    {
        var obj = Value.GetExprValue(frame);
        PyOperators.DelAttr(obj, Identifier).PyThrowIfNull();
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
    public ExprContext Ctx { get; set; }

    public override PyObject ExecuteExpr(PyFrame frame)
    {
        return GetItem(frame);
    }

    public PyObject GetItem(PyFrame frame)
    {
        var value = Value.GetExprValue(frame);
        var slice = Slice.GetExprValue(frame);
        return value.GetItem(slice).PyThrowIfNull();
    }

    public PyObject SetItem(PyFrame frame, PyObject obj)
    {
        var value = Value.GetExprValue(frame);
        var slice = Slice.GetExprValue(frame);
        return value.SetItem(slice, obj).PyThrowIfNull();
    }

    public PyObject DelItem(PyFrame frame)
    {
        var value = Value.GetExprValue(frame);
        var slice = Slice.GetExprValue(frame);
        return value.Delete(slice).PyThrowIfNull();
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

    void ITargetNode.SetVaue(PyObject value, PyFrame frame)
    {
        SetItem(frame, value);
    }

    void ITargetNode.DeleteValue(PyFrame frame)
    {
        DelItem(frame);
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

    public override PyObject ExecuteExpr(PyFrame frame)
    {
        return new PySliceObject(
            Lower?.GetExprValue(frame) ?? PyNoneObject.None,
            Upper?.GetExprValue(frame) ?? PyNoneObject.None,
            Step?.GetExprValue(frame) ?? PyNoneObject.None
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

    public override PyObject ExecuteExpr(PyFrame frame)
    {
        var func = Func.GetExprValue(frame).PyThrowIfNull();

        IReadOnlyList<PyObject> args;
        IReadOnlyDictionary<string, PyObject> kwargs;

        switch (_argsType)
        {
            case CallArgumentsType.ArgsKwargs:
                args = [.. Args.Select(arg => arg.GetExprValue(frame))];
                kwargs = Keywords.ToDictionary(keyword => keyword.Arg, keyword => keyword.Value.GetExprValue(frame));
                break;

            case CallArgumentsType.NoArgsOrKwargs:
                args = [];
                kwargs = FrozenDictionary<string, PyObject>.Empty;
                break;

            case CallArgumentsType.ArgsOnly:
                args = [.. Args.Select(arg => arg.GetExprValue(frame))];
                kwargs = FrozenDictionary<string, PyObject>.Empty;
                break;

            case CallArgumentsType.KwargsOnly:
                args = [];
                kwargs = Keywords.ToDictionary(keyword => keyword.Arg, keyword => keyword.Value.GetExprValue(frame));
                break;

            default:
                throw new UnreachableException();
        }

        return func.Call(args, kwargs).PyThrowIfNull();
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
    public ExprContext Ctx { get; set; }

    public override PyListObject ExecuteExpr(PyFrame frame)
    {
        return new PyListObject(Elts.Select(item => item.GetExprValue(frame)));
    }

    public override bool? NoSideEffects()
    {
        if (Elts.All(elt => elt.NoSideEffects() is true))
            return true;

        return null;
    }

    public override ListNode Reduce(OptimizationOptions options)
    {
        if (options.NoOptimization)
            return this;

        return List(Elts.Select(elt => elt.Reduce(options)));
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
    public ExprContext Ctx { get; set; }

    public override PyTupleObject ExecuteExpr(PyFrame frame)
    {
        return PyTupleObject.CreateTuple(Elts.Select(item => item.GetExprValue(frame)));
    }

    public override bool? NoSideEffects()
    {
        if (Elts.All(elt => elt.NoSideEffects() is true))
            return true;

        return null;
    }

    public override TupleNode Reduce(OptimizationOptions options)
    {
        if (options.NoOptimization)
            return this;

        return Tuple(Elts.Select(elt => elt.Reduce(options)));
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

    public override PyDictObject ExecuteExpr(PyFrame frame)
    {
        return new PyDictObject(
            Keys
            .Zip(Values)
            .Select(item => KeyValuePair.Create(
                item.First.GetExprValue(frame),
                item.Second.GetExprValue(frame)))
            );
    }

    public override bool? NoSideEffects()
    {
        if (Keys.All(elt => elt.NoSideEffects() is true) && Values.All(elt => elt.NoSideEffects() is true))
            return true;

        return null;
    }

    public override DictNode Reduce(OptimizationOptions options)
    {
        if (options.NoOptimization)
            return this;

        return Dict(Keys.Zip(Values).Select(t => KeyValuePair.Create(t.First.Reduce(options), t.Second.Reduce(options))));
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

    public override PySetObject ExecuteExpr(PyFrame frame)
    {
        return new PySetObject(Elts.Select(item => item.GetExprValue(frame)));
    }

    public override bool? NoSideEffects()
    {
        if (Elts.All(elt => elt.NoSideEffects() is true))
            return true;

        return null;
    }

    public override SetNode Reduce(OptimizationOptions options)
    {
        if (options.NoOptimization)
            return this;

        return Set(Elts.Select(elt => elt.Reduce(options)));
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

    public override PyObject ExecuteExpr(PyFrame frame)
    {
        return GetExprValueWithResult(frame).Value;
    }

    public (bool Result, PyObject Value) GetExprValueWithResult(PyFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        var ret = Op.GetBoolOpValue(Values.Select(v => v.GetExprValue(frame)));
        ret.Value.PyThrowIfNull();
        return ret!;
    }

    public override AstExprNode Reduce(OptimizationOptions options)
    {
        if (options.NoOptimization)
            return this;

        var values = Values.Select(value => value.Reduce(options));

        if (options.ShortCircuit)
        {
            var reducedValues = new List<AstExprNode>();

            bool allConatant = true;
            foreach (var value in values)
            {
                var test = value.TrgGetConstantBoolValue();
                if (test is null)
                {
                    allConatant = false;
                    reducedValues.Add(value);
                }
                else
                {
                    if (Op is AndNode && test is false)
                    {
                        if (allConatant)
                            return Constant(false, null);

                        reducedValues.Add(value);
                        break;
                    }
                    else if (Op is OrNode && test is true)
                    {
                        if (allConatant)
                            return Constant(true, null);

                        reducedValues.Add(value);
                        break;
                    }
                }
            }

            if (reducedValues.Count is 0)
            {
                if (Op is AndNode)
                    return Constant(true, null);
                else // if (Op is OrNode)
                    return Constant(false, null);
            }
            else if (reducedValues.Count is 1)
            {
                return reducedValues[0];
            }

            values = reducedValues;
        }

        return BoolOp(Op, values);
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

    public override PyObject ExecuteExpr(PyFrame frame)
    {
        var left = Left.GetExprValue(frame);
        var right = Right.GetExprValue(frame);
        return Operator.GetOpValue(left, right).PyThrowIfNullOrNotImplemented();
    }

    public override AstExprNode Reduce(OptimizationOptions options)
    {
        if (options.ConstantFolding)
        {
            if (Left.Reduce(options) is ConstantNode leftConstant && Right.Reduce(options) is ConstantNode rightConstant)
            {
                var result = Operator.GetOpValue(leftConstant.Value, rightConstant.Value);
                if (result is not (null or PyNotImplementedObject))
                    return Constant(result, null);
            }
        }

        return this;
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

    public override PyObject ExecuteExpr(PyFrame frame)
    {
        return Op.GetUnaryOpValue(Operand.GetExprValue(frame)).PyThrowIfNull();
    }

    public override AstExprNode Reduce(OptimizationOptions options)
    {
        if (options.ConstantFolding)
        {
            if (Operand.Reduce(options) is ConstantNode operandConstant)
            {
                var result = Op.GetUnaryOpValue(operandConstant.Value);
                if (result is not null)
                    return Constant(result, null);
            }
        }

        return this;
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

    public override PyObject ExecuteExpr(PyFrame frame)
    {
        return GetExprValueWithResult(frame).Value;
    }

    public (bool Result, PyObject Value) GetExprValueWithResult(PyFrame frame)
    {
        PyObject lastValue = null!;

        var left = Left.GetExprValue(frame);
        for (int i = 0; i < Ops.Length; i++)
        {
            var op = Ops[i];
            var right = Comparators[i].GetExprValue(frame);
            var value = op.GetCompareValue(left, right).PyThrowIfNull();
            var boolValue = ((PyBoolObject)PySpecialMethods.GetBool(value).PyThrowIfNull()).BoolValue;

            if (!boolValue)
                return (boolValue, value);

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

    public override PyObject ExecuteExpr(PyFrame frame)
    {
        if (Test.GetExprValue(frame).Bool().PyCast<PyBoolObject>().BoolValue)
            return Body.GetExprValue(frame).PyThrowIfNull();
        return OrElse.GetExprValue(frame).PyThrowIfNull();
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

    public override PyObject ExecuteExpr(PyFrame frame)
    {
        var tempFrame = frame.TempFrame(FrameType.Comprehension);
        var list = new List<PyObject>();
        For(0);
        return new PyListObject(list);

        void For(int index)
        {
            var generator = Generators[index];
            var iter = Utils.EnumerateIterable(generator.Iter.GetExprValue(tempFrame)) ?? throw new PyRuntimeException(PyVirtualMachine.CurrentException!);
            foreach (var item in iter)
            {
                generator.Target.SetTargetValue(item.PyThrowIfNull(), tempFrame);
                var shouldContinue = false;
                foreach (var test in generator.Ifs)
                {
                    if (!test.GetBoolValue(tempFrame))
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
                    list.Add(Elt.GetExprValue(tempFrame));
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

    public override PyObject ExecuteExpr(PyFrame frame)
    {
        var tempFrame = frame.TempFrame(FrameType.Comprehension);
        var list = new List<PyObject>();
        For(0);
        return new PySetObject(list);

        void For(int index)
        {
            var generator = Generators[index];
            var iter = Utils.EnumerateIterable(generator.Iter.GetExprValue(tempFrame)) ?? throw new PyRuntimeException(PyVirtualMachine.CurrentException!);
            foreach (var item in iter)
            {
                generator.Target.SetTargetValue(item.PyThrowIfNull(), tempFrame);
                var shouldContinue = false;
                foreach (var test in generator.Ifs)
                {
                    if (!test.GetBoolValue(tempFrame))
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
                    list.Add(Elt.GetExprValue(tempFrame));
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

    public override PyObject ExecuteExpr(PyFrame frame)
    {
        var tempFrame = frame.TempFrame(FrameType.Comprehension);
        var list = new List<KeyValuePair<PyObject, PyObject>>();
        For(0);
        return new PyDictObject(list);

        void For(int index)
        {
            var generator = Generators[index];
            var iter = Utils.EnumerateIterable(generator.Iter.GetExprValue(tempFrame)) ?? throw new PyRuntimeException(PyVirtualMachine.CurrentException!);
            foreach (var item in iter)
            {
                generator.Target.SetTargetValue(item.PyThrowIfNull(), tempFrame);
                var shouldContinue = false;
                foreach (var test in generator.Ifs)
                {
                    if (!test.GetBoolValue(tempFrame))
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
                    list.Add(KeyValuePair.Create(Key.GetExprValue(tempFrame), Value.GetExprValue(tempFrame)));
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

    public override PyObject ExecuteExpr(PyFrame frame)
    {
        var tempFrame = frame.TempFrame(FrameType.Comprehension);
        var list = new List<PyObject>();
        For(0);
        return PyTupleObject.CreateTuple(list);

        void For(int index)
        {
            var generator = Generators[index];
            var iter = Utils.EnumerateIterable(generator.Iter.GetExprValue(tempFrame)) ?? throw new PyRuntimeException(PyVirtualMachine.CurrentException!);
            foreach (var item in iter)
            {
                generator.Target.SetTargetValue(item.PyThrowIfNull(), tempFrame);
                var shouldContinue = false;
                foreach (var test in generator.Ifs)
                {
                    if (!test.GetBoolValue(tempFrame))
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
                    list.Add(Elt.GetExprValue(tempFrame));
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

    public override PyObject ExecuteExpr(PyFrame frame)
    {
        var caller = new FunctionCaller(this, frame, Body.GetExprValue);
        var func = new PyFunctionObject("<lambda>", caller.Call, frame.InternalClosure?.Values, frame._globals);
        caller._func = func;
        return func;
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

    public override PyObject ExecuteExpr(PyFrame frame)
    {
        var builder = new StringBuilder();

        foreach (var expr in Values)
        {
            if (!PySpecialMethods.TryGetStr(expr.GetExprValue(frame), out var s))
            {
                Debug.Assert(PyVirtualMachine.CurrentException is not null);
                throw new PyRuntimeException(PyVirtualMachine.CurrentException);
            }

            builder.Append(s.Value);
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

    public override PyObject ExecuteExpr(PyFrame frame)
    {
        if (Conversion is not -1 || FormatSpec is not null)
            throw new NotImplementedException();

        return Value.GetExprValue(frame);
    }
}