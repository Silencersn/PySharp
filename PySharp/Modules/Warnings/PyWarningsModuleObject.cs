using PySharp.Modules.Builtins;
using PySharp.Runtime;
using PySharp.Runtime.PyAttributes;

namespace PySharp.Modules.Warnings;

[PyModuleInclude(PyModuleIncludeScheme.StaticMembers, typeof(PyWarningsFunctions))]
[PyModuleInclude(PyModuleIncludeScheme.TypeSingleton, typeof(PyWarningMessageObjectType))]
public partial class PyWarningsModuleObject : PyModuleObject
{
    public override string? Origin => "built-in";

    public PyWarningsModuleObject() : base("warnings")
    {
        // Mirror the public surface exposed by this module in __all__.
        AppendAttribute(PySpecialNames.All, PyListObject.CreateList(
            PyStrObject.FromString("warn"),
            PyStrObject.FromString("warn_explicit"),
            PyStrObject.FromString("simplefilter"),
            PyStrObject.FromString("filterwarnings"),
            PyStrObject.FromString("resetwarnings"),
            PyStrObject.FromString("catch_warnings"),
            PyStrObject.FromString("WarningMessage")));
    }
}
