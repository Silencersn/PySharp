using PySharp.Modules.Builtins;
using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;

namespace PySharp.Modules.Sys;

// CPython's struct sequence sys.version_info (Python/sysmodule.c
// make_versioninfo): a tuple subclass exposing the (major, minor, micro,
// releaselevel, serial) fields by name, with the structseq repr
public sealed class PyVersionInfoObject : PyTupleObject, IPyObjectRecursiveRepr
{
    // (3, 14, 4, 'final', 0) — the CPython 3.14.4 release this interpreter
    // implements; sys.version and sys.implementation share it
    internal static PyVersionInfoObject Shared { get; } = new(
    [
        PyIntObject.FromInteger(3),
        PyIntObject.FromInteger(14),
        PyIntObject.FromInteger(4),
        PyStrObject.FromString("final"),
        PyIntObject.Zero,
    ]);

    private PyVersionInfoObject(PyObject[] items) : base(items)
    {
        _pyType = PyVersionInfoObjectType.Shared;
    }

    public override PyTypeObject DefaultPyType => PyVersionInfoObjectType.Shared;

    // Nested containers render items through this object-level interface;
    // forwarding to the repr slot keeps the structseq face there too
    PyResult<PyStrObject> IPyObjectRecursiveRepr.RecursiveRepr(PyCallContext context, HashSet<PyObject> ids)
    {
        var result = PySpecialMethods.Repr(context, this);
        return result.IsError ? result.ExceptionResult : PyResult<PyStrObject>.FromValue(result.Value);
    }
}

[PyType("sys.version_info")]
public sealed partial class PyVersionInfoObjectType : PyTypeObject<PyVersionInfoObject>
{
    // A tuple subclass: the MRO fallback supplies every tuple slot
    // (indexing, comparison, iteration), as structseq's bases do
    public sealed override IReadOnlyList<PyTypeObject> Bases => [PyTupleObjectType.Shared];

    // CPython structseq_new rejects direct instantiation
    protected override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        return PyResult.TypeError(PySR.Runtime_Sys_CannotCreateInstances, "sys.version_info");
    }

    protected override void PostConstruct()
    {
        InstallField("major", 0);
        InstallField("minor", 1);
        InstallField("micro", 2);
        InstallField("releaselevel", 3);
        InstallField("serial", 4);
    }

    private void InstallField(string name, int index)
    {
        PyAttributes[name] = new PyMemberDescriptorObject(
            PyGetSetDescriptorObjectType.Shared,
            this,
            name,
            (context, instance) => ((PyVersionInfoObject)instance).InternalArray[index],
            setter: null,
            deleter: null);
    }

    // CPython structseq_repr: "sys.version_info(major=3, ..., serial=0)"
    protected override PyResult Repr(PyCallContext context, PyVersionInfoObject self)
    {
        return PyFlagsObjectType.StructSeqRepr(context, "sys.version_info", FieldNames, index => self.InternalArray[index]);
    }

    private static readonly string[] FieldNames = ["major", "minor", "micro", "releaselevel", "serial"];
}
