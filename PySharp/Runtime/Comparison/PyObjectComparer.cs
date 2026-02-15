using PySharp.Modules.Builtins;
using PySharp.Runtime.Calls;
using System.Diagnostics.CodeAnalysis;

namespace PySharp.Runtime.Comparison;

public sealed class PyObjectComparer : IEqualityComparer<PyObject>, IComparer<PyObject>
{
    public static PyObjectComparer Default { get; } = new();

    private PyCallContext Context { get; } = PyCallContext.PyObjectComparison;

    private PyObjectComparer() { }

    public int Compare(PyObject? x, PyObject? y)
    {
        if (x is null)
            return y is null ? 0 : -1;

        if (y is null)
            return 1;

        if (Equals(x, y))
            return 0;

        var lt = PyOperators.Lt(Context, x, y);
        if (lt.IsError)
            throw new PyRuntimeException(Context, lt.Exception);

        var ltBool = PySpecialMethods.Bool(Context, lt.Value);
        if (ltBool.IsError)
            throw new PyRuntimeException(Context, ltBool.Exception);

        return ltBool.Value.BoolValue ? -1 : 1;
    }

    public bool Equals(PyObject? x, PyObject? y)
    {
        if (x is null)
            return y is null;

        if (y is null)
            return false;

        var eq = PyOperators.Eq(Context, x, y);
        if (eq.IsError)
            throw new PyRuntimeException(Context, eq.Exception);

        var eqBool = PySpecialMethods.Bool(Context, eq.Value);
        if (eqBool.IsError)
            throw new PyRuntimeException(Context, eqBool.Exception);

        return eqBool.Value.BoolValue;
    }

    public int GetHashCode([DisallowNull] PyObject obj)
    {
        var hash = PySpecialMethods.Hash(Context, obj);
        if (hash.IsError)
            throw new PyRuntimeException(Context, hash.Exception);

        return hash.Value.Value.GetHashCode();
    }
}
