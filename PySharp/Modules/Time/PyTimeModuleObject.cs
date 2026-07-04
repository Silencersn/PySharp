using PySharp.Modules.Builtins;
using PySharp.Runtime.PyAttributes;

namespace PySharp.Modules.Time;

[PyModuleInclude(PyModuleIncludeScheme.StaticMembers, typeof(PyTimeFunctions))]
public partial class PyTimeModuleObject : PyModuleObject
{
    public PyTimeModuleObject() : base("time")
    {
    }
}
