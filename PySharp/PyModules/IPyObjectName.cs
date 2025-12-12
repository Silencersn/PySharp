using PySharp.PyModules.Builtins;
using PySharp.PyRuntime;
using System.Diagnostics.CodeAnalysis;

namespace PySharp.PyModules;

public interface IPyObjectName
{
    string Name { get; }
}


public interface IPyObjectRecursiveRepr
{
    PyObject? RecursiveRepr(HashSet<int> ids);

    public static PyObject? RecursiveRepr(PyObject pyObj)
    {
        ArgumentNullException.ThrowIfNull(pyObj);

        return RecursiveRepr(pyObj, []);
    }

    public static PyObject? RecursiveRepr(PyObject pyObj, HashSet<int> ids)
    {
        ArgumentNullException.ThrowIfNull(pyObj);

        if (pyObj is IPyObjectRecursiveRepr recursiveReprObj)
            return recursiveReprObj.RecursiveRepr(ids);

        return pyObj.Repr();
    }

    public static bool TryGetRecursiveRepr(PyObject pyObj, HashSet<int> ids, [NotNullWhen(true)] out PyStrObject? s)
    {
        ArgumentNullException.ThrowIfNull(pyObj);

        var repr = RecursiveRepr(pyObj, ids);
        if (repr is null)
        {
            s = null;
            return false;
        }

        if (repr is not PyStrObject strObj)
        {
            s = null;
            PyVirtualMachine.RaiseTypeError($"{PySpecialNames.Repr} returned non-string (type {repr.PyType.Name})");
            return false;
        }

        s = strObj;
        return true;
    }
}

internal interface IPyDescriptor
{
    internal bool SupportsGet { get; }
    internal bool SupportsSet { get; }
    internal bool SupportsDelete { get; }
}