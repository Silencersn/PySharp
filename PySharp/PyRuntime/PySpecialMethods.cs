using PySharp.PyModules.Builtins;
using PySharp.PyRuntime.Calls;
using System.Diagnostics.CodeAnalysis;

namespace PySharp.PyRuntime;

public static class PySpecialMethods
{
    private static PyResult<TObject> CallUnaryFunction<TObject>(PyCallContext context, PyObject obj, PyUnaryFunction func, Func<PyObject, string> getErrMsg) where TObject : PyObject
    {
        var result = func(context, obj);
        if (result.IsError)
            return result.Of<TObject>();

        if (result.Value is not TObject objOfT)
            return PyResult.RaiseTypeError(getErrMsg(result.Value)).Of<TObject>();

        return objOfT;
    }

    public static PyResult<PyStrObject> Str(PyCallContext context, PyObject obj)
    {
        var str = obj.PyType.Slots.Str;
        if (str is not null)
            return CallUnaryFunction<PyStrObject>(context, obj, str, static o => $"{PySpecialNames.Str} returned non-string (type {o.PyType.Name})");

        return CallUnaryFunction<PyStrObject>(context, obj, obj.PyType.DefaultStr, static o => $"{PySpecialNames.Str} returned non-string (type {o.PyType.Name})");
    }

    public static PyResult<PyStrObject> Repr(PyCallContext context, PyObject obj)
    {
        var repr = obj.PyType.Slots.Repr;
        if (repr is not null)
            return CallUnaryFunction<PyStrObject>(context, obj, repr, static o => $"{PySpecialNames.Repr} returned non-string (type {o.PyType.Name})");

        return CallUnaryFunction<PyStrObject>(context, obj, obj.PyType.DefaultRepr, static o => $"{PySpecialNames.Repr} returned non-string (type {o.PyType.Name})");
    }

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

    public static bool TryGetBool(PyCallContext context, PyObject obj, [NotNullWhen(true)] out PyBoolObject? b, out PyResult result)
    {
        return TryGetSpecialMethod(() => obj.Bool(context), o => $"{PySpecialNames.Bool} should return bool, returned {o.PyType.Name}", out b, out result);
    }

    public static bool TryGetFloat(PyCallContext context, PyObject obj, [NotNullWhen(true)] out PyFloatObject? f, out PyResult result)
    {
        return TryGetSpecialMethod(() => obj.Float(context), o => $"{PySpecialNames.Float} returned non-float (type {o.PyType.Name})", out f, out result);
    }

    public static bool TryGetLen(PyCallContext context, PyObject obj, [NotNullWhen(true)] out PyIntObject? i, out PyResult result)
    {
        return TryGetSpecialMethod(() => obj.Len(context), o => $"{PySpecialNames.Len} returned non-int (type {o.PyType.Name})", out i, out result);
    }

    public static bool TryGetHash(PyCallContext context, PyObject obj, [NotNullWhen(true)] out PyIntObject? i, out PyResult result)
    {
        if (!TryGetSpecialMethod(() => obj.Hash(context), o => $"{PySpecialNames.Hash} returned non-int (type {o.PyType.Name})", out i, out result))
            return false;

        var value = unchecked((int)i.Value);
        if (value is -1)
            value = -2;

        i = PyIntObject.FromInteger(value);
        return true;
    }

    public static bool TryGetIndex(PyCallContext context, PyObject obj, [NotNullWhen(true)] out PyIntObject? i, out PyResult result)
    {
        return TryGetSpecialMethod(() => obj.Index(context), o => $"{PySpecialNames.Index} returned non-int (type {o.PyType.Name})", out i, out result);
    }

    public static PyResult GetBool(PyCallContext context, PyObject obj)
    {
        TryGetBool(context, obj, out _, out var result);
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