using PySharp.Modules.Builtins;
using PySharp.Runtime;
using PySharp.Runtime.Calls;
using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;

namespace PySharp.Modules;

public interface IPyObjectName
{
    string Name { get; }
}


public interface IPyObjectRecursiveRepr
{
    PyResult<PyStrObject> RecursiveRepr(PyCallContext context, HashSet<PyObject> ids);

    public static PyResult<PyStrObject> RecursiveRepr(PyCallContext context, PyObject pyObj)
    {
        ArgumentNullException.ThrowIfNull(pyObj);

        return RecursiveRepr(context, pyObj, []);
    }

    public static PyResult<PyStrObject> RecursiveRepr(PyCallContext context, PyObject pyObj, HashSet<PyObject> ids)
    {
        ArgumentNullException.ThrowIfNull(pyObj);

        if (pyObj is IPyObjectRecursiveRepr recursiveReprObj)
            return recursiveReprObj.RecursiveRepr(context, ids);

        return PySpecialMethods.Repr(context, pyObj);
    }

    public static bool TryGetRecursiveRepr(PyCallContext context, PyObject pyObj, HashSet<PyObject> ids, [NotNullWhen(true)] out PyStrObject? s, out PyResult<PyStrObject> result)
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

internal interface IPyAttributesObject
{
    public static IPyAttributesObject FrozenEmpty { get; } = new PyFrozenAttributesObject();

    void Add(string key, PyObject value);
    bool TryGetValue(string key, [NotNullWhen(true)] out PyObject? value);
    PyObject this[string key] { set; }
    bool ContainsKey(string key);
    bool Remove(string key);
    IEnumerator<KeyValuePair<string, PyObject>> GetEnumerator();
    PyObject Self { get; }

    private sealed class PyFrozenAttributesObject : PyObject, IPyAttributesObject
    {
        PyObject IPyAttributesObject.this[string key] { set => throw new KeyNotFoundException(); }

        PyObject IPyAttributesObject.Self => this;

        void IPyAttributesObject.Add(string key, PyObject value)
        {
            throw new NotSupportedException();
        }

        bool IPyAttributesObject.ContainsKey(string key)
        {
            return false;
        }

        IEnumerator<KeyValuePair<string, PyObject>> IPyAttributesObject.GetEnumerator()
        {
            return FrozenDictionary<string, PyObject>.Empty.GetEnumerator();
        }

        bool IPyAttributesObject.Remove(string key)
        {
            return false;
        }

        bool IPyAttributesObject.TryGetValue(string key, [NotNullWhen(true)] out PyObject? value)
        {
            value = null;
            return false;
        }
    }
}