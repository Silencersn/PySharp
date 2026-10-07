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

    // stage-3 pilot of the C-form middle layer. The double-sided guard is
    // load-bearing on the inheritance-sensitive path: when bool is the
    // right operand of a subclass-ordered operation (e.g. `2 & True`), the
    // reflected slot runs with the LEFT int operand in the self position,
    // and declining with NotImplemented hands control to the left type's
    // forward slot — the CPython slot convention expressed verbatim
    protected internal override PyResult NbAnd(PyCallContext context, PyObject self, PyObject other)
    {
        if (self is not PyBoolObject boolSelf)
            return PyNotImplementedObject.NotImplemented;

        // CPython boolobject.c: bitwise ops keep bool only when both operands
        // are bool; otherwise they fall through to the int slots (int), so the
        // int slot's body is inlined for the non-bool case.
        if (other is PyBoolObject otherBool)
            return PyBoolObject.FromBoolean(boolSelf.BoolValue & otherBool.BoolValue);
        if (other is PyIntObject intObj)
            return PyMath.CalculatePyIntObject(context, PyOperatorTypes.BitAnd, boolSelf, intObj);
        return PyNotImplementedObject.NotImplemented;
    }

    protected internal override PyResult NbXor(PyCallContext context, PyObject self, PyObject other)
    {
        if (self is not PyBoolObject boolSelf)
            return PyNotImplementedObject.NotImplemented;

        if (other is PyBoolObject otherBool)
            return PyBoolObject.FromBoolean(boolSelf.BoolValue ^ otherBool.BoolValue);
        if (other is PyIntObject intObj)
            return PyMath.CalculatePyIntObject(context, PyOperatorTypes.BitXor, boolSelf, intObj);
        return PyNotImplementedObject.NotImplemented;
    }

    protected internal override PyResult NbOr(PyCallContext context, PyObject self, PyObject other)
    {
        if (self is not PyBoolObject boolSelf)
            return PyNotImplementedObject.NotImplemented;

        if (other is PyBoolObject otherBool)
            return PyBoolObject.FromBoolean(boolSelf.BoolValue | otherBool.BoolValue);
        if (other is PyIntObject intObj)
            return PyMath.CalculatePyIntObject(context, PyOperatorTypes.BitOr, boolSelf, intObj);
        return PyNotImplementedObject.NotImplemented;
    }

    // rewires the three bitwise slots onto the C-form entries; the
    // inherited arithmetic slots (Add/Sub/... off int) stay untouched, so
    // IsInheritedForwardSlot keeps skipping their synthesis and bool keeps
    // picking int's reflected wrappers through the MRO
    protected override void PostConstruct()
    {
        base.PostConstruct();

        var number = Slots.Number!;
        PyBinaryFunction and = NbAnd;
        number.And = and;
        number.RAnd = and;
        PyAttributes[PySpecialNames.And] = new PyWrapperDescriptorObject(and);
        PyBinaryFunction xor = NbXor;
        number.Xor = xor;
        number.RXor = xor;
        PyAttributes[PySpecialNames.Xor] = new PyWrapperDescriptorObject(xor);
        PyBinaryFunction or = NbOr;
        number.Or = or;
        number.ROr = or;
        PyAttributes[PySpecialNames.Or] = new PyWrapperDescriptorObject(or);

        PyAttributes[PySpecialNames.RAnd] = new PyWrapperDescriptorObject(ReflectedWrapper(NbAnd));
        PyAttributes[PySpecialNames.RXor] = new PyWrapperDescriptorObject(ReflectedWrapper(NbXor));
        PyAttributes[PySpecialNames.ROr] = new PyWrapperDescriptorObject(ReflectedWrapper(NbOr));
    }

    private static PyBinaryFunction ReflectedWrapper(PyBinaryFunction entry)
        => (context, self, other) => other is PyBoolObject
            ? entry(context, other, self)
            : PyNotImplementedObject.NotImplemented;

    // CPython bool has no nb_positive of its own: the inherited int slot
    // returns its exact receiver, which for bool upgrades to int 1/0.
    protected override PyResult Pos(PyCallContext context, PyBoolObject self)
    {
        return PyIntObject.FromInteger(self.Value);
    }

    // CPython bool_invert warns before delegating to the int slot
    // (Objects/boolobject.c: the deprecation is scheduled for removal in
    // 3.16), so the warning fires on every '~' reaching this slot.
    protected override PyResult Invert(PyCallContext context, PyBoolObject self)
    {
        var warnResult = context.Warn(PyDeprecationWarningObjectType.Shared, PySR.Runtime_Bool_InvertDeprecated);
        if (warnResult.IsError)
            return warnResult;

        return PyIntObject.FromInteger(~self.Value);
    }
}
