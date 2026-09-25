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

[PyType("bool", IsSealed = true)]
public sealed partial class PyBoolObjectType : PyTypeObject<PyBoolObject>
{
    public override IReadOnlyList<PyTypeObject> Bases => [PyIntObjectType.Shared];

    protected override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        // CPython bool_new: x defaults to Py_False, so no argument at all
        // yields False; keyword arguments are rejected before the arity
        // check, and more than one positional is an error (Objects/boolobject.c)
        if (kwargs.Count > 0)
            return PyResult.TypeError(PySR.Runtime_Bool_TakesNoKwargs);
        if (args.Count > 1)
            return PyResult.TypeError(PySR.Runtime_Bool_ExpectedAtMostOne, args.Count);

        if (args.Count is 0)
            return PyBoolObject.False;

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

    // CPython boolobject.c: bitwise ops keep bool only when both operands
    // are bool; otherwise they fall through to the int slots (int), so the
    // int slot's body is inlined for the non-bool case (base.And would be
    // the generic NotImplemented fallback, not int's implementation).
    protected override PyResult And(PyCallContext context, PyBoolObject self, PyObject other)
    {
        if (other is PyBoolObject otherBool)
            return PyBoolObject.FromBoolean(self.BoolValue & otherBool.BoolValue);
        if (other is PyIntObject intObj)
            return PyMath.CalculatePyIntObject(PyOperatorTypes.BitAnd, self, intObj);
        return base.And(context, self, other);
    }

    protected override PyResult Xor(PyCallContext context, PyBoolObject self, PyObject other)
    {
        if (other is PyBoolObject otherBool)
            return PyBoolObject.FromBoolean(self.BoolValue ^ otherBool.BoolValue);
        if (other is PyIntObject intObj)
            return PyMath.CalculatePyIntObject(PyOperatorTypes.BitXor, self, intObj);
        return base.Xor(context, self, other);
    }

    protected override PyResult Or(PyCallContext context, PyBoolObject self, PyObject other)
    {
        if (other is PyBoolObject otherBool)
            return PyBoolObject.FromBoolean(self.BoolValue | otherBool.BoolValue);
        if (other is PyIntObject intObj)
            return PyMath.CalculatePyIntObject(PyOperatorTypes.BitOr, self, intObj);
        return base.Or(context, self, other);
    }

    // CPython bool has no nb_positive of its own: the inherited int slot
    // returns its exact receiver, which for bool upgrades to int 1/0.
    protected override PyResult Pos(PyCallContext context, PyBoolObject self)
    {
        return PyIntObject.FromInteger(self.Value);
    }
}
