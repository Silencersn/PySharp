using PySharp.Modules.Builtins;
using PySharp.Runtime.Calls;
using System;
using System.Collections.Generic;
using System.Text;

namespace PySharp.Runtime.Comparison;

internal static class PyCollectionComparer
{
    public static PyResult<PyBoolObject> Eq(PyCallContext context, ReadOnlySpan<PyObject> left, ReadOnlySpan<PyObject> right)
    {
        if (left.Length != right.Length)
            return PyBoolObject.False;

        for (int i = 0; i < left.Length; i++)
        {
            var eq = PyComparer.Eq(context, left[i], right[i]);
            if (eq.IsError)
                return eq;

            if (!eq.Value.BoolValue)
                return PyBoolObject.False;
        }

        return PyBoolObject.True;
    }

    private static PyResult SequenceCompare(PyCallContext context, ReadOnlySpan<PyObject> left, ReadOnlySpan<PyObject> right, Func<PyCallContext, PyObject, PyObject, PyResult> compare)
    {
        int i = 0;
        for (; i < left.Length && i < right.Length; i++)
        {
            var eq = PyComparer.Eq(context, left[i], right[i]);
            if (eq.IsError)
                return eq;

            if (!eq.Value.BoolValue)
                break;
        }

        if (i >= left.Length || i >= right.Length)
            return compare(context, PyIntObject.FromInteger(left.Length), PyIntObject.FromInteger(right.Length));

        return compare(context, left[i], right[i]);
    }

    public static PyResult Lt(PyCallContext context, ReadOnlySpan<PyObject> left, ReadOnlySpan<PyObject> right)
    {
        return SequenceCompare(context, left, right, PyOperators.Lt);
    }
    public static PyResult Le(PyCallContext context, ReadOnlySpan<PyObject> left, ReadOnlySpan<PyObject> right)
    {
        return SequenceCompare(context, left, right, PyOperators.LtE);
    }
    public static PyResult Gt(PyCallContext context, ReadOnlySpan<PyObject> left, ReadOnlySpan<PyObject> right)
    {
        return SequenceCompare(context, left, right, PyOperators.Gt);
    }
    public static PyResult Ge(PyCallContext context, ReadOnlySpan<PyObject> left, ReadOnlySpan<PyObject> right)
    {
        return SequenceCompare(context, left, right, PyOperators.GtE);
    }
}
