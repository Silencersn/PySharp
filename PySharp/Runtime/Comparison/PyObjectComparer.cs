using PySharp.Modules.Builtins;
using PySharp.Runtime.Calls;
using PySharp.Runtime.Calls.Extensions;
using System.Diagnostics.CodeAnalysis;

namespace PySharp.Runtime.Comparison;

public sealed class PyObjectComparer :
    IEqualityComparer<PyObject>,
    IComparer<PyObject>,
    IAlternateEqualityComparer<(PyCallContext Context, PyObject Object), PyObject>
{
    public static PyObjectComparer Default { get; } = new();

    private PyCallContext DefaultContext { get; } = PyCallContext.PyObjectComparison;

    private PyObjectComparer() { }

    public int Compare(PyObject? x, PyObject? y)
    {
        if (x is null)
            return y is null ? 0 : -1;

        if (y is null)
            return 1;

        if (Equals(x, y))
            return 0;

        var lt = PyOperators.Lt(DefaultContext, x, y);
        if (lt.IsError)
            throw new PyRuntimeException(DefaultContext, lt.Exception);

        var ltBool = PySpecialMethods.Bool(DefaultContext, lt.Value);
        if (ltBool.IsError)
            throw new PyRuntimeException(DefaultContext, ltBool.Exception);

        return ltBool.Value.BoolValue ? -1 : 1;
    }

    private static PyResult<PyBoolObject> Equals(PyCallContext context, PyObject? x, PyObject? y)
    {
        if (x is null)
            return PyBoolObject.FromBoolean(y is null);

        if (y is null)
            return PyBoolObject.False;

        var eq = PyOperators.Eq(context, x, y);
        if (eq.IsError)
            return eq.Of<PyBoolObject>();

        return PySpecialMethods.Bool(context, eq.Value);
    }

    private static PyResult<PyIntObject> GetHashCode(PyCallContext context, [DisallowNull] PyObject obj)
    {
        return PySpecialMethods.Hash(context, obj);
    }

    public bool Equals(PyObject? x, PyObject? y)
    {
        return Equals(DefaultContext, x, y).PyUnwrap(DefaultContext).BoolValue;
    }

    public int GetHashCode([DisallowNull] PyObject obj)
    {
        return GetHashCode(DefaultContext, obj).PyUnwrap(DefaultContext).Value.GetHashCode();
    }

    public bool Equals((PyCallContext Context, PyObject Object) alternate, PyObject other)
    {
        return Equals(alternate.Context, alternate.Object, other).PyUnwrap(alternate.Context).BoolValue;
    }

    public int GetHashCode((PyCallContext Context, PyObject Object) alternate)
    {
        return GetHashCode(alternate.Context, alternate.Object).PyUnwrap(alternate.Context).Value.GetHashCode();
    }

    public PyObject Create((PyCallContext Context, PyObject Object) alternate)
    {
        return alternate.Object;
    }
}
