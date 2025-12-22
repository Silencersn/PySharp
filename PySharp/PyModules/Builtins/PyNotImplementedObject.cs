using PySharp.PyRuntime.Calls;

namespace PySharp.PyModules.Builtins;

public class PyNotImplementedObject : PyObject
{
    public static PyNotImplementedObject NotImplemented { get; } = new PyNotImplementedObject();
    public override PyTypeObject DefaultPyType => PyNotImplementedObjectType.Shared;
    private PyNotImplementedObject() { }
}

public sealed class PyNotImplementedObjectType : PyTypeObject<PyNotImplementedObjectType, PyNotImplementedObject>
{
    public override string Name => "NotImplementedType";
    public override bool IsSealed => true;
    private static readonly PyStrObject _repr = PyStrObject.FromString("NotImplemented");

    protected internal override PyResult Repr(PyCallContext context, PyNotImplementedObject self)
    {
        return _repr;
    }

    protected internal override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        var pack = new PyArgsPack(args, kwargs);
        if (!pack.ValidateEmpty())
            return PyResult.RaiseTypeError(null);
        return PyNotImplementedObject.NotImplemented;
    }
}