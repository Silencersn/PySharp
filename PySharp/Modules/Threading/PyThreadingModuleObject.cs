using PySharp.Modules.Builtins;
using PySharp.Runtime.Calls;
using PySharp.Runtime.Environments;
using PySharp.Runtime.PyAttributes;

namespace PySharp.Modules.Threading;

[PyModuleInclude(PyModuleIncludeScheme.StaticMembers, typeof(PyThreadingFunctions))]
[PyModuleInclude(PyModuleIncludeScheme.TypeSingleton, typeof(PyThreadObjectType))]
[PyModuleInclude(PyModuleIncludeScheme.TypeSingleton, typeof(PyMainThreadObjectType))]
[PyModuleInclude(PyModuleIncludeScheme.TypeSingleton, typeof(PyDummyThreadObjectType))]
[PyModuleInclude(PyModuleIncludeScheme.TypeSingleton, typeof(PyTimerObjectType))]
[PyModuleInclude(PyModuleIncludeScheme.TypeSingleton, typeof(PyConditionObjectType))]
[PyModuleInclude(PyModuleIncludeScheme.TypeSingleton, typeof(PySemaphoreObjectType))]
[PyModuleInclude(PyModuleIncludeScheme.TypeSingleton, typeof(PyBoundedSemaphoreObjectType))]
[PyModuleInclude(PyModuleIncludeScheme.TypeSingleton, typeof(PyEventObjectType))]
[PyModuleInclude(PyModuleIncludeScheme.TypeSingleton, typeof(PyBarrierObjectType))]
[PyModuleInclude(PyModuleIncludeScheme.TypeSingleton, typeof(PyBrokenBarrierErrorObjectType))]
[PyModuleInclude(PyModuleIncludeScheme.TypeSingleton, typeof(PyLocalObjectType))]
public partial class PyThreadingModuleObject : PyModuleObject
{
    public PyThreadingModuleObject() : base("threading")
    {
    }

    public override void OnImport(PyCallContext context, PyEnvironment environment)
    {
        // threading.Lock IS the _thread.lock type (Lock = _LockType in
        // threading.py); the TypeSingleton registration exposes it under
        // its C type name, so the module alias points at the same object
        AppendAttribute("Lock", PyLockObjectType.Shared);

        // threading.local IS _thread._local (from _thread import _local);
        // the TypeSingleton registration exposes the C type name, so the
        // module alias points at the same object
        AppendAttribute("local", PyLocalObjectType.Shared);

        // _thread.TIMEOUT_MAX on Windows: floor(0xFFFFFFFE milliseconds)
        // as seconds, a plain float like CPython publishes it
        AppendAttribute("TIMEOUT_MAX", PyFloatObject.FromDouble(4294967.0));

        // threading.ThreadError = _thread.error, which is RuntimeError
        // itself (the same type object, not a subclass)
        AppendAttribute("ThreadError", PyRuntimeErrorObjectType.Shared);

        // threading.excepthook is the C function _thread._excepthook, so
        // the function's own name is "_excepthook" while the module
        // exposes it as "excepthook" (both names reach the same object)
        AppendAttribute("excepthook", PyThreadingFunctions.ExceptHook);

        // _MainThread settles into the active registry at import time so
        // current_thread()/enumerate() report it from the start
        _ = PyThreadObjectType.GetMainThread(environment);
    }
}
