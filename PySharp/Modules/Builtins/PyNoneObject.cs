using PySharp.Runtime.Calls;

namespace PySharp.Modules.Builtins;

public class PyNoneObject : PyObject
{
    public static PyNoneObject None { get; } = new PyNoneObject();
    public override PyTypeObject DefaultPyType => PyNoneObjectType.Shared;
    private PyNoneObject() { }
}

public sealed class PyNoneObjectType : PyTypeObject<PyNoneObjectType, PyNoneObject>
{
    public override string Module => "builtins";
    public override string Name => "NoneType";
    public override bool IsSealed => true;
    private static readonly PyStrObject _repr = PyStrObject.FromString("None");

    protected override PyResult Repr(PyCallContext context, PyNoneObject self)
    {
        return _repr;
    }

    protected override PyResult Bool(PyCallContext context, PyNoneObject self)
    {
        return PyBoolObject.False;
    }

    protected override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        if (!PyArgsValidator.ValidateEmpty(args, kwargs, out var err))
            return err.Value;
        return PyNoneObject.None;
    }
}