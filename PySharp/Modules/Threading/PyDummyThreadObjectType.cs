using PySharp.Modules.Builtins;
using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;

namespace PySharp.Modules.Threading;

/// <summary>
/// threading._DummyThread — the synthetic object current_thread() hands out
/// for a thread that was not created through the threading module. Same
/// parallel-type shape as _MainThread: alive and daemonic from birth, and
/// join always refuses.
/// </summary>
[PyType("_DummyThread", Module = "threading")]
public sealed partial class PyDummyThreadObjectType : PyTypeObject<PyThreadObject>
{
    public override IReadOnlyList<PyTypeObject> Bases => [PyThreadObjectType.Shared];

    protected override PyResult Repr(PyCallContext context, PyThreadObject self)
    {
        return self.FormatRepr(context);
    }

    [PyMethod("join")]
    [PyFunctionParameters("timeout=None")]
    private static PyResult Join(PyCallContext context, PyThreadObject self, PyArguments arguments)
    {
        return PyResult.RaiseException(PyRuntimeErrorObjectType.Shared, PySR.Runtime_Threading_JoinDummyThread);
    }
}
