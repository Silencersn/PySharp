using PySharp.PyRuntime;

namespace PySharp.PyObjects.Builtins;

public class PyNoneObject : PyObject
{
    public static PyNoneObject None { get; } = new PyNoneObject();

    public override PyStrObject Repr()
    {
        return "None";
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