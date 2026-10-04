using PySharp.Modules.Builtins;
using PySharp.Runtime.Calls;
using PySharp.Runtime.Environments;
using PySharp.Runtime.PyAttributes;

namespace PySharp.Modules.Threading;

[PyModuleInclude(PyModuleIncludeScheme.StaticMembers, typeof(PyThreadingFunctions))]
[PyModuleInclude(PyModuleIncludeScheme.TypeSingleton, typeof(PyThreadObjectType))]
[PyModuleInclude(PyModuleIncludeScheme.TypeSingleton, typeof(PyConditionObjectType))]
[PyModuleInclude(PyModuleIncludeScheme.TypeSingleton, typeof(PySemaphoreObjectType))]
[PyModuleInclude(PyModuleIncludeScheme.TypeSingleton, typeof(PyBoundedSemaphoreObjectType))]
[PyModuleInclude(PyModuleIncludeScheme.TypeSingleton, typeof(PyEventObjectType))]
[PyModuleInclude(PyModuleIncludeScheme.TypeSingleton, typeof(PyBarrierObjectType))]
[PyModuleInclude(PyModuleIncludeScheme.TypeSingleton, typeof(PyBrokenBarrierErrorObjectType))]
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
    }
}
