using PySharp.Modules.Builtins;
using System.Diagnostics.CodeAnalysis;

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

        if (!ReferenceEquals(x.PyType, y.PyType))
            return false;

        return PyObjectComparer.Default.Equals(x, y);
    }

    public int GetHashCode([DisallowNull] PyObject obj)
    {
        return 0;
    }
}
