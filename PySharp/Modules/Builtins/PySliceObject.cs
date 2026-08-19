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

    [AIGenerated]
    public PyResult Indices(PyCallContext context, int length, out (int start, int stop, int step, int sliceLength) indices)
    {
        indices = default;
        int step = Step is PyNoneObject ? 1 : ((PyIntObject)Step).Int32Value;
        int start, stop;

        if (step > 0)
        {
            start = Start is PyNoneObject ? 0 : PyUtils.MapIndex(((PyIntObject)Start).Int32Value, length);
            stop = Stop is PyNoneObject ? length : PyUtils.MapIndex(((PyIntObject)Stop).Int32Value, length);
            start = Math.Clamp(start, 0, length);
            stop = Math.Clamp(stop, 0, length);
        }
        else if (step < 0)
        {
            start = Start is PyNoneObject ? length - 1 : PyUtils.MapIndex(((PyIntObject)Start).Int32Value, length);
            stop = Stop is PyNoneObject ? -1 : PyUtils.MapIndex(((PyIntObject)Stop).Int32Value, length);
            start = Math.Clamp(start, -1, length - 1);
            stop = Math.Clamp(stop, -1, length - 1);
        }
        else
        {
            // CPython: ValueError("slice step cannot be zero"); returned as an
            // error result (no-throw Try pattern) so callers propagate it.
            return PyResult.ValueError("slice step cannot be zero");
        }

        int sliceLength = 0;
        if (step > 0 && start < stop)
            sliceLength = (stop - start + step - 1) / step;
        else if (step < 0 && start > stop)
            sliceLength = (stop - start + step + 1) / step;

        indices = (start, stop, step, sliceLength);
        return default;
    }

    // BigInteger variant for sequences whose length may exceed int (e.g. range)
    public PyResult Indices(PyCallContext context, BigInteger length, out (BigInteger start, BigInteger stop, BigInteger step, BigInteger sliceLength) indices)
    {
        indices = default;
        var step = Step is PyNoneObject ? BigInteger.One : ((PyIntObject)Step).Value;
        BigInteger start, stop;

        if (step > 0)
        {
            start = Start is PyNoneObject ? BigInteger.Zero : PyUtils.MapIndex(((PyIntObject)Start).Value, length);
            stop = Stop is PyNoneObject ? length : PyUtils.MapIndex(((PyIntObject)Stop).Value, length);
            start = BigInteger.Clamp(start, BigInteger.Zero, length);
            stop = BigInteger.Clamp(stop, BigInteger.Zero, length);
        }
        else if (step < 0)
        {
            start = Start is PyNoneObject ? length - 1 : PyUtils.MapIndex(((PyIntObject)Start).Value, length);
            stop = Stop is PyNoneObject ? BigInteger.MinusOne : PyUtils.MapIndex(((PyIntObject)Stop).Value, length);
            start = BigInteger.Clamp(start, BigInteger.MinusOne, length - 1);
            stop = BigInteger.Clamp(stop, BigInteger.MinusOne, length - 1);
        }
        else
        {
            // CPython: ValueError("slice step cannot be zero"); returned as an
            // error result (no-throw Try pattern) so callers propagate it.
            return PyResult.ValueError("slice step cannot be zero");
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
