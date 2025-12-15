using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;
using PySharp.PyRuntime.PyAttributes;

namespace PySharp.PyModules.Builtins;

public class PySliceObject : PyObject
{
    public PyObject Start { get; }
    public PyObject Stop { get; }
    public PyObject Step { get; }

    public override PyTypeObject PyType => PySliceObjectType.Shared;

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

public sealed class PySliceObjectType : PyPrimitiveTypeObject<PySliceObjectType, PySliceObject>
{
    public override string Name => "slice";

    public PySliceObjectType()
    {
        AppendMemberDescriptor<PySliceObject>("start", static slice => slice.Start);
        AppendMemberDescriptor<PySliceObject>("stop", static slice => slice.Stop);
        AppendMemberDescriptor<PySliceObject>("step", static slice => slice.Step);
    }

    private static readonly PyBuiltinFunctionOrMethodObject _new = new(PySpecialNames.New, NewImpl_1, NewImpl_2);

    [PyFunctionArgsDef("stop", "/")]
    private static PySliceObject NewImpl_1(PyArguments arguments)
    {
        return new PySliceObject(PyNoneObject.None, arguments[0], PyNoneObject.None);
    }

    [PyFunctionArgsDef("start", "stop", "step=None", "/")]
    private static PySliceObject NewImpl_2(PyArguments arguments)
    {
        return new PySliceObject(arguments[0], arguments[1], arguments[2]);
    }

    public override PyObject? New(PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        return _new.Call(args, kwargs);
    }

}