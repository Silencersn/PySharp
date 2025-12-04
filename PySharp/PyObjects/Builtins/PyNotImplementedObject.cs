using PySharp.PyRuntime;

namespace PySharp.PyObjects.Builtins;

public class PyNotImplementedObject : PyObject
{
    public static PyNotImplementedObject NotImplemented { get; } = new PyNotImplementedObject();
    private static readonly PyStrObject _repr = PyStrObject.FromString("NotImplemented");

    public override PyStrObject Repr()
    {
        return _repr;
    }
}

public sealed class PyNotImplementedObjectType : PyTypeObject
{
    public override string Name => "NotImplementedType";

    public override PyObject? New(IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        var pack = new PyArgsPack(args, kwargs);
        if (!pack.ValidateEmpty())
            return PyVirtualMachine.RaiseTypeError(null);

        return PyNotImplementedObject.NotImplemented;
    }
}