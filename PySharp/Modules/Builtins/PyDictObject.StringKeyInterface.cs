using PySharp.Runtime;
using PySharp.Runtime.Calls;
using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace PySharp.Modules.Builtins;

partial class PyDictObject : IPyVariablesLocalsDict, IDictionary<string, PyObject>
{
    private IEnumerable<string> GetStringKeys()
    {
        // TODO: perf
        foreach (var entry in Entries.ToArray())
        {
            if (entry.Key is PyStrObject strKey)
                yield return strKey.Value;
        }
    }

    PyObject IDictionary<string, PyObject>.this[string key]
    {
        get => GetItem(key).PyUnwrap(PyCallContext.CSharpRuntime);
        set => SetItem(key, value);
    }
    PyObject IPyVariablesLocalsDict.this[string key]
    {
        get => GetItem(key).PyUnwrap(PyCallContext.CSharpRuntime);
        set => SetItem(key, value);
    }

    ICollection<string> IDictionary<string, PyObject>.Keys => [.. GetStringKeys()];

    int ICollection<KeyValuePair<string, PyObject>>.Count => Entries.ToArray().Count(static pair => pair.Key is PyStrObject);

    ICollection<PyObject> IDictionary<string, PyObject>.Values => [.. Entries.ToArray().Where(static pair => pair.Key is PyStrObject).Select(static pair => pair.Value)];

    bool ICollection<KeyValuePair<string, PyObject>>.IsReadOnly => throw new NotImplementedException();

    void IDictionary<string, PyObject>.Add(string key, PyObject value)
    {
        if (ContainsKey(key))
            throw new ArgumentException(null, nameof(key));
        SetItem(key, value);
    }

    void ICollection<KeyValuePair<string, PyObject>>.Add(KeyValuePair<string, PyObject> item)
    {
        throw new NotSupportedException();
    }

    void ICollection<KeyValuePair<string, PyObject>>.Clear()
    {
        throw new NotSupportedException();
    }

    bool ICollection<KeyValuePair<string, PyObject>>.Contains(KeyValuePair<string, PyObject> item)
    {
        throw new NotSupportedException();
    }

    void ICollection<KeyValuePair<string, PyObject>>.CopyTo(KeyValuePair<string, PyObject>[] array, int arrayIndex)
    {
        throw new NotSupportedException();
    }

    IEnumerator<KeyValuePair<string, PyObject>> IEnumerable<KeyValuePair<string, PyObject>>.GetEnumerator()
    {
        foreach (var entry in Entries.ToArray())
        {
            if (entry.Key is PyStrObject { Value: var str})
                yield return KeyValuePair.Create(str, entry.Value);
        }
    }

    bool IDictionary<string, PyObject>.Remove(string key)
    {
        return DelItem(PyCallContext.NotImplemented, PyStrObject.FromString(key)).IsSuccessful;
    }

    bool ICollection<KeyValuePair<string, PyObject>>.Remove(KeyValuePair<string, PyObject> item)
    {
        throw new NotSupportedException();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        throw new NotImplementedException();
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
}
