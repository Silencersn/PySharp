using PySharp.Modules.Builtins;
using PySharp.Runtime.Calls;

namespace PySharp.Runtime;

public static class PySpecialMethods
{
    private static PyResult<TObject> ValidateResultOf<TObject>(PyResult result, Func<PyObject, string> getErrMsg) where TObject : PyObject
    {
        if (result.IsError)
            return result.ExceptionResult;

        if (result.Value is not TObject objOfT)
            return PyResult.TypeError(getErrMsg(result.Value));

        return objOfT;
    }

    public static PyResult<PyStrObject> Str(PyCallContext context, PyObject obj)
    {
        var func = obj.PyType.Slots.Str ?? PyTypeObject.DefaultStr;
        var result = func(context, obj);
        return ValidateResultOf<PyStrObject>(result, MessageCreator);

        static string MessageCreator(PyObject o)
        {
            return PySR.Format(PySR.Runtime_Object_SpecialMethodReturnsWrongType, PySpecialNames.Str, "string", o.PyType.FullName);
        }
    }

    public static PyResult<PyStrObject> Repr(PyCallContext context, PyObject obj)
    {
        var func = obj.PyType.Slots.Repr ?? PyTypeObject.DefaultRepr;
        var result = func(context, obj);
        return ValidateResultOf<PyStrObject>(result, MessageCreator);

        static string MessageCreator(PyObject o)
        {
            return PySR.Format(PySR.Runtime_Object_SpecialMethodReturnsWrongType, PySpecialNames.Repr, "string", o.PyType.FullName);
        }
    }

    public static PyResult<PyBoolObject> Bool(PyCallContext context, PyObject obj)
    {
        var boolFunc = obj.PyType.Slots.Bool;
        if (boolFunc is not null)
            return ValidateResultOf<PyBoolObject>(boolFunc(context, obj), MessageCreator);

        var lenFunc = obj.PyType.Slots.Len;
        if (lenFunc is not null)
        {
            var result = Len(context, obj);
            if (result.IsError)
                return result.ExceptionResult;

            return PyBoolObject.FromBoolean(result.Value.Value > 0);
        }

        return ValidateResultOf<PyBoolObject>(PyTypeObject.DefaultBool(context, obj), MessageCreator);

        static string MessageCreator(PyObject o)
        {
            return PySR.Format(PySR.Runtime_Object_SpecialMethodReturnsWrongType, PySpecialNames.Bool, "bool", o.PyType.FullName);
        }
    }

    public static PyResult<PyIntObject> Hash(PyCallContext context, PyObject obj)
    {
        var func = obj.PyType.Slots.Hash;
        if (func is null)
            return PyResult.TypeError(PySR.Runtime_Object_Unhashable, obj.PyType.FullName);
        var hash = ValidateResultOf<PyIntObject>(func(context, obj), MessageCreator);
        if (hash.IsError || hash.Value.Value != -1)
            return hash;
        return PyIntObject.FromInteger(-2);

        static string MessageCreator(PyObject o)
        {
            return PySR.Format(PySR.Runtime_Object_SpecialMethodReturnsWrongType, PySpecialNames.Hash, "int", o.PyType.FullName);
        }
    }

    public static PyResult<PyIntObject> Index(PyCallContext context, PyObject obj)
    {
        if (obj is PyIntObject intObj)
            return intObj;

        var func = obj.PyType.Slots.Index;
        if (func is not null)
            return ValidateResultOf<PyIntObject>(func(context, obj), MessageCreator);

        return PyResult.TypeError(PySR.Runtime_Number_Int_CannotInterpretedAsInt, obj.PyType.FullName);

        static string MessageCreator(PyObject o)
        {
            return PySR.Format(PySR.Runtime_Object_SpecialMethodReturnsWrongType, PySpecialNames.Index, "int", o.PyType.FullName);
        }
    }

    public static PyResult<PyFloatObject> Float(PyCallContext context, PyObject obj)
    {
        var func = obj.PyType.Slots.Float;
        if (func is not null)
            return ValidateResultOf<PyFloatObject>(func(context, obj), MessageCreator);

        return PyResult.TypeError(PySR.Runtime_Number_Float_WrongArg, obj.PyType.FullName);

        static string MessageCreator(PyObject o)
        {
            return PySR.Format(PySR.Runtime_Object_SpecialMethodReturnsWrongType, PySpecialNames.Float, "float", o.PyType.FullName);
        }
    }

    public static PyResult<PyIntObject> Len(PyCallContext context, PyObject obj)
    {
        var func = obj.PyType.Slots.Len;
        if (func is not null)
        {
            var result = ValidateResultOf<PyIntObject>(func(context, obj), MessageCreator);
            if (result.IsError)
                return result;

            if (result.Value.Value >= 0)
                return result;

            return PyResult.ValueError(PySR.Runtime_Sequence_NegativeLen);
        }

        return PyResult.TypeError(PySR.Runtime_Sequence_NoLen, obj.PyType.FullName);

        static string MessageCreator(PyObject o)
        {
            return PySR.Format(PySR.Runtime_Object_SpecialMethodReturnsWrongType, PySpecialNames.Len, "int", o.PyType.FullName);
        }
    }

