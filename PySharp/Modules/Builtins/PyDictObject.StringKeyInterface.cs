using PySharp.Runtime;
using PySharp.Runtime.Calls;
using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace PySharp.Modules.Builtins;

partial class PyDictObject : IPyVariablesLocalsDict, IPyAttributesObject
{
    PyObject? IPyVariablesLocalsDict.this[string key]
    {
        get => GetItem(key).PyUnwrap(PyCallContext.CSharpRuntime);
        set => InternalSetItem(key, value);
    }

    PyObject IPyAttributesObject.Self => this;

    PyObject IPyAttributesObject.this[string key]
    {
        set => SetItem(key, value);
    }

    bool IPyVariablesLocalsDict.Remove(string key) => DelItem(key);

    private IEnumerable<KeyValuePair<string, PyObject>> EnumerateStringKeyPair()
    {
        for (int i = 0; i < _count; i++)
        {
            var entry = _entries[i];
            if (entry.Key is PyStrObject { Value: var str })
                yield return KeyValuePair.Create(str, entry.Value)!;
        }
    }

    IEnumerator<KeyValuePair<string, PyObject?>> IPyVariablesLocalsDict.GetEnumerator() => EnumerateStringKeyPair().GetEnumerator()!;

    IEnumerator<KeyValuePair<string, PyObject>> IPyAttributesObject.GetEnumerator() => EnumerateStringKeyPair().GetEnumerator();

    bool IPyAttributesObject.Remove(string key) => DelItem(key);
}
