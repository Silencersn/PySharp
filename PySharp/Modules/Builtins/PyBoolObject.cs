using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;

namespace PySharp.Modules.Builtins;

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

[PyType("bool")]
public sealed partial class PyBoolObjectType : PyTypeObject<PyBoolObjectType, PyBoolObject>
{
    public override bool IsSealed => true;
    public override IReadOnlyList<PyTypeObject> Bases => [PyIntObjectType.Shared];

    protected override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        if (!PyArgsValidator.ValidateSinglePositionalArg(args, kwargs, out var err))
            return err.Value;
        return PySpecialMethods.Bool(context, args[0]);
    }

    protected override PyResult Repr(PyCallContext context, PyBoolObject self)
    {
        return self._repr;
    }

    protected override PyResult Bool(PyCallContext context, PyBoolObject self)
    {
        return self;
    }
}