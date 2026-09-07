using PySharp.Runtime;
using PySharp.Runtime.Calls;
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
