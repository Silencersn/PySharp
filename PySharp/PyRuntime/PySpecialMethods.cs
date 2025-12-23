using PySharp.PyModules.Builtins;
using PySharp.PyRuntime.Calls;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;

namespace PySharp.PyRuntime;

public static class PySpecialMethods
{
    private static bool TryGetSpecialMethod<TPyObject>(Func<PyResult> func, Func<PyObject, string> msgCreatorIfWrongType, [NotNullWhen(true)] out TPyObject? o, out PyResult result) where TPyObject : PyObject
    {
        result = func();
        if (result.IsError)
        {
            o = null;
            return false;
        }

        if (result.Value is not TPyObject objOfT)
        {
            o = null;
            result = PyResult.RaiseTypeError(msgCreatorIfWrongType(result.Value));
            return false;
        }

        o = objOfT;
        return true;
    }


    public static bool TryGetStr(PyCallContext context, PyObject obj, [NotNullWhen(true)] out PyStrObject? s, out PyResult result)
    {
        return TryGetSpecialMethod(() => obj.Str(context), o => $"{PySpecialNames.Str} returned non-string (type {o.PyType.Name})", out s, out result);
    }

    public static bool TryGetRepr(PyCallContext context, PyObject obj, [NotNullWhen(true)] out PyStrObject? s, out PyResult result)
    {
        return TryGetSpecialMethod(() => obj.Repr(context), o => $"{PySpecialNames.Repr} returned non-string (type {o.PyType.Name})", out s, out result);
    }

    public static bool TryGetBool(PyCallContext context, PyObject obj, [NotNullWhen(true)] out PyBoolObject? b, out PyResult result)
    {
        return TryGetSpecialMethod(() => obj.Bool(context), o => $"{PySpecialNames.Bool} should return bool, returned {o.PyType.Name}", out b, out result);
    }

    public static bool TryGetInt(PyCallContext context, PyObject obj, [NotNullWhen(true)] out PyIntObject? i, out PyResult result)
    {
        return TryGetSpecialMethod(() => obj.Int(context), o => $"{PySpecialNames.Int} returned non-int (type {o.PyType.Name})", out i, out result);
    }

    public static bool TryGetFloat(PyCallContext context, PyObject obj, [NotNullWhen(true)] out PyFloatObject? f, out PyResult result)
    {
        return TryGetSpecialMethod(() => obj.Float(context), o => $"{PySpecialNames.Float} returned non-float (type {o.PyType.Name})", out f, out result);
    }

    public static bool TryGetLen(PyCallContext context, PyObject obj, [NotNullWhen(true)] out PyIntObject? i, out PyResult result)
    {
        return TryGetSpecialMethod(() => obj.Len(context), o => $"{PySpecialNames.Len} returned non-int (type {o.PyType.Name})", out i, out result);
    }

    private static readonly BigInteger _maxHash = new(uint.MaxValue);
    public static bool TryGetHash(PyCallContext context, PyObject obj, [NotNullWhen(true)] out PyIntObject? i, out PyResult result)
    {
        if (!TryGetSpecialMethod(() => obj.Hash(context), o => $"{PySpecialNames.Hash} returned non-int (type {o.PyType.Name})", out i, out result))
            return false;

        var value = i.Value;
        if (value == -1)
            value = -2;

        value &= _maxHash;
        i = PyIntObject.FromInteger(value);
        return true;
    }

    public static bool TryGetIndex(PyCallContext context, PyObject obj, [NotNullWhen(true)] out PyIntObject? i, out PyResult result)
    {
        return TryGetSpecialMethod(() => obj.Index(context), o => $"{PySpecialNames.Index} returned non-int (type {o.PyType.Name})", out i, out result);
    }

    public static PyResult GetStr(PyCallContext context, PyObject obj)
    {
        TryGetStr(context, obj, out _, out var result);
        return result;
    }

    public static PyResult GetRepr(PyCallContext context, PyObject obj)
    {
        TryGetRepr(context, obj, out _, out var result);
        return result;
    }

    public static PyResult GetBool(PyCallContext context, PyObject obj)
    {
        TryGetBool(context, obj, out _, out var result);
        return result;
    }

    public static PyResult GetInt(PyCallContext context, PyObject obj)
    {
        TryGetInt(context, obj, out _, out var result);
        return result;
    }

    public static PyResult GetFloat(PyCallContext context, PyObject obj)
    {
        TryGetFloat(context, obj, out _, out var result);
        return result;
    }

    public static PyResult GetLen(PyCallContext context, PyObject obj)
    {
        TryGetLen(context, obj, out _, out var result);
        return result;
    }

    public static PyResult GetHash(PyCallContext context, PyObject obj)
    {
        TryGetHash(context, obj, out _, out var result);
        return result;
    }

    public static bool TryCastType<TPyObject>(PyObject obj, string objName, string expectedType, [NotNullWhen(true)] out TPyObject? result) where TPyObject : PyObject
    {
        if (obj is TPyObject objOfT)
        {
            result = objOfT;
            return true;
        }

        PyVirtualMachine.RaiseTypeError($"{objName} must be {expectedType}, not {obj.PyType.Name}");
        result = null;
        return false;
    }

    public static PyResult DivMod(PyCallContext context, PyObject left, PyObject right)
    {
        var ret = left.DivMod(context, right);
        if (!ret.IsNotImplemented)
            return ret;

        ret = right.RDivMod(context, left);
        return ret;
    }

    public static PyResult Abs(PyCallContext context, PyObject obj)
    {
        return obj.Abs(context);
    }

    public static PyResult Iter(PyCallContext context, PyObject obj)
    {
        return obj.Iter(context);
    }

    public static PyResult Next(PyCallContext context, PyObject obj)
    {
        return obj.Next(context);
    }
}