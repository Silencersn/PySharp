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
        var func = obj.PyType.Slots.Str;
        if (func is not null)
            return CallUnaryFunction<PyStrObject>(context, obj, func, static o => $"{PySpecialNames.Str} returned non-string (type {o.PyType.FullName})");

        return CallUnaryFunction<PyStrObject>(context, obj, obj.PyType.DefaultStr, static o => $"{PySpecialNames.Str} returned non-string (type {o.PyType.FullName})");
    }

    public static PyResult<PyStrObject> Repr(PyCallContext context, PyObject obj)
    {
        var func = obj.PyType.Slots.Repr;
        if (func is not null)
            return CallUnaryFunction<PyStrObject>(context, obj, func, static o => $"{PySpecialNames.Repr} returned non-string (type {o.PyType.FullName})");

        return CallUnaryFunction<PyStrObject>(context, obj, obj.PyType.DefaultRepr, static o => $"{PySpecialNames.Repr} returned non-string (type {o.PyType.FullName})");
    }

    public static PyResult<PyBoolObject> Bool(PyCallContext context, PyObject obj)
    {
        var boolFunc = obj.PyType.Slots.Bool;
        if (boolFunc is not null)
            return CallUnaryFunction<PyBoolObject>(context, obj, boolFunc, static o => $"{PySpecialNames.Bool} returned non-bool (type {o.PyType.FullName})");

        return CallUnaryFunction<PyBoolObject>(context, obj, obj.PyType.DefaultBool, static o => $"{PySpecialNames.Bool} returned non-bool (type {o.PyType.FullName})");
    }

    public static PyResult<PyIntObject> Hash(PyCallContext context, PyObject obj)
    {
        var func = obj.PyType.Slots.Hash;
        if (func is not null)
            return CallUnaryFunction<PyIntObject>(context, obj, func, static o => $"{PySpecialNames.Hash} returned non-int (type {o.PyType.FullName})");

        return CallUnaryFunction<PyIntObject>(context, obj, obj.PyType.DefaultHash, static o => $"{PySpecialNames.Hash} returned non-int (type {o.PyType.FullName})");
    }

    public static PyResult<PyIntObject> Index(PyCallContext context, PyObject obj)
    {
        var func = obj.PyType.Slots.Index;
        if (func is not null)
            return CallUnaryFunction<PyIntObject>(context, obj, func, static o => $"{PySpecialNames.Index} returned non-int (type {o.PyType.FullName})");

        return PyResult.RaiseTypeError($"'{obj.PyType.FullName}' object cannot be interpreted as an integer").Of<PyIntObject>();
    }

    public static PyResult<PyFloatObject> Float(PyCallContext context, PyObject obj)
    {
        var func = obj.PyType.Slots.Float;
        if (func is not null)
            return CallUnaryFunction<PyFloatObject>(context, obj, func, static o => $"{PySpecialNames.Float} returned non-float (type {o.PyType.FullName})");

        return PyResult.RaiseTypeError($"float() argument must be a string or a real number, not '{obj.PyType.FullName}'").Of<PyFloatObject>();
    }

    public static PyResult<PyIntObject> Len(PyCallContext context, PyObject obj)
    {
        var func = obj.PyType.Slots.Len;
        if (func is not null)
            return CallUnaryFunction<PyIntObject>(context, obj, func, static o => $"{PySpecialNames.Len} returned non-int (type {o.PyType.FullName})");

        return PyResult.RaiseTypeError($"object of type '{obj.PyType.FullName}' has no len()").Of<PyIntObject>();
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