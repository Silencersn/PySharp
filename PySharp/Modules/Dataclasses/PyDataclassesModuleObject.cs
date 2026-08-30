using PySharp.Modules.Builtins;
using PySharp.Runtime.PyAttributes;

namespace PySharp.Modules.Dataclasses;

/// <summary>
/// The <c>dataclasses</c> module.
/// Provides a minimal <c>@dataclass</c> decorator.
/// </summary>
[PyFrozenModule("dataclasses", @"Lib\dataclasses.py")]
public partial class PyDataclassesModuleObject : PyFrozenModuleObject;