    public static PyResult Iter(PyCallContext context, PyObject obj)
    {
        var iterFunc = obj.PyType.Slots.Iter;
        if (iterFunc is not null)
            return iterFunc(context, obj);

        var getItemFunc = obj.PyType.Slots.GetItem;
        if (getItemFunc is not null)
            return new PyIteratorObject(obj);

        return PyResult.TypeError(PySR.Runtime_Sequence_NonIterable, obj.PyType.FullName);
    }

    public static PyResult Await(PyCallContext context, PyObject obj)
    {
        var func = obj.PyType.Slots.Await;
        if (func is not null)
            return func(context, obj);

        return PyResult.TypeError(PySR.Runtime_Async_NonAwaitable, obj.PyType.FullName);
    }

    public static PyResult AIter(PyCallContext context, PyObject obj)
    {
        var func = obj.PyType.Slots.AIter;
        if (func is not null)
            return func(context, obj);

        return PyResult.TypeError(PySR.Runtime_AsyncFor_MissingAIter, obj.PyType.FullName);
    }

    public static PyResult Next(PyCallContext context, PyObject obj)
    {
        var func = obj.PyType.Slots.Next;
        if (func is null)
            return PyResult.TypeError(PySR.Runtime_Sequence_IterReturnsNonIterator, obj.PyType.FullName);

        return func(context, obj);
    }

    public static PyResult GetItem(PyCallContext context, PyObject obj, PyObject key)
    {
        var func = obj.PyType.Slots.GetItem;
        if (func is null)
            return PyResult.TypeError(PySR.Runtime_Sequence_NonSubscriptable, obj.PyType.FullName);

        return func(context, obj, key);
    }

    public static PyResult SetItem(PyCallContext context, PyObject obj, PyObject key, PyObject value)
    {
        var func = obj.PyType.Slots.SetItem;
        if (func is null)
            return PyResult.TypeError(PySR.Runtime_Sequence_NonSubscriptable, obj.PyType.FullName);

        return func(context, obj, key, value);
    }

    public static PyResult DelItem(PyCallContext context, PyObject obj, PyObject key)
    {
        var func = obj.PyType.Slots.DelItem;
        if (func is null)
            return PyResult.TypeError(PySR.Runtime_Sequence_NonSubscriptable, obj.PyType.FullName);

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

        return PyResult.TypeError(PySR.Runtime_Operator_UnsupportedForDivmod, left.PyType.FullName, right.PyType.FullName);
    }

    public static PyResult Abs(PyCallContext context, PyObject obj)
    {
        var func = obj.PyType.Slots.Abs;
        if (func is not null)
            return func(context, obj);

        return PyResult.TypeError(PySR.Runtime_Operator_UnsupportedForAbs, obj.PyType.FullName);
    }

    public static PyResult Call(PyCallContext context, PyObject callable, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        var func = callable.PyType.Slots.Call;
        if (func is not null)
            return func(context, callable, args, kwargs);

        return PyResult.TypeError(PySR.Runtime_Object_NonCallable, callable.PyType.FullName);
    }

    public static PyResult<PyStrObject> Format(PyCallContext context, PyObject obj, PyObject formatSpec)
    {
        var func = obj.PyType.Slots.Format ?? PyTypeObject.DefaultFormat;
        var result = func(context, obj, formatSpec);
        return ValidateResultOf<PyStrObject>(result, MessageCreator);

        static string MessageCreator(PyObject o)
        {
            return PySR.Format(PySR.Runtime_Object_FormatReturnsNonString, o.PyType.FullName);
        }
    }

    public static PyResult Round(PyCallContext context, PyObject obj, PyObject ndigits)
    {
        var func = obj.PyType.Slots.Round;
        if (func is not null)
            return func(context, obj, ndigits);

        return PyResult.TypeError(null);
    }

    public static PyResult Trunc(PyCallContext context, PyObject obj)
    {
        var func = obj.PyType.Slots.Trunc;
        if (func is not null)
            return func(context, obj);

        return PyResult.TypeError(null);
    }

    public static PyResult Floor(PyCallContext context, PyObject obj)
    {
        var func = obj.PyType.Slots.Floor;
        if (func is not null)
            return func(context, obj);

        return PyResult.TypeError(null);
    }

    public static PyResult Ceil(PyCallContext context, PyObject obj)
    {
        var func = obj.PyType.Slots.Ceil;
        if (func is not null)
            return func(context, obj);

        return PyResult.TypeError(null);
    }

    public static PyResult<PyIntObject> Int(PyCallContext context, PyObject obj)
    {
        var toInt = obj.PyType.Slots.Int;
        if (toInt is not null)
        {
            var result = toInt(context, obj);
            if (result.IsError)
                return result.ExceptionResult;

            if (result.Value is not PyIntObject intObj)
            {
                return PyResult.TypeError(PySR.Runtime_Object_SpecialMethodReturnsWrongType,
                    PySpecialNames.Int, "int", result.Value.PyType.FullName);
            }

            return intObj;
        }

        var index = obj.PyType.Slots.Index;
        if (index is not null)
        {
            var result = index(context, obj);
            if (result.IsError)
                return result.ExceptionResult;

            if (result.Value is not PyIntObject intObj)
            {
                return PyResult.TypeError(PySR.Runtime_Object_SpecialMethodReturnsWrongType,
                    PySpecialNames.Index, "int", result.Value.PyType.FullName);
            }

            return intObj;
        }

        return PyResult.TypeError(PySR.Runtime_Number_Int_WrongArg, obj.PyType.FullName);
    }
}