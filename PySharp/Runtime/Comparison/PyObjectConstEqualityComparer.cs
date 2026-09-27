using PySharp.Modules.Builtins;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace PySharp.Runtime.Comparison;

internal sealed class PyObjectConstEqualityComparer : IEqualityComparer<PyObject>
{
    internal static PyObjectConstEqualityComparer Shared { get; } = new();

    private PyObjectConstEqualityComparer() { }

    public bool Equals(PyObject? x, PyObject? y)
    {
        if (x is null)
            return y is null;

        if (y is null)
            return false;

        if (ReferenceEquals(x, y))
            return true;

        Debug.Assert(IsSupported(x));
        Debug.Assert(IsSupported(y));

        // Type is part of a constant's identity: 1, True and 1.0 are three
        // distinct constants even though == says otherwise, and CPython keys
        // the const cache on exactly that distinction (_PyCode_ConstantKey,
        // Objects/codeobject.c). The check also keeps a PyBoolObject from
        // folding with a PyIntObject, which it subclasses.
        if (!ReferenceEquals(x.PyType, y.PyType))
            return false;

        // Float constants must be distinguished by their exact bit pattern so
        // that 0.0 and -0.0 (and distinct NaNs) never share a pooled object:
        // value equality would collapse them into one constant and the second
        // occurrence would incorrectly reuse the first one's sign. Complex
        // constants carry the same signed-zero distinction per component.
        if (x is PyFloatObject fx && y is PyFloatObject fy)
            return BitConverter.DoubleToInt64Bits(fx.Value) == BitConverter.DoubleToInt64Bits(fy.Value);

        if (x is PyComplexObject cx && y is PyComplexObject cy)
        {
            return BitConverter.DoubleToInt64Bits(cx.Real) == BitConverter.DoubleToInt64Bits(cy.Real)
                && BitConverter.DoubleToInt64Bits(cx.Imag) == BitConverter.DoubleToInt64Bits(cy.Imag);
        }

        // A constant tuple's identity is its elements' identities, element by
        // element — never element-wise ==. (1,) and (True,) compare equal but
        // are different constants (CPython's _PyCode_ConstantKey recurses into
        // tuples for this reason).
        if (x is PyTupleObject tx && y is PyTupleObject ty)
        {
            if (tx.Count != ty.Count)
                return false;

            for (int i = 0; i < tx.Count; i++)
            {
                if (!Equals(tx[i], ty[i]))
                    return false;
            }

            return true;
        }

        return PyObjectComparer.Default.Equals(x, y);
    }

    public int GetHashCode([DisallowNull] PyObject obj)
    {
        return obj switch
        {
            PyStrObject s => s.Value.GetHashCode(),
            PyIntObject i => i.Value.GetHashCode(),
            PyFloatObject f => BitConverter.DoubleToInt64Bits(f.Value).GetHashCode(),
            PyComplexObject c => (BitConverter.DoubleToInt64Bits(c.Real), BitConverter.DoubleToInt64Bits(c.Imag)).GetHashCode(),
            PyBytesObject b => GetBytesHash(b.AsSpan()),
            PyTupleObject t => GetTupleHash(t),
            PyNoneObject or PyEllipsisObject or PyCodeObject or PyTypeObject
                => RuntimeHelpers.GetHashCode(obj),
            _ => throw new NotSupportedException($"{obj.PyType.QualName} is not a supported constant type."),
        };
    }

    private static bool IsSupported(PyObject obj)
    {
        return obj is PyStrObject or PyIntObject or PyFloatObject
            or PyComplexObject or PyBytesObject or PyTupleObject
            or PyNoneObject or PyEllipsisObject or PyCodeObject or PyTypeObject;
    }

    private static int GetBytesHash(ReadOnlySpan<byte> bytes)
    {
        unchecked
        {
            var hash = bytes.Length;
            foreach (var b in bytes)
                hash = hash * 31 + b;
            return hash;
        }
    }

    private int GetTupleHash(PyTupleObject tuple)
    {
        unchecked
        {
            var hash = 17;
            foreach (var item in tuple)
                hash = hash * 31 + GetHashCode(item);
            return hash;
        }
    }
}
