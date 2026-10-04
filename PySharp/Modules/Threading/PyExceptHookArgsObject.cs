using PySharp.Modules.Builtins;
using PySharp.Modules.Sys;
using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;

namespace PySharp.Modules.Threading;

/// <summary>
/// _thread._ExceptHookArgs — the struct sequence threading.excepthook
/// receives: (exc_type, exc_value, exc_traceback, thread). PySharp has no
/// traceback object, so the traceback field travels as None (the same
/// trade the sys.excepthook channel makes).
/// </summary>
public sealed partial class PyExceptHookArgsObject : PyTupleObject
{
    internal static PyExceptHookArgsObject Create(PyObject excType, PyObject excValue, PyObject excTraceback, PyObject thread)
    {
        return new PyExceptHookArgsObject([excType, excValue, excTraceback, thread]);
    }

    private PyExceptHookArgsObject(PyObject[] items) : base(items)
    {
        _pyType = PyExceptHookArgsObjectType.Shared;
    }

    public override PyTypeObject DefaultPyType => PyExceptHookArgsObjectType.Shared;
}

[PyType("_thread._ExceptHookArgs")]
public sealed partial class PyExceptHookArgsObjectType : PyTypeObject<PyExceptHookArgsObject>
{
    private static readonly string[] FieldNames = ["exc_type", "exc_value", "exc_traceback", "thread"];

    // A tuple subclass: the MRO fallback supplies every tuple slot
    public sealed override IReadOnlyList<PyTypeObject> Bases => [PyTupleObjectType.Shared];

    // CPython structseq_new rejects direct instantiation
    protected override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        return PyResult.TypeError(PySR.Runtime_Type_CannotCreateInstance, "_thread._ExceptHookArgs");
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
            (context, instance) => PyResult.FromValue(((PyExceptHookArgsObject)instance).InternalArray[index]),
            setter: null,
            deleter: null);
    }

    // structseq repr: "_thread._ExceptHookArgs(exc_type=..., ...)"
    protected override PyResult Repr(PyCallContext context, PyExceptHookArgsObject self)
    {
        return PyFlagsObjectType.StructSeqRepr(context, "_thread._ExceptHookArgs", FieldNames,
            index => self.InternalArray[index]);
    }
}
