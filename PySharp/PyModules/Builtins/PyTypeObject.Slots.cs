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
        // TODO: support different protocols

        internal PyUnaryFunction? Str;
        internal PyUnaryFunction? Repr;
        internal PyUnaryFunction? Bool;
        internal PyUnaryFunction? Hash;
        internal PyUnaryFunction? Len;
        internal PyUnaryFunction? Index;
        internal PyUnaryFunction? Int;
        internal PyUnaryFunction? Float;

        internal PyUnaryFunction? Iter;
        internal PyUnaryFunction? Next;
        internal PyBinaryFunction? GetItem;
        internal PyTernaryFunction? SetItem;
        internal PyBinaryFunction? DelItem;
        internal PyBinaryFunction? Contains;

        // Binary operators
        internal PyBinaryFunction? Add;
        internal PyBinaryFunction? Sub;
        internal PyBinaryFunction? Mul;
        internal PyBinaryFunction? TrueDiv;
        internal PyBinaryFunction? FloorDiv;
        internal PyBinaryFunction? Mod;
        internal PyBinaryFunction? DivMod;
        internal PyBinaryFunction? LShift;
        internal PyBinaryFunction? RShift;
        internal PyBinaryFunction? And;
        internal PyBinaryFunction? Xor;
        internal PyBinaryFunction? Or;

        // Rich comparison operators
        internal PyBinaryFunction? Lt;
        internal PyBinaryFunction? Le;
        internal PyBinaryFunction? Eq;
        internal PyBinaryFunction? Ne;
        internal PyBinaryFunction? Gt;
        internal PyBinaryFunction? Ge;

        // Reverse binary operators
        internal PyBinaryFunction? RAdd;
        internal PyBinaryFunction? RSub;
        internal PyBinaryFunction? RMul;
        internal PyBinaryFunction? RTrueDiv;
        internal PyBinaryFunction? RFloorDiv;
        internal PyBinaryFunction? RMod;
        internal PyBinaryFunction? RDivMod;
        internal PyBinaryFunction? RLShift;
        internal PyBinaryFunction? RRShift;
        internal PyBinaryFunction? RAnd;
        internal PyBinaryFunction? RXor;
        internal PyBinaryFunction? ROr;

        // Ternary operators
        internal PyTernaryFunction? Pow;
        internal PyTernaryFunction? RPow;

        internal PyTypeSlots Clone()
        {
            return (PyTypeSlots)MemberwiseClone();
        }
    }
}
