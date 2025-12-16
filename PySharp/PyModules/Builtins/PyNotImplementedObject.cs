using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;

namespace PySharp.PyModules.Builtins;

public class PyNotImplementedObject : PyObject
{
    public static PyNotImplementedObject NotImplemented { get; } = new PyNotImplementedObject();
    private static readonly PyStrObject _repr = PyStrObject.FromString("NotImplemented");

    public override PyTypeObject DefaultPyType => PyNotImplementedObjectType.Shared;

	protected internal override PyStrObject ReprImpl()
    {
        return _repr;
    }
}

public sealed class PyNotImplementedObjectType : PyPrimitiveTypeObject<PyNotImplementedObjectType, PyNotImplementedObject>
{
    public override string Name => "NotImplementedType";
    public override bool IsSealed => true;

    protected internal override PyObject? New(PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        var pack = new PyArgsPack(args, kwargs);
        if (!pack.ValidateEmpty())
            return PyVirtualMachine.RaiseTypeError(null);

        return PyNotImplementedObject.NotImplemented;
    }
}