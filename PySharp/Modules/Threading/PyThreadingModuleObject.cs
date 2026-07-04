using PySharp.Modules.Builtins;
using PySharp.Runtime.PyAttributes;

namespace PySharp.Modules.Threading;

[PyModuleInclude(PyModuleIncludeScheme.TypeSingleton, typeof(PyThreadObjectType))]
public partial class PyThreadingModuleObject : PyModuleObject
{
    public PyThreadingModuleObject() : base("threading")
    {
    }
}
