using PySharp.Modules.Builtins;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace PySharp.Utility;

[InlineArray(Length)]
public struct InlinePyObjectArray
{
    public const int Length = 8;

    private PyObject _element;
}
