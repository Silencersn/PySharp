using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.Comparison;
using PySharp.Runtime.PyAttributes;
using System.Numerics;

namespace PySharp.Modules.Builtins;

public class PySliceObject : PyObject
{
    public PyObject Start { get; }
    public PyObject Stop { get; }
    public PyObject Step { get; }

    public override PyTypeObject DefaultPyType => PySliceObjectType.Shared;

    public PySliceObject(PyObject start, PyObject stop, PyObject step)
    {
        ArgumentNullException.ThrowIfNull(start);
        ArgumentNullException.ThrowIfNull(stop);
        ArgumentNullException.ThrowIfNull(step);
        Start = start;
        Stop = stop;
        Step = step;
    }

    // _PyEval_SliceIndex: the boundary must support __index__; failures of
    // the protocol itself (e.g. a non-int result) propagate unchanged while
    // other objects get the slice-specific TypeError
    private static PyResult<PyIntObject> SliceIndex(PyCallContext context, PyObject boundary)
    {
        if (boundary is PyIntObject intObj)
            return intObj;
        if (boundary.PyType.Slots.Index is null)
            return PyResult.TypeError(PySR.Runtime_Slice_IndicesMustBeInt);
        return PySpecialMethods.Index(context, boundary);
    }

    [AIGenerated]
    public PyResult Indices(PyCallContext context, int length, out (int start, int stop, int step, int sliceLength) indices)
    {
        indices = default;

        // PySlice_Unpack order: step first (zero rejected before the bounds
        // are touched), then start, then stop. None is special-cased before
        // conversion; out-of-range values saturate like
        // PyNumber_AsSsize_t(v, NULL) ahead of the length clamp.
        int step;
        if (Step is PyNoneObject)
        {
            step = 1;
        }
        else
        {
            var stepResult = SliceIndex(context, Step);
            if (stepResult.IsError)
                return stepResult;
            var value = stepResult.Value.Value;
            step = value > int.MaxValue ? int.MaxValue : value < int.MinValue ? int.MinValue : (int)value;
            if (step is 0)
                return PyResult.ValueError("slice step cannot be zero");
        }

        int start;
        if (Start is PyNoneObject)
        {
            start = step > 0 ? 0 : length - 1;
        }
        else
        {
            var startResult = SliceIndex(context, Start);
            if (startResult.IsError)
                return startResult;
            var value = startResult.Value.Value;
            start = PyUtils.MapIndex(value > int.MaxValue ? int.MaxValue : value < int.MinValue ? int.MinValue : (int)value, length);
        }

        int stop;
        if (Stop is PyNoneObject)
        {
            stop = step > 0 ? length : -1;
        }
        else
        {
            var stopResult = SliceIndex(context, Stop);
            if (stopResult.IsError)
                return stopResult;
            var value = stopResult.Value.Value;
            stop = PyUtils.MapIndex(value > int.MaxValue ? int.MaxValue : value < int.MinValue ? int.MinValue : (int)value, length);
        }

        if (step > 0)
        {
            start = Math.Clamp(start, 0, length);
            stop = Math.Clamp(stop, 0, length);
        }
        else
        {
            start = Math.Clamp(start, -1, length - 1);
            stop = Math.Clamp(stop, -1, length - 1);
        }

        int sliceLength = 0;
        // 64-bit intermediate: a saturated step can overflow the 32-bit sum
        if (step > 0 && start < stop)
            sliceLength = (int)(((long)stop - start + step - 1) / step);
        else if (step < 0 && start > stop)
            sliceLength = (int)(((long)stop - start + step + 1) / step);

        indices = (start, stop, step, sliceLength);
        return default;
    }

