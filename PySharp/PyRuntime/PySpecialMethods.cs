using PySharp.PyModules.Builtins;
using PySharp.PyRuntime.Calls;
using System.Collections.Frozen;

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

        return CallUnaryFunction<PyStrObject>(context, obj, PyTypeObject.DefaultStr, static o => $"{PySpecialNames.Str} returned non-string (type {o.PyType.FullName})");
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

        var lenFunc = obj.PyType.Slots.Len;
        if (lenFunc is not null)
        {
            var result = Len(context, obj);
            if (result.IsError)
                return result.Of<PyBoolObject>();

            return PyBoolObject.FromBoolean(result.Value.Value > 0);
        }

        return CallUnaryFunction<PyBoolObject>(context, obj, PyTypeObject.DefaultBool, static o => $"{PySpecialNames.Bool} returned non-bool (type {o.PyType.FullName})");
    }

    public static PyResult<PyIntObject> Hash(PyCallContext context, PyObject obj)
    {
        var func = obj.PyType.Slots.Hash;
        if (func is not null)
            return CallUnaryFunction<PyIntObject>(context, obj, func, static o => $"{PySpecialNames.Hash} returned non-int (type {o.PyType.FullName})");

        return CallUnaryFunction<PyIntObject>(context, obj, PyTypeObject.DefaultHash, static o => $"{PySpecialNames.Hash} returned non-int (type {o.PyType.FullName})");
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
        {
            var result = CallUnaryFunction<PyIntObject>(context, obj, func, static o => $"{PySpecialNames.Len} returned non-int (type {o.PyType.FullName})");
            if (result.IsError)
                return result;

            if (result.Value.Value >= 0)
                return result;

            return PyResult.RaiseValueError("__len__() should return >= 0").Of<PyIntObject>();
        }

        return PyResult.RaiseTypeError($"object of type '{obj.PyType.FullName}' has no len()").Of<PyIntObject>();
    }

    public static PyResult Iter(PyCallContext context, PyObject obj)
    {
        var iterFunc = obj.PyType.Slots.Iter;
        if (iterFunc is not null)
            return iterFunc(context, obj);

        var getItemFunc = obj.PyType.Slots.GetItem;
        if (getItemFunc is not null)
            return new PyIteratorObject(obj);

        return PyResult.RaiseTypeError($"'{obj.PyType.FullName}' object is not iterable");
    }

    public static PyResult Next(PyCallContext context, PyObject obj)
    {
        var func = obj.PyType.Slots.Next;
        if (func is null)
            return PyResult.RaiseTypeError($"iter() returned non-iterator of type '{obj.PyType.FullName}'");

        return func(context, obj);
    }

    public static PyResult GetItem(PyCallContext context, PyObject obj, PyObject key)
    {
        var func = obj.PyType.Slots.GetItem;
        if (func is null)
            return PyResult.RaiseTypeError($"'{obj.PyType.FullName}' object is not subscriptable");

        return func(context, obj, key);
    }

    public static PyResult SetItem(PyCallContext context, PyObject obj, PyObject key, PyObject value)
    {
        var func = obj.PyType.Slots.SetItem;
        if (func is null)
            return PyResult.RaiseTypeError($"'{obj.PyType.FullName}' object is not subscriptable");

        return func(context, obj, key, value);
    }

    public static PyResult DelItem(PyCallContext context, PyObject obj, PyObject key)
    {
        var func = obj.PyType.Slots.DelItem;
        if (func is null)
            return PyResult.RaiseTypeError($"'{obj.PyType.FullName}' object is not subscriptable");

        return func(context, obj, key);
    }

    public static PyResult Contains(PyCallContext context, PyObject obj, PyObject item)
    {
        var func = obj.PyType.Slots.Contains;
        if (func is not null)
            return func(context, obj, item);

        var iter = Iter(context, obj);
        if (iter.IsError)
            return iter;

        var element = Next(context, iter.Value);
        while (!element.IsStopIteration)
        {
            if (element.IsError)
                return element;

            var eq = PyOperators.Eq(context, element.Value, item);
            if (eq.IsError)
                return eq;

            var b = Bool(context, eq.Value);
            if (b.IsError)
                return b;

            if (b.Value.BoolValue)
                return PyBoolObject.True;

            element = Next(context, iter.Value);
        }

        return PyBoolObject.False;
    }

    public static PyResult DivMod(PyCallContext context, PyObject left, PyObject right)
    {
        var func = left.PyType.Slots.DivMod;
        if (func is not null)
        {
            var result = func(context, left, right);
            if (!result.IsNotImplemented)
                return result;
        }

        func = right.PyType.Slots.RDivMod;
        if (func is not null)
        {
            var result = func(context, right, left);
            if (!result.IsNotImplemented)
                return result;
        }

        return PyResult.RaiseTypeError($"unsupported operand type(s) for divmod(): '{left.PyType.FullName}' and '{right.PyType.FullName}'");
    }

    public static PyResult Abs(PyCallContext context, PyObject obj)
    {
        var func = obj.PyType.Slots.Abs;
        if (func is not null)
            return func(context, obj);

        return PyResult.RaiseTypeError($"bad operand type for abs(): '{obj.PyType.FullName}'");
    }

    public static PyResult Call(PyCallContext context, PyObject callable, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        var func = callable.PyType.Slots.Call;
        if (func is not null)
            return func(context, callable, args, kwargs);

        return PyResult.RaiseTypeError($"'{callable.PyType.FullName}' object is not callable");
    }

    public static PyResult Call(this PyObject callable, PyCallContext context, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        return Call(context, callable, args, kwargs);
    }

    public static PyResult Call(this PyObject callable, PyCallContext context, IReadOnlyList<PyObject> args)
    {
        return Call(context, callable, args, FrozenDictionary<string, PyObject>.Empty);
    }

    public static PyResult Format(PyCallContext context, PyObject obj, PyObject formatSpec)
    {
        var func = obj.PyType.Slots.Format ?? PyTypeObject.DefaultFormat;
        return func(context, obj, formatSpec);
    }
}