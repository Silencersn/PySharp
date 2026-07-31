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

        if (!ReferenceEquals(x.PyType, y.PyType))
            return false;

        return PyObjectComparer.Default.Equals(x, y);
    }

    public int GetHashCode([DisallowNull] PyObject obj)
    {
        return obj switch
        {
            PyStrObject s => s.Value.GetHashCode(),
            PyIntObject i => i.Value.GetHashCode(),
            PyFloatObject f => f.Value.GetHashCode(),
            PyComplexObject c => c.Value.GetHashCode(),
            PyBytesObject b => GetBytesHash(b.AsSpan()),
            PyTupleObject t => GetTupleHash(t),
            PyNoneObject or PyEllipsisObject or PyCodeObject or PyTypeObject
                => RuntimeHelpers.GetHashCode(obj),
            _ => throw new NotSupportedException($"{obj.PyType.FullName} is not a supported constant type."),
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
