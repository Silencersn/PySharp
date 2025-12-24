using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;

namespace PySharp.PyModules.Builtins;

public sealed class PyBoolObject : PyIntObject
{
    public static PyBoolObject True { get; } = new PyBoolObject(true);
    public static PyBoolObject False { get; } = new PyBoolObject(false);

    public bool BoolValue { get; }
    internal readonly PyStrObject _repr;

    public override PyTypeObject DefaultPyType => PyBoolObjectType.Shared;

    private PyBoolObject(bool value) : base(value ? 1 : 0)
    {
        BoolValue = value;
        _repr = PyStrObject.FromString(value ? "True" : "False");
    }

    public static PyBoolObject FromBoolean(bool value)
    {
        return value ? True : False;
    }
}

public sealed class PyBoolObjectType : PyTypeObject<PyBoolObjectType, PyBoolObject>
{
    public override string Name => "bool";
    public override bool IsSealed => true;
    public override IReadOnlyList<PyTypeObject> Bases => [PyIntObjectType.Shared];

    protected internal override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        if (!PyArgsValidator.ValidateSinglePositionalArg(args, kwargs, out var err))
            return err.Value;
        return PySpecialMethods.GetBool(context, args[0]);
    }

    protected internal override PyResult Repr(PyCallContext context, PyBoolObject self)
    {
        return self._repr;
    }

    protected internal override PyResult Bool(PyCallContext context, PyBoolObject self)
    {
        return self;
    }
}