    // BigInteger variant for sequences whose length may exceed int (e.g. range);
    // boundaries keep full precision because the length clamp handles any magnitude
    public PyResult Indices(PyCallContext context, BigInteger length, out (BigInteger start, BigInteger stop, BigInteger step, BigInteger sliceLength) indices)
    {
        indices = default;

        BigInteger step;
        if (Step is PyNoneObject)
        {
            step = BigInteger.One;
        }
        else
        {
            var stepResult = SliceIndex(context, Step);
            if (stepResult.IsError)
                return stepResult;
            step = stepResult.Value.Value;
            if (step.IsZero)
                return PyResult.ValueError("slice step cannot be zero");
        }

        BigInteger start;
        if (Start is PyNoneObject)
        {
            start = step > 0 ? BigInteger.Zero : length - 1;
        }
        else
        {
            var startResult = SliceIndex(context, Start);
            if (startResult.IsError)
                return startResult;
            start = PyUtils.MapIndex(startResult.Value.Value, length);
        }

        BigInteger stop;
        if (Stop is PyNoneObject)
        {
            stop = step > 0 ? length : BigInteger.MinusOne;
        }
        else
        {
            var stopResult = SliceIndex(context, Stop);
            if (stopResult.IsError)
                return stopResult;
            stop = PyUtils.MapIndex(stopResult.Value.Value, length);
        }

        if (step > 0)
        {
            start = BigInteger.Clamp(start, BigInteger.Zero, length);
            stop = BigInteger.Clamp(stop, BigInteger.Zero, length);
        }
        else
        {
            start = BigInteger.Clamp(start, BigInteger.MinusOne, length - 1);
            stop = BigInteger.Clamp(stop, BigInteger.MinusOne, length - 1);
        }

        BigInteger sliceLength = 0;
        if (step > 0 && start < stop)
            sliceLength = (stop - start + step - 1) / step;
        else if (step < 0 && start > stop)
            sliceLength = (stop - start + step + 1) / step;

        indices = (start, stop, step, sliceLength);
        return default;
    }
}

[PyType("slice")]
public sealed partial class PySliceObjectType : PyTypeObject<PySliceObject>
{
    [PyExport(PySpecialNames.New, nameof(NewImpl_1), nameof(NewImpl_2))]
    private static partial PyBuiltinFunctionOrMethodObject _new { get; }

    [PyFunctionParameters("stop", "/")]
    private static PyResult NewImpl_1(PyCallContext context, PyArguments arguments)
    {
        return new PySliceObject(PyNoneObject.None, arguments[0], PyNoneObject.None);
    }

    [PyFunctionParameters("start", "stop", "step=None", "/")]
    private static PyResult NewImpl_2(PyCallContext context, PyArguments arguments)
    {
        return new PySliceObject(arguments[0], arguments[1], arguments[2]);
    }

