using PySharp.Modules.Builtins;
using PySharp.Runtime;
using PySharp.Runtime.PyAttributes;

namespace PySharp.Modules.Warnings;

[PyModuleInclude(PyModuleIncludeScheme.StaticMembers, typeof(PyWarningsFunctions))]
public partial class PyWarningsModuleObject : PyModuleObject
{
    public override string? Origin => "built-in";

    public PyWarningsModuleObject() : base("warnings")
    {
        // Mirror the public surface exposed by this module in __all__.
        AppendAttribute(PySpecialNames.All, PyListObject.CreateList(
            PyStrObject.FromString("warn"),
            PyStrObject.FromString("simplefilter"),
            PyStrObject.FromString("filterwarnings"),
            PyStrObject.FromString("resetwarnings")));
    }
}
