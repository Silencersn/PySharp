using PySharp.Modules.Builtins;
using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.Environments;
using PySharp.Runtime.PyAttributes;

namespace PySharp.Modules.Typing;

/// <summary>
/// The <c>typing</c> module.
/// Provides runtime support for type hints including <c>Generic</c>.
/// </summary>
[PyModuleInclude(PyModuleIncludeScheme.TypeSingleton, typeof(PyGenericObjectType))]
public sealed partial class PyTypingModuleObject : PyModuleObject
{
    public override string? Origin => "built-in";

    public PyTypingModuleObject() : base("typing")
    {
    }
}
