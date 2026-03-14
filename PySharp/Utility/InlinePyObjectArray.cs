using PySharp.Modules.Builtins;
using System.Runtime.CompilerServices;

namespace PySharp.Utility;

[InlineArray(Length)]
public struct InlinePyObjectArray
{
    public const int Length = 8;

    private PyObject _element;
}
