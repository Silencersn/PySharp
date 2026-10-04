using PySharp.Modules.Builtins;
using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;

namespace PySharp.Modules.Threading;

/// <summary>
/// threading._MainThread — the interpreter's startup thread. Instances are
/// only minted by the module machinery (at import time) and stay alive for
/// the process; the Python-level bases chain reaches Thread through the
/// Bases override while the C# side stays on the shared PyThreadObject
/// object hierarchy (the PyBoundedSemaphore parallel-type shape).
/// </summary>
[PyType("_MainThread", Module = "threading")]
public sealed partial class PyMainThreadObjectType : PyTypeObject<PyThreadObject>
{
    public override IReadOnlyList<PyTypeObject> Bases => [PyThreadObjectType.Shared];

    protected override PyResult Repr(PyCallContext context, PyThreadObject self)
    {
        return self.FormatRepr(context);
    }
}
