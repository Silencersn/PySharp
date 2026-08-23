using PySharp.Runtime;
using PySharp.Runtime.Calls;
using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace PySharp.Modules.Builtins;

partial class PyDictObject : IPyVariablesLocalsDict, IPyAttributesObject
{
    PyObject IPyVariablesLocalsDict.this[string key]
    {
        get => GetItem(key).PyUnwrap(PyCallContext.CSharpRuntime);
        set => SetItem(key, value);
    }

    PyObject IPyAttributesObject.Self => this;

    PyObject IPyAttributesObject.this[string key]
    {
        set => SetItem(key, value);
    }

    bool IPyVariablesLocalsDict.Remove(string key)
    {
        // TODO
        return DelItem(PyCallContext.NotImplemented, PyStrObject.FromString(key)).IsSuccessful;
    }

    IEnumerator<KeyValuePair<string, PyObject?>> IPyVariablesLocalsDict.GetEnumerator()
    {
        foreach (var entry in Entries.ToArray())
        {
            if (entry.Key is PyStrObject { Value: var str })
                yield return KeyValuePair.Create(str, entry.Value)!;
        }
    }

    void IPyAttributesObject.Add(string key, PyObject value)
    {
        if (ContainsKey(key))
            throw new ArgumentException(null, nameof(key));
        SetItem(key, value);
    }

    IEnumerator<KeyValuePair<string, PyObject>> IPyAttributesObject.GetEnumerator()
    {
        foreach (var entry in Entries.ToArray())
        {
            if (entry.Key is PyStrObject { Value: var str })
                yield return KeyValuePair.Create(str, entry.Value)!;
        }
    }

    bool IPyAttributesObject.Remove(string key)
    {
        // TODO
        return DelItem(PyCallContext.NotImplemented, PyStrObject.FromString(key)).IsSuccessful;
    }
}
