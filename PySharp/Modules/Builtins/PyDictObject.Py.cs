using PySharp.Runtime;
using PySharp.Runtime.Calls;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace PySharp.Modules.Builtins;

partial class PyDictObject
{
    public PyDictItemsObject PyItems()
    {
        return new PyDictItemsObject(this);
    }

    public void PySetItem(PyObject key, PyObject value)
    {
        _dict[key] = value;
    }

    public bool PyTryGet(PyObject key, [NotNullWhen(true)] out PyObject? value)
    {
        return _dict.TryGetValue(key, out value);
    }

    public void PyClear()
    {
        _dict.Clear();
    }

    public PyDictObject PyCopy()
    {
        return CreateDict(_dict);
    }

    public bool PyTryPop(PyObject key, [NotNullWhen(true)] out PyObject? value)
    {
        return _dict.Remove(key, out value);
    }

    public bool PyTryPopItem([NotNullWhen(true)] out PyObject? key, [NotNullWhen(true)] out PyObject? value)
    {
        if (_dict.Count is 0)
        {
            key = null;
            value = null;
            return false;
        }

        (key, value) = _dict.ElementAt(_dict.Count - 1);
        var removed = _dict.Remove(key);
        Debug.Assert(removed);
        return true;
    }

    public void PyUpdate(IEnumerable<KeyValuePair<PyObject, PyObject>> pairs)
    {
        foreach (var pair in pairs)
            PySetItem(pair.Key, pair.Value);
    }

    public PyResult PyUpdate(PyCallContext context, PyObject iterableOrMapping)
    {
        var dict = PyUtils.ToDict(context, iterableOrMapping);
        if (dict.IsError)
            return dict;

        PyUpdate(dict.Value._dict);
        return PyNoneObject.None;
    }

    public PyObject PySetDefault(PyObject key, PyObject defaultValue)
    {
        if (_dict.TryGetValue(key, out var value))
            return value;
        return _dict[key] = defaultValue;
    }
}
