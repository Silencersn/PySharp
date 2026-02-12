using PySharp.PyModules.Builtins;
using PySharp.PyRuntime.Comparison;
using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace PySharp.Utility;

internal sealed class StringKeyDict : IDictionary<string, PyObject>
{
    private readonly IDictionary<PyObject, PyObject> _dict;

    public StringKeyDict(IDictionary<PyObject, PyObject> dict)
    {
        _dict = dict;
    }

    private static PyStrObject ConvertKey(string key)
    {
        return PyStrObject.FromString(key);
    }

    PyObject IDictionary<string, PyObject>.this[string key]
    {
        get => _dict[ConvertKey(key)];
        set => _dict[ConvertKey(key)] = value;
    }

    ICollection<string> IDictionary<string, PyObject>.Keys
        => [.. GetStringKeys()];

    ICollection<PyObject> IDictionary<string, PyObject>.Values
        => [.. _dict.Where(static pair => pair.Key is PyStrObject).Select(static pair => pair.Value)];

    int ICollection<KeyValuePair<string, PyObject>>.Count
        => _dict.Count(static pair => pair.Key is PyStrObject);

    bool ICollection<KeyValuePair<string, PyObject>>.IsReadOnly
        => _dict.IsReadOnly;

    void IDictionary<string, PyObject>.Add(string key, PyObject value)
    {
        _dict.Add(ConvertKey(key), value);
    }

    void ICollection<KeyValuePair<string, PyObject>>.Add(KeyValuePair<string, PyObject> item)
    {
        _dict.Add(ConvertKey(item.Key), item.Value);
    }

    void ICollection<KeyValuePair<string, PyObject>>.Clear()
    {
        foreach (var key in GetStringKeys())
            _dict.Remove(ConvertKey(key));
    }

    bool ICollection<KeyValuePair<string, PyObject>>.Contains(KeyValuePair<string, PyObject> item)
    {
        return _dict.TryGetValue(ConvertKey(item.Key), out var value) && PyObjectComparer.Default.Equals(value, item.Value);
    }

    bool IDictionary<string, PyObject>.ContainsKey(string key)
    {
        return _dict.ContainsKey(ConvertKey(key));
    }

    void ICollection<KeyValuePair<string, PyObject>>.CopyTo(KeyValuePair<string, PyObject>[] array, int arrayIndex)
    {
        foreach (var kv in _dict)
        {
            if (kv.Key is PyStrObject strKey)
                array[arrayIndex++] = new KeyValuePair<string, PyObject>(strKey.Value, kv.Value);
        }
    }

    IEnumerator<KeyValuePair<string, PyObject>> IEnumerable<KeyValuePair<string, PyObject>>.GetEnumerator()
    {
        foreach (var kv in _dict)
        {
            if (kv.Key is PyStrObject strKey)
                yield return new KeyValuePair<string, PyObject>(strKey.Value, kv.Value);
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
        => ((IEnumerable<KeyValuePair<string, PyObject>>)this).GetEnumerator();

    bool IDictionary<string, PyObject>.Remove(string key)
    {
        return _dict.Remove(ConvertKey(key));
    }

    bool ICollection<KeyValuePair<string, PyObject>>.Remove(KeyValuePair<string, PyObject> item)
    {
        if (_dict.TryGetValue(ConvertKey(item.Key), out var value) && EqualityComparer<PyObject>.Default.Equals(value, item.Value))
        {
            return _dict.Remove(ConvertKey(item.Key));
        }
        return false;
    }

    bool IDictionary<string, PyObject>.TryGetValue(string key, [NotNullWhen(true)] out PyObject? value)
    {
        return _dict.TryGetValue(ConvertKey(key), out value);
    }

    private IEnumerable<string> GetStringKeys()
    {
        foreach (var key in _dict.Keys)
        {
            if (key is PyStrObject strKey)
                yield return strKey.Value;
        }
    }
}
