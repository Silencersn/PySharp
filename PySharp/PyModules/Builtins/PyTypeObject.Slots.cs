using PySharp.PyRuntime.Calls;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace PySharp.PyModules.Builtins;

partial class PyTypeObject
{
    internal PyTypeSlots Slots { get; }

    internal sealed class PyTypeSlots
    {
        internal PyUnaryFunction? Str;
        internal PyUnaryFunction? Repr;
        internal PyUnaryFunction? Bool;
        internal PyUnaryFunction? Hash;
        internal PyUnaryFunction? Len;
        internal PyUnaryFunction? Index;
        internal PyUnaryFunction? Int;
        internal PyUnaryFunction? Float;

        internal PyBinaryFunction? Add;
        internal PyBinaryFunction? RAdd;

        internal PyTypeSlots Clone()
        {
            return (PyTypeSlots)MemberwiseClone();
        }
    }
}
