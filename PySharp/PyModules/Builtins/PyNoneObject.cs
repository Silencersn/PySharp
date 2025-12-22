using PySharp.PyRuntime.Calls;

namespace PySharp.PyModules.Builtins;

public class PyNoneObject : PyObject
{
    public static PyNoneObject None { get; } = new PyNoneObject();
    public override PyTypeObject DefaultPyType => PyNoneObjectType.Shared;
    private PyNoneObject() { }
}

public sealed class PyNoneObjectType : PyTypeObject<PyNoneObjectType, PyNoneObject>
{
    public override string Name => "NoneType";
    public override bool IsSealed => true;
    private static readonly PyStrObject _repr = PyStrObject.FromString("None");

    protected internal override PyResult Repr(PyCallContext context, PyNoneObject self)
    {
        return _repr;
    }

    protected internal override PyResult Bool(PyCallContext context, PyNoneObject self)
    {
        return PyBoolObject.False;
    }

    protected internal override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        var pack = new PyArgsPack(args, kwargs);
        if (!pack.ValidateEmpty())
            return PyResult.RaiseTypeError(null);
        return PyNoneObject.None;
    }
}