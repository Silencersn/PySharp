using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;

namespace PySharp.PyModules.Builtins;

public class PyNoneObject : PyObject
{
    public static PyNoneObject None { get; } = new PyNoneObject();
    private static readonly PyStrObject _repr = PyStrObject.FromString("None");

    public override PyTypeObject DefaultPyType => PyNoneObjectType.Shared;

    protected internal override PyStrObject ReprImpl()
    {
        return _repr;
    }

    protected internal override PyBoolObject BoolImpl()
    {
        return false;
    }
}

public sealed class PyNoneObjectType : PyPrimitiveTypeObject<PyNoneObjectType, PyNoneObject>
{
    public override string Name => "NoneType";
    public override bool IsSealed => true;

    protected internal override PyObject? New(PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        var pack = new PyArgsPack(args, kwargs);
        if (!pack.ValidateEmpty())
            return PyVirtualMachine.RaiseTypeError(null);

        return PyNoneObject.None;
    }
}