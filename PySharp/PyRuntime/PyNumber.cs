using PySharp.PyModules.Builtins;
using PySharp.PyRuntime.Calls;
using System;
using System.Collections.Generic;
using System.Text;

namespace PySharp.PyRuntime;

internal static class PyNumber
{
    public static PyResult Int(PyCallContext context, PyObject obj)
    {
        var toInt = obj.PyType.Slots.Int;
        if (toInt is null)
            return PyResult.RaiseTypeError($"int() argument must be a string, a bytes-like object or a real number, not '{obj.PyType.FullName}'");

        var result = toInt(context, obj);
        if (result.IsError)
            return result;

        if (result.Value is not PyIntObject intObj)
            return PyResult.RaiseTypeError($"{PySpecialNames.Int} returned non-int (type {result.Value.PyType.FullName})");

        return intObj;
    }
}
