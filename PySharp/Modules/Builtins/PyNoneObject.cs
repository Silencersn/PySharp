using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;

namespace PySharp.Modules.Builtins;

public class PyNoneObject : PyObject
{
    public static PyNoneObject None { get; } = new PyNoneObject();
    public override PyTypeObject DefaultPyType => PyNoneObjectType.Shared;
    private PyNoneObject() { }
}

[PyType("NoneType", IsSealed = true)]
public sealed partial class PyNoneObjectType : PyTypeObject<PyNoneObjectType, PyNoneObject>
{
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