using PySharp.PyModules.Builtins;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;

namespace PySharp.PyRuntime;

public static class PyInteropService
{
    public static bool TryGetStr(PyObject obj, [NotNullWhen(true)] out string? s)
    {
        if (PySpecialMethods.TryGetStr(obj, out var pyObj))
        {
            s = pyObj.Value;
            return true;
        }

        s = null;
        return false;
    }
    public static bool TryGetRepr(PyObject obj, [NotNullWhen(true)] out string? s)
    {
        if (PySpecialMethods.TryGetRepr(obj, out var pyObj))
        {
            s = pyObj.Value;
            return true;
        }

        s = null;
        return false;
    }
    public static bool TryGetBool(PyObject obj, out bool b)
    {
        if (PySpecialMethods.TryGetBool(obj, out var pyObj))
        {
            b = pyObj.BoolValue;
            return true;
        }

        b = false;
        return false;
    }
    public static bool TryGetInt(PyObject obj, out int i)
    {
        if (PySpecialMethods.TryGetInt(obj, out var pyObj))
        {
            i = pyObj.Int32Value;
            return true;
        }

        i = 0;
        return false;
    }
    public static bool TryGetFloat(PyObject obj, out double f)
    {
        if (PySpecialMethods.TryGetFloat(obj, out var pyObj))
        {
            f = pyObj.Value;
            return true;
        }

        f = 0;
        return false;
    }
    public static bool TryGetLen(PyObject obj, out int i)
    {
        if (PySpecialMethods.TryGetLen(obj, out var pyObj))
        {
            i = pyObj.Int32Value;
            return true;
        }

        i = 0;
        return false;
    }
    public static bool TryGetHash(PyObject obj, out int i)
    {
        if (PySpecialMethods.TryGetHash(obj, out var pyObj))
        {
            i = pyObj.Int32Value;
            return true;
        }

        i = 0;
        return false;
    }
    public static bool TryGetIndex(PyObject obj, out int i)
    {
        if (PySpecialMethods.TryGetIndex(obj, out var pyObj))
        {
            i = pyObj.Int32Value;
            return true;
        }

        i = 0;
        return false;
    }
    public static bool TryGetIndex(PyObject obj, out BigInteger i)
    {
        if (PySpecialMethods.TryGetIndex(obj, out var pyObj))
        {
            i = pyObj.Int32Value;
            return true;
        }

        i = 0;
        return false;
    }

    public static bool TryGetLt(PyObject left, PyObject right, out bool result)
    {
        var retObj = PyOperators.Lt(left, right);
        if (retObj is null)
        {
            result = false;
            return false;
        }

        if (!TryGetBool(retObj, out result))
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
