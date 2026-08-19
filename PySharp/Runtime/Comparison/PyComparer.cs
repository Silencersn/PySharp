using PySharp.Modules.Builtins;
using PySharp.Runtime.Calls;

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

        return ToBool(context, PyOperators.Eq(context, left, right));
    }
    public static PyResult<PyBoolObject> NotEq(PyCallContext context, PyObject? left, PyObject? right)
    {
        if (left is null)
            return PyBoolObject.FromBoolean(right is null);

        if (right is null)
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
