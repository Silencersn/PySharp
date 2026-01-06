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
        internal PyUnaryFunction? Repr;
        internal PyUnaryFunction? Int;

        internal PyBinaryFunction? Add;
        internal PyBinaryFunction? RAdd;

        internal PyTypeSlots Clone()
        {
            return (PyTypeSlots)MemberwiseClone();
        }
    }
}
