using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;

namespace PySharp.PyModules.Builtins;

public sealed class PyBoolObject : PyIntObject
{
    public static PyBoolObject True { get; } = new PyBoolObject(true);
    public static PyBoolObject False { get; } = new PyBoolObject(false);

    public bool BoolValue { get; }
    private readonly PyStrObject _repr;

    public override PyTypeObject DefaultPyType => PyBoolObjectType.Shared;

    private PyBoolObject(bool value) : base(value ? 1 : 0)
    {
        BoolValue = value;
        _repr = PyStrObject.FromString(value ? "True" : "False");
    }

    public static implicit operator PyBoolObject(bool value)
    {
        return FromBoolean(value);
    }

    public static PyBoolObject FromBoolean(bool value)
    {
        return value ? True : False;
    }

    public override PyStrObject Repr()
    {
        return _repr;
    }

    public override PyBoolObject Bool()
    {
        return this;
    }
}

public sealed class PyBoolObjectType : PyPrimitiveTypeObject<PyBoolObjectType, PyBoolObject>
{
    public override string Name => "bool";
    public override bool IsSealed => true;
    public override IReadOnlyList<PyTypeObject> Bases => [PyIntObjectType.Shared];

    public override PyObject? New(PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        var pack = new PyArgsPack(args, kwargs);
        if (!pack.ValidateCount(1, 0))
            return PyVirtualMachine.RaiseTypeError(null);

        return PySpecialMethods.GetBool(pack[0]);
    }
}