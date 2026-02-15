using PySharp.Modules.Builtins;
using System.Collections.Frozen;

namespace PySharp.Runtime.Calls;

public static class PyObjectCallExtensions
{
    public static PyResult Call(this PyObject callable, PyCallContext context, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        return PySpecialMethods.Call(context, callable, args, kwargs);
    }

    public static PyResult Call(this PyObject callable, PyCallContext context, IReadOnlyList<PyObject> args)
    {
        return Call(callable, context, args, FrozenDictionary<string, PyObject>.Empty);
    }

    public static PyResult Call(this PyObject callable, PyCallContext context)
    {
        return Call(callable, context, []);
    }

    public static PyResult CallMethod(this PyObject obj, PyCallContext context, string methodName, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        var method = PyOperators.GetAttr(context, obj, methodName);
        if (method.IsError)
            return method;

        return Call(method.Value, context, args, kwargs);
    }

    public static PyResult CallMethod(this PyObject obj, PyCallContext context, string methodName, IReadOnlyList<PyObject> args)
    {
        return CallMethod(obj, context, methodName, args, FrozenDictionary<string, PyObject>.Empty);
    }

    public static PyResult CallMethod(this PyObject obj, PyCallContext context, string methodName)
    {
        return CallMethod(obj, context, methodName, []);
    }
}
