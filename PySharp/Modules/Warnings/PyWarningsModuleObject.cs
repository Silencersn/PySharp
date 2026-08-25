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
        // Only warn is exposed in this phase; __all__ mirrors the public surface.
        AppendAttribute(PySpecialNames.All, PyListObject.CreateList(PyStrObject.FromString("warn")));
    }
}