    protected override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        var obj = _new.Call(context, args, kwargs);
        if (obj.IsError)
            return obj;
        obj.Value._pyType = cls;
        return obj;
    }

    protected override PyResult Repr(PyCallContext context, PySliceObject self)
    {
        // slice_repr: the constructor form with all three parts explicit
        var start = PySpecialMethods.Repr(context, self.Start);
        if (start.IsError)
            return start;
        var stop = PySpecialMethods.Repr(context, self.Stop);
        if (stop.IsError)
            return stop;
        var step = PySpecialMethods.Repr(context, self.Step);
        if (step.IsError)
            return step;

        return PyStrObject.FromString($"slice({start.Value.Value}, {stop.Value.Value}, {step.Value.Value})");
    }

    protected override PyResult Eq(PyCallContext context, PySliceObject self, PyObject other)
    {
        if (other is not PySliceObject otherSlice)
            return PyNotImplementedObject.NotImplemented;

        // CPython slice_richcompare: the parts are compared as the tuple
        // (start, stop, step) with each part's own equality.
        if (ReferenceEquals(self, otherSlice))
            return PyBoolObject.True;

        foreach (var (left, right) in new[] { (self.Start, otherSlice.Start), (self.Stop, otherSlice.Stop), (self.Step, otherSlice.Step) })
        {
            var eq = PyOperators.Eq(context, left, right);
            if (eq.IsError)
                return eq;

            var b = PySpecialMethods.Bool(context, eq.Value);
            if (b.IsError)
                return b;

            if (!b.Value.BoolValue)
                return PyBoolObject.False;
        }

        return PyBoolObject.True;
    }

    // Order comparisons delegate to the tuple (start, stop, step) like
    // slice_richcompare; the identity shortcut makes LE/GE true and
    // LT/GT false without consulting the parts
    protected override PyResult Lt(PyCallContext context, PySliceObject self, PyObject other)
        => RichCompare(context, self, other, PyCollectionComparer.Lt, referenceResult: PyBoolObject.False);
    protected override PyResult Le(PyCallContext context, PySliceObject self, PyObject other)
        => RichCompare(context, self, other, PyCollectionComparer.Le, referenceResult: PyBoolObject.True);
    protected override PyResult Gt(PyCallContext context, PySliceObject self, PyObject other)
        => RichCompare(context, self, other, PyCollectionComparer.Gt, referenceResult: PyBoolObject.False);
    protected override PyResult Ge(PyCallContext context, PySliceObject self, PyObject other)
        => RichCompare(context, self, other, PyCollectionComparer.Ge, referenceResult: PyBoolObject.True);

    private static PyResult RichCompare(PyCallContext context, PySliceObject self, PyObject other, Func<PyCallContext, ReadOnlySpan<PyObject>, ReadOnlySpan<PyObject>, PyResult> compare, PyBoolObject referenceResult)
    {
        if (other is not PySliceObject)
            return PyNotImplementedObject.NotImplemented;

        if (ReferenceEquals(self, other))
            return referenceResult;

        ReadOnlySpan<PyObject> left = [self.Start, self.Stop, self.Step];
        ReadOnlySpan<PyObject> right = [((PySliceObject)other).Start, ((PySliceObject)other).Stop, ((PySliceObject)other).Step];
        return compare(context, left, right);
    }

    protected override PyResult Hash(PyCallContext context, PySliceObject self)
    {
        // CPython slice_hash: the tuplehash lane combination without the
        // length mix-in, so hash(slice) never equals hash(tuple) of the
        // same parts
        unchecked
        {
            ulong acc = 2870177450012600261; // _PyHASH_XXPRIME_5
            foreach (var part in new[] { self.Start, self.Stop, self.Step })
            {
                var laneResult = PySpecialMethods.Hash(context, part);
                if (laneResult.IsError)
                    return laneResult;

                acc += (ulong)(long)laneResult.Value.Value * 14029467366897019727; // _PyHASH_XXPRIME_2
                acc = (acc << 31) | (acc >> 33); // _PyHASH_XXROTATE
                acc *= 11400714785074694791; // _PyHASH_XXPRIME_1
            }

            if (acc is ulong.MaxValue)
                acc = 1546275796;

            return PyIntObject.FromInteger((long)acc);
        }
    }

    [PyMethod("indices")]
    [PyFunctionParameters("*args", "**kwargs")]
    private static PyResult Indices(PyCallContext context, PySliceObject self, PyArguments arguments)
    {
        // CPython's METH_O binding reports the keyword and arity
        // rejections before slice_indices converts anything
        if (arguments.ExtraKwargs.Count > 0)
            return PyResult.TypeError(PySR.Runtime_Slice_IndicesNoKwargs);

        var count = arguments.Args.Length + arguments.ExtraArgs.Count;
        if (count is not 1)
            return PyResult.TypeError(PySR.Runtime_Slice_IndicesOneArgument, count);

        // slice_indices: the length goes through PyNumber_Index (strict
        // __index__, no saturation) and rejects negatives; the parts then
        // convert with full precision (_PySlice_GetLongIndices)
        var lengthResult = PySpecialMethods.Index(context, arguments[0]);
        if (lengthResult.IsError)
            return lengthResult;

        if (lengthResult.Value.Value.Sign < 0)
            return PyResult.ValueError(PySR.Runtime_Slice_LengthShouldNotBeNegative);

        var indicesResult = self.Indices(context, lengthResult.Value.Value, out var indices);
        if (indicesResult.IsError)
            return indicesResult;

        return PyTupleObject.CreateTuple(PyIntObject.FromInteger(indices.start), PyIntObject.FromInteger(indices.stop), PyIntObject.FromInteger(indices.step));
    }

    [PyProperty("start")]
    private static PyResult Get_Start(PyCallContext context, PySliceObject self)
    {
        return self.Start;
    }
    [PyProperty("stop")]
    private static PyResult Get_Stop(PyCallContext context, PySliceObject self)
    {
        return self.Stop;
    }
    [PyProperty("step")]
    private static PyResult Get_Step(PyCallContext context, PySliceObject self)
    {
        return self.Step;
    }
}
