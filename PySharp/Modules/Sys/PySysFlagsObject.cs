using PySharp.Modules.Builtins;
using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;
using PySharp.Utility;

namespace PySharp.Modules.Sys;

// CPython's struct sequence sys.flags (Python/sysmodule.c set_flags_from_config):
// a tuple subclass of the command-line flag values this interpreter started
// with. PySharp runs with no flags on, so every int field is 0 except
// hash_randomization, the bool fields are False, and int_max_str_digits
// tracks the live limit
public sealed class PyFlagsObject : PyTupleObject, IPyObjectRecursiveRepr
{
    internal static readonly PyObject[] DefaultItems =
    [
        PyIntObject.Zero,                                  // debug
        PyIntObject.Zero,                                  // inspect
        PyIntObject.Zero,                                  // interactive
        PyIntObject.Zero,                                  // optimize
        PyIntObject.One,                                   // dont_write_bytecode — PySharp never writes bytecode
        PyIntObject.Zero,                                  // no_user_site
        PyIntObject.Zero,                                  // no_site
        PyIntObject.Zero,                                  // ignore_environment
        PyIntObject.Zero,                                  // verbose
        PyIntObject.Zero,                                  // bytes_warning
        PyIntObject.Zero,                                  // quiet
        PyIntObject.One,                                   // hash_randomization
        PyIntObject.Zero,                                  // isolated
        PyBoolObject.False,                                // dev_mode
        PyIntObject.Zero,                                  // utf8_mode
        PyIntObject.Zero,                                  // warn_default_encoding
        PyBoolObject.False,                                // safe_path
        PyIntObject.FromInteger(PyIntStrDigitsLimit.DefaultMaxStrDigits), // int_max_str_digits snapshot
    ];

    internal static PyFlagsObject Create()
    {
        return new PyFlagsObject();
    }

    private PyFlagsObject() : base(DefaultItems)
    {
        _pyType = PyFlagsObjectType.Shared;
    }

    public override PyTypeObject DefaultPyType => PyFlagsObjectType.Shared;

    PyResult<PyStrObject> IPyObjectRecursiveRepr.RecursiveRepr(PyCallContext context, HashSet<PyObject> ids)
    {
        var result = PySpecialMethods.Repr(context, this);
        return result.IsError ? result.ExceptionResult : PyResult<PyStrObject>.FromValue(result.Value);
    }
}

[PyType("sys.flags")]
public sealed partial class PyFlagsObjectType : PyTypeObject<PyFlagsObject>
{
    private static readonly string[] FieldNames =
    [
        "debug", "inspect", "interactive", "optimize", "dont_write_bytecode",
        "no_user_site", "no_site", "ignore_environment", "verbose",
        "bytes_warning", "quiet", "hash_randomization", "isolated",
        "dev_mode", "utf8_mode", "warn_default_encoding", "safe_path",
        "int_max_str_digits",
    ];

    // A tuple subclass: the MRO fallback supplies every tuple slot
    public sealed override IReadOnlyList<PyTypeObject> Bases => [PyTupleObjectType.Shared];

    // CPython structseq_new rejects direct instantiation
    protected override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        return PyResult.TypeError(PySR.Runtime_Sys_CannotCreateInstances, "sys.flags");
    }

    protected override void PostConstruct()
    {
        for (var index = 0; index < FieldNames.Length; index++)
            InstallField(FieldNames[index], index);
    }

    private void InstallField(string name, int index)
    {
        PyAttributes[name] = new PyMemberDescriptorObject(
            PyGetSetDescriptorObjectType.Shared,
            this,
            name,
            (context, instance) => Get_Field(context, index),
            setter: null,
            deleter: null);
    }

    // The int_max_str_digits field reads the live limit so a
    // set_int_max_str_digits call is reflected in sys.flags, matching
    // CPython's _PySys_SetFlagInt write-back
    private static PyResult Get_Field(PyCallContext context, int index)
    {
        if (index is 17)
            return PyIntObject.FromInteger(context.PyEnvironment.IntStrDigits.MaxStrDigits);
        return PyFlagsObject.DefaultItems[index];
    }

    // CPython structseq_repr: "sys.flags(debug=0, ..., int_max_str_digits=4300)"
    protected override PyResult Repr(PyCallContext context, PyFlagsObject self)
    {
        return StructSeqRepr(context, "sys.flags", FieldNames,
            index => index is 17
                ? PyIntObject.FromInteger(context.PyEnvironment.IntStrDigits.MaxStrDigits)
                : self.InternalArray[index]);
    }

    internal static PyResult StructSeqRepr(PyCallContext context, string typeName, string[] fieldNames, Func<int, PyObject> getField)
    {
        var builder = new System.Text.StringBuilder(typeName).Append('(');
        for (var index = 0; index < fieldNames.Length; index++)
        {
            if (index > 0)
                builder.Append(", ");
            var itemRepr = PySpecialMethods.Repr(context, getField(index));
            if (itemRepr.IsError)
                return itemRepr;
            builder.Append(fieldNames[index]).Append('=').Append(itemRepr.Value.Value);
        }
        builder.Append(')');
        return PyStrObject.FromString(builder.ToString());
    }
}
