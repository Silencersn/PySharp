using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;
using PySharp.PyRuntime.PyAttributes;

namespace PySharp.PyModules.Builtins;

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
}

public sealed class PySliceObjectType : PyTypeObject<PySliceObjectType, PySliceObject>
{
    public override string Name => "slice";

    public PySliceObjectType()
    {
        AppendMemberDescriptor<PySliceObject>("start", static slice => slice.Start);
        AppendMemberDescriptor<PySliceObject>("stop", static slice => slice.Stop);
        AppendMemberDescriptor<PySliceObject>("step", static slice => slice.Step);
    }

    private static readonly PyBuiltinFunctionOrMethodObject2 _new = new(PySpecialNames.New, NewImpl_1, NewImpl_2);

    [PyFunctionArgsDef("stop", "/")]
    private static PyResult NewImpl_1(PyCallContext context, PyArguments arguments)
    {
        return new PySliceObject(PyNoneObject.None, arguments[0], PyNoneObject.None);
    }

    [PyFunctionArgsDef("start", "stop", "step=None", "/")]
    private static PyResult NewImpl_2(PyCallContext context, PyArguments arguments)
    {
        return new PySliceObject(arguments[0], arguments[1], arguments[2]);
    }

    protected internal override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        var obj = _new.Call(args, kwargs);
        if (obj is null)
            return PyResult.CaptureExceptionFromPVM();
        obj._pyType = cls;
        return obj;
    }
}