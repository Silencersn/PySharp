using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;

namespace PySharp.PyModules.Builtins;

public class PyNoneObject : PyObject
{
    public static PyNoneObject None { get; } = new PyNoneObject();
    private static readonly PyStrObject _repr = PyStrObject.FromString("None");

    public override PyTypeObject DefaultPyType => PyNoneObjectType.Shared;

    public override PyStrObject Repr()
    {
        return _repr;
    }

    public override PyBoolObject Bool()
    {
        return false;
    }
}

public sealed class PyNoneObjectType : PyPrimitiveTypeObject<PyNoneObjectType, PyNoneObject>
{
    public override string Name => "NoneType";

    public override PyObject? New(PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        var pack = new PyArgsPack(args, kwargs);
        if (!pack.ValidateEmpty())
            return PyVirtualMachine.RaiseTypeError(null);

        return PyNoneObject.None;
    }
}