using PySharp.PyRuntime;

namespace PySharp.PyObjects.Builtins;

public sealed class PyEllipsisObject : PyObject
{
    public static PyEllipsisObject Ellipsis { get; } = new PyEllipsisObject();
    private static readonly PyStrObject _repr = PyStrObject.FromString("Ellipsis");

    public override PyTypeObject PyType => PyBuiltinTypes.Ellipsis;

    public override PyObject? Repr()
    {
        return _repr;
    }
}

public sealed class PyEllipsisObjectType : PyTypeObject
{
    public override string Name => "ellipsis";

    public override PyObject? New(IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        var pack = new PyArgsPack(args, kwargs);
        if (!pack.ValidateEmpty())
            return PyVirtualMachine.RaiseTypeError(null);

        return PyEllipsisObject.Ellipsis;
    }
}