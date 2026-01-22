using PySharp.PyModules.Builtins;
using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;
using System.Diagnostics.CodeAnalysis;

namespace PySharp.PyModules;

public interface IPyObjectName
{
    string Name { get; }
}


public interface IPyObjectRecursiveRepr
{
    PyResult<PyStrObject> RecursiveRepr(PyCallContext context, HashSet<int> ids);

    public static PyResult<PyStrObject> RecursiveRepr(PyCallContext context, PyObject pyObj)
    {
        ArgumentNullException.ThrowIfNull(pyObj);

        return RecursiveRepr(context, pyObj, []);
    }

    public static PyResult<PyStrObject> RecursiveRepr(PyCallContext context, PyObject pyObj, HashSet<int> ids)
    {
        ArgumentNullException.ThrowIfNull(pyObj);

        if (pyObj is IPyObjectRecursiveRepr recursiveReprObj)
            return recursiveReprObj.RecursiveRepr(context, ids);

        return PySpecialMethods.Repr(context, pyObj);
    }

    public static bool TryGetRecursiveRepr(PyCallContext context, PyObject pyObj, HashSet<int> ids, [NotNullWhen(true)] out PyStrObject? s, out PyResult<PyStrObject> result)
    {
        ArgumentNullException.ThrowIfNull(pyObj);

        result = RecursiveRepr(context, pyObj, ids);
        if (result.IsError)
        {
            s = null;
            return false;
        }

        s = result.Value;
        return true;
    }
}