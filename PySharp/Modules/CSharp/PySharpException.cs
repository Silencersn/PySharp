using PySharp.Modules.Builtins;
using PySharp.Runtime.PyAttributes;
using System;
using System.Collections.Generic;
using System.Text;

namespace PySharp.Modules.CSharp;

[PyException("PySharpException", Bases = [], IsSealed = true)]
[PyTypeConstructor(AccessModifier = "internal")]
internal sealed partial class PySharpException : PyExceptionType;