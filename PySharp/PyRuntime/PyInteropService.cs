using PySharp.PyModules.Builtins;
using PySharp.PyRuntime.Calls;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;

namespace PySharp.PyRuntime;

public static class PyInteropService
{
    public static bool TryGetStr(PyObject obj, [NotNullWhen(true)] out string? s)
    {
        if (PySpecialMethods.TryGetStr(PyCallContext.Null, obj, out var pyObj, out var result))
        {
            s = pyObj.Value;
            return true;
        }

        s = null;
        return false;
    }
    public static bool TryGetRepr(PyObject obj, [NotNullWhen(true)] out string? s)
    {
        if (PySpecialMethods.TryGetRepr(PyCallContext.Null, obj, out var pyObj, out var result))
        {
            s = pyObj.Value;
            return true;
        }

        s = null;
        return false;
    }
    public static bool TryGetBool(PyObject obj, out bool b)
    {
        if (PySpecialMethods.TryGetBool(PyCallContext.Null, obj, out var pyObj, out var result))
        {
            b = pyObj.BoolValue;
            return true;
        }

        b = false;
        return false;
    }
    public static bool TryGetInt(PyObject obj, out int i)
    {
        if (PySpecialMethods.TryGetInt(PyCallContext.Null, obj, out var pyObj, out var result))
        {
            i = pyObj.Int32Value;
            return true;
        }

        i = 0;
        return false;
    }
    public static bool TryGetFloat(PyObject obj, out double f)
    {
        if (PySpecialMethods.TryGetFloat(PyCallContext.Null, obj, out var pyObj, out var result))
        {
            f = pyObj.Value;
            return true;
        }

        f = 0;
        return false;
    }
    public static bool TryGetLen(PyObject obj, out int i)
    {
        if (PySpecialMethods.TryGetLen(PyCallContext.Null, obj, out var pyObj, out var result))
        {
            i = pyObj.Int32Value;
            return true;
        }

        i = 0;
        return false;
    }
    public static bool TryGetHash(PyObject obj, out int i)
    {
        if (PySpecialMethods.TryGetHash(PyCallContext.Null, obj, out var pyObj, out var result))
        {
            i = pyObj.Int32Value;
            return true;
        }

        i = 0;
        return false;
    }
    public static bool TryGetIndex(PyObject obj, out int i)
    {
        if (PySpecialMethods.TryGetIndex(PyCallContext.Null, obj, out var pyObj, out var result))
        {
            i = pyObj.Int32Value;
            return true;
        }

        i = 0;
        return false;
    }
    public static bool TryGetIndex(PyObject obj, out BigInteger i)
    {
        if (PySpecialMethods.TryGetIndex(PyCallContext.Null, obj, out var pyObj, out var result))
        {
            i = pyObj.Int32Value;
            return true;
        }

        i = 0;
        return false;
    }

    public static bool TryGetLt(PyObject left, PyObject right, out bool result)
    {
        var retObj = PyOperators.Lt(PyCallContext.Null, left, right);
        if (retObj.IsError)
        {
            result = false;
            return false;
        }

        if (!TryGetBool(retObj.Value, out result))
            return false;
        return true;
    }


    public static PyObject ToPyObject<T>(T? obj)
    {
        if (obj is null)
            return PyNoneObject.None;

        if (obj is string s)
            return PyStrObject.FromString(s);

        if (obj is char c)
            return PyStrObject.FromString(c.ToString());

        throw new NotImplementedException();
    }
}
