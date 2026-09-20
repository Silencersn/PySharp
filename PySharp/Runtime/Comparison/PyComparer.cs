using PySharp.Modules.Builtins;
using PySharp.Runtime.Calls;
using System.Runtime.CompilerServices;

namespace PySharp.Runtime.Comparison;

public static class PyComparer
{
    private static PyResult<PyBoolObject> ToBool(PyCallContext context, PyResult result)
    {
        if (result.IsError)
            return result.ExceptionResult;

        return PySpecialMethods.Bool(context, result.Value);
    }
    public static PyResult<PyBoolObject> Eq(PyCallContext context, PyObject? left, PyObject? right)
    {
        if (left is null)
            return PyBoolObject.FromBoolean(right is null);

        if (right is null)
            return PyBoolObject.False;

        // CPython PyObject_RichCompareBool: identity implies equality,
        // checked before any user __eq__ runs — the guarantee container
        // lookups rely on (a NaN finds itself) and the reason a raising
        // __eq__ is never reached for identical operands
        if (ReferenceEquals(left, right))
            return PyBoolObject.True;

        // Comparison recursion enters no Python frame, so the frame counters
        // cannot bound a cyclic container pair or matching __eq__ re-entry;
        // probe the native stack (CPython checks it in PyObject_RichCompare)
        if (!RuntimeHelpers.TryEnsureSufficientExecutionStack())
        {
            return PyResult<PyBoolObject>.FromException(PyRecursionErrorObjectType.Shared.Create(
                PyStrObject.FromString(PySR.Runtime_Recursion_MaxRecursionDepthExceeded)));
        }

        return ToBool(context, PyOperators.Eq(context, left, right));
    }
    public static PyResult<PyBoolObject> NotEq(PyCallContext context, PyObject? left, PyObject? right)
    {
        if (left is null)
            return PyBoolObject.FromBoolean(right is null);

        if (right is null)
            return PyBoolObject.False;

        // CPython PyObject_RichCompareBool: identical operands are never
        // unequal, again before any user __ne__ runs
        if (ReferenceEquals(left, right))
            return PyBoolObject.False;

        return ToBool(context, PyOperators.NotEq(context, left, right));
    }
    public static PyResult<PyBoolObject> Lt(PyCallContext context, PyObject left, PyObject right)
    {
        return ToBool(context, PyOperators.Lt(context, left, right));
    }
    public static PyResult<PyBoolObject> LtE(PyCallContext context, PyObject left, PyObject right)
    {
        return ToBool(context, PyOperators.LtE(context, left, right));
    }
    public static PyResult<PyBoolObject> Gt(PyCallContext context, PyObject left, PyObject right)
    {
        return ToBool(context, PyOperators.Gt(context, left, right));
    }
    public static PyResult<PyBoolObject> GtE(PyCallContext context, PyObject left, PyObject right)
    {
        return ToBool(context, PyOperators.GtE(context, left, right));
    }
}
