using PySharp.PyModules.Builtins;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;

namespace PySharp.PyRuntime;

public static class PySpecialMethods
{
    private static bool TryGetSpecialMethod<TPyObject>(Func<PyObject?> func, Func<PyObject, string> msgCreator, [NotNullWhen(true)] out TPyObject? o) where TPyObject : PyObject
    {
        var obj = func();
        if (obj is null)
        {
            o = null;
            return false;
        }

        if (obj is not TPyObject objOfT)
        {
            o = null;
            PyVirtualMachine.RaiseTypeError(msgCreator(obj));
            return false;
        }

        o = objOfT;
        return true;
    }

    public static bool TryGetStr(PyObject obj, [NotNullWhen(true)] out PyStrObject? s)
    {
        return TryGetSpecialMethod(obj.Str, o => $"{PySpecialNames.Str} returned non-string (type {o.PyType.Name})", out s);
    }

    public static bool TryGetRepr(PyObject obj, [NotNullWhen(true)] out PyStrObject? s)
    {
        return TryGetSpecialMethod(obj.Repr, o => $"{PySpecialNames.Repr} returned non-string (type {o.PyType.Name})", out s);
    }

    public static bool TryGetBool(PyObject obj, [NotNullWhen(true)] out PyBoolObject? b)
    {
        return TryGetSpecialMethod(obj.Bool, o => $"{PySpecialNames.Bool} should return bool, returned {o.PyType.Name}", out b);
    }

    public static bool TryGetInt(PyObject obj, [NotNullWhen(true)] out PyIntObject? i)
    {
        return TryGetSpecialMethod(obj.Int, o => $"{PySpecialNames.Int} returned non-int (type {o.PyType.Name})", out i);
    }

    public static bool TryGetFloat(PyObject obj, [NotNullWhen(true)] out PyFloatObject? f)
    {
        return TryGetSpecialMethod(obj.Float, o => $"{PySpecialNames.Float} returned non-float (type {o.PyType.Name})", out f);
    }

    public static bool TryGetLen(PyObject obj, [NotNullWhen(true)] out PyIntObject? i)
    {
        return TryGetSpecialMethod(obj.Len, o => $"{PySpecialNames.Len} returned non-int (type {o.PyType.Name})", out i);
    }

    private static readonly BigInteger _maxHash = new(uint.MaxValue);
    public static bool TryGetHash(PyObject obj, [NotNullWhen(true)] out PyIntObject? i)
    {
        if (!TryGetSpecialMethod(obj.Hash, o => $"{PySpecialNames.Hash} returned non-int (type {o.PyType.Name})", out i))
            return false;

        var value = i.Value;
        if (value == -1)
            value = -2;

        value &= _maxHash;
        i = PyIntObject.FromInteger(value);
        return true;
    }

    public static bool TryGetIndex(PyObject obj, [NotNullWhen(true)] out PyIntObject? i)
    {
        return TryGetSpecialMethod(obj.Index, o => $"{PySpecialNames.Index} returned non-int (type {o.PyType.Name})", out i);
    }

    public static PyStrObject? GetStr(PyObject obj)
    {
        return TryGetStr(obj, out var s) ? s : null;
    }

    public static PyStrObject? GetRepr(PyObject obj)
    {
        return TryGetRepr(obj, out var s) ? s : null;
    }

    public static PyBoolObject? GetBool(PyObject obj)
    {
        return TryGetBool(obj, out var s) ? s : null;
    }

    public static PyIntObject? GetInt(PyObject obj)
    {
        return TryGetInt(obj, out var s) ? s : null;
    }

    public static PyFloatObject? GetFloat(PyObject obj)
    {
        return TryGetFloat(obj, out var s) ? s : null;
    }

    public static PyIntObject? GetLen(PyObject obj)
    {
        return TryGetLen(obj, out var s) ? s : null;
    }

    public static PyIntObject? GetHash(PyObject obj)
    {
        return TryGetHash(obj, out var s) ? s : null;
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

    public static bool TryCastType<TPyObject>(PyObject obj, string objName, string expectedType, TPyObject valueIfNone, [NotNullWhen(true)] out TPyObject? result) where TPyObject : PyObject
    {
        if (obj is TPyObject objOfT)
        {
            result = objOfT;
            return true;
        }

        if (obj is PyNoneObject)
        {
            result = valueIfNone;
            return true;
        }

        PyVirtualMachine.RaiseTypeError($"{objName} must be None or {expectedType}, not {obj.PyType.Name}");
        result = null;
        return false;
    }
    
    public static PyObject? DivMod(PyObject left, PyObject right)
    {
        var ret = left.DivMod(right);
        if (ret is not PyNotImplementedObject)
            return ret;

        ret = right.RDivMod(left);
        return ret;
    }

    public static PyObject? Abs(PyObject obj)
    {
        return obj.Abs();
    }

    public static PyObject? Iter(PyObject obj)
    {
        return obj.Iter(); 
    }

    public static PyObject? Next(PyObject obj)
    {
        return obj.Next();
    }
}