using PySharp.Modules.Builtins;
using PySharp.Runtime.PyAttributes;

namespace PySharp.Modules.Operator;

[PyModuleInclude(PyModuleIncludeScheme.StaticMembers, typeof(PyOperatorFunctions))]
public partial class PyOperatorModuleObject : PyModuleObject
{
    public PyOperatorModuleObject() : base("operator")
    {
    }
}
