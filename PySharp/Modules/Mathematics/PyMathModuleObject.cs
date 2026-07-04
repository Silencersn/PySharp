using PySharp.Modules.Builtins;
using PySharp.Runtime.PyAttributes;

namespace PySharp.Modules.Mathematics;

[PyModuleInclude(PyModuleIncludeScheme.ExplicitMember, "pi", typeof(PyFloatObject), nameof(PyFloatObject.Pi))]
[PyModuleInclude(PyModuleIncludeScheme.ExplicitMember, "e", typeof(PyFloatObject), nameof(PyFloatObject.E))]
[PyModuleInclude(PyModuleIncludeScheme.ExplicitMember, "tau", typeof(PyFloatObject), nameof(PyFloatObject.Tau))]
[PyModuleInclude(PyModuleIncludeScheme.StaticMembers, typeof(PyMathFunctions))]
public partial class PyMathModuleObject : PyModuleObject
{
    public PyMathModuleObject() : base("math")
    {
    }
}
