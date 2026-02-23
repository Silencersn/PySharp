using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;

namespace PySharp.Modules.Builtins;

public sealed class PyEllipsisObject : PyObject
{
    public static PyEllipsisObject Ellipsis { get; } = new PyEllipsisObject();
    public override PyTypeObject DefaultPyType => PyEllipsisObjectType.Shared;
    private PyEllipsisObject() { }
}

[PyType("ellipsis")]
public sealed partial class PyEllipsisObjectType : PyTypeObject<PyEllipsisObjectType, PyEllipsisObject>
{
    public override bool IsSealed => true;
    private static readonly PyStrObject _repr = PyStrObject.FromString("Ellipsis");

    protected override PyResult Repr(PyCallContext context, PyEllipsisObject self)
    {
        return _repr;
    }

    protected override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        if (!PyArgsValidator.ValidateEmpty(args, kwargs, out var err))
            return err.Value;
        return PyEllipsisObject.Ellipsis;
    }
}