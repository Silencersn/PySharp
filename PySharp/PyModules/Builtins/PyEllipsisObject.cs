using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;

namespace PySharp.PyModules.Builtins;

public sealed class PyEllipsisObject : PyObject
{
    public static PyEllipsisObject Ellipsis { get; } = new PyEllipsisObject();
    private static readonly PyStrObject _repr = PyStrObject.FromString("Ellipsis");

    public override PyTypeObject DefaultPyType => PyEllipsisObjectType.Shared;

    public override PyObject? Repr()
    {
        return _repr;
    }
}

public sealed class PyEllipsisObjectType : PyPrimitiveTypeObject<PyEllipsisObjectType, PyEllipsisObject>
{
    public override string Name => "ellipsis";

    public override PyObject? New(PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        var pack = new PyArgsPack(args, kwargs);
        if (!pack.ValidateEmpty())
            return PyVirtualMachine.RaiseTypeError(null);

        return PyEllipsisObject.Ellipsis;
    }
}