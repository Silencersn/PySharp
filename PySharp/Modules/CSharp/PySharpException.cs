using PySharp.Modules.Builtins;
using PySharp.Runtime.PyAttributes;

namespace PySharp.Modules.CSharp;

[PyException("PySharpException", Bases = [], IsSealed = true)]
[PyTypeConstructor(AccessModifier = "internal")]
internal sealed partial class PySharpException : PyExceptionType;