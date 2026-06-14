using PySharp.Modules.Builtins;
using PySharp.Runtime.Calls;
using System.Diagnostics.CodeAnalysis;

namespace PySharp.Runtime.Comparison;

public sealed class PyObjectComparer :
    IEqualityComparer<PyObject>,
    IComparer<PyObject>,
    IAlternateEqualityComparer<(PyCallContext Context, PyObject Object), PyObject>,
    IAlternateEqualityComparer<string, PyObject>,
    IAlternateEqualityComparer<ReadOnlySpan<char>, PyObject>
{
    public static PyObjectComparer Default { get; } = new(PyCallContext.PyObjectComparison);

    private PyCallContext Context { get; }

    internal PyObjectComparer(PyCallContext context)
    {
        Context = context;
    }

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

    private static PyResult<PyBoolObject> Equals(PyCallContext context, PyObject? x, PyObject? y)
    {
        if (x is null)
            return PyBoolObject.FromBoolean(y is null);

        if (y is null)
            return PyBoolObject.False;

        var eq = PyOperators.Eq(context, x, y);
        if (eq.IsError)
            return eq.ExceptionResult;

        return PySpecialMethods.Bool(context, eq.Value);
    }

    private static PyResult<PyIntObject> GetHashCode(PyCallContext context, [DisallowNull] PyObject obj)
    {
        return PySpecialMethods.Hash(context, obj);
    }

    public bool Equals(PyObject? x, PyObject? y)
    {
        return Equals(Context, x, y).PyUnwrap(Context).BoolValue;
    }

    public int GetHashCode([DisallowNull] PyObject obj)
    {
        return GetHashCode(Context, obj).PyUnwrap(Context).Value.GetHashCode();
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

    public bool Equals(string alternate, PyObject other)
    {
        return Equals(alternate.AsSpan(), other);
    }

    public int GetHashCode(string alternate)
    {
        return alternate.GetHashCode();
    }

    public PyObject Create(string alternate)
    {
        return PyStrObject.FromString(alternate);
    }

    public bool Equals(ReadOnlySpan<char> alternate, PyObject other)
    {
        if (other is not PyStrObject strObj)
            return false;

        return alternate.Equals(strObj.Value, StringComparison.Ordinal);
    }

    public int GetHashCode(ReadOnlySpan<char> alternate)
    {
        return string.GetHashCode(alternate);
    }

    public PyObject Create(ReadOnlySpan<char> alternate)
    {
        return PyStrObject.FromString(alternate.ToString());
    }
}

[AIGenerated]
public static class ComparisonExtensions
{
    public static int SequenceCompare<T>(this IEnumerable<T> x, IEnumerable<T> y, IComparer<T> comparer)
    {
        using var xe = x.GetEnumerator();
        using var ye = y.GetEnumerator();

        while (true)
        {
            var xHasNext = xe.MoveNext();
            var yHasNext = ye.MoveNext();

            if (!xHasNext && !yHasNext)
                return 0;

            if (!xHasNext)
                return -1;

            if (!yHasNext)
                return 1;

            var result = comparer.Compare(xe.Current, ye.Current);
            if (result != 0)
                return result;
        }
    }
}
