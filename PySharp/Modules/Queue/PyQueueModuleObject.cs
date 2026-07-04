using PySharp.Modules.Builtins;
using PySharp.Runtime.PyAttributes;

namespace PySharp.Modules.Queue;

[PyModuleInclude(PyModuleIncludeScheme.TypeSingleton, typeof(PyQueueObjectType))]
[PyModuleInclude(PyModuleIncludeScheme.TypeSingleton, typeof(PyFullObjectType))]
[PyModuleInclude(PyModuleIncludeScheme.TypeSingleton, typeof(PyEmptyObjectType))]
[PyModuleInclude(PyModuleIncludeScheme.TypeSingleton, typeof(PyShutDownObjectType))]
public sealed partial class PyQueueModuleObject : PyModuleObject
{
    public PyQueueModuleObject() : base("queue")
    {
    }
}
