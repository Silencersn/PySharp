using PySharp.PyRuntime;

namespace PySharp.PyObjects.Builtins;

public class PyNoneObject : PyObject
{
    public static PyNoneObject None { get; } = new PyNoneObject();
    private static readonly PyStrObject _repr = PyStrObject.FromString("None");

    public override PyStrObject Repr()
    {
        return _repr;
    }

    public override PyBoolObject Bool()
    {
        return false;
    }
}

public sealed class PyNoneObjectType : PyTypeObject
{
    public override string Name => "NoneType";

    public override PyObject? New(IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        var pack = new PyArgsPack(args, kwargs);
        if (!pack.ValidateEmpty())
            return PyVirtualMachine.RaiseTypeError(null);

        return PyNoneObject.None;
    }
}