using System.Diagnostics.CodeAnalysis;

namespace PySharp.Modules.Builtins;

partial class PyDictObject : IDictionary<string, PyObject>, IReadOnlyDictionary<string, PyObject>
{
    private static PyStrObject ConvertKey(string key)
    {
        return PyStrObject.FromString(key);
    }

    private static KeyValuePair<PyObject, PyObject> ConvertPair(KeyValuePair<string, PyObject> pair)
    {
        return KeyValuePair.Create(ConvertKey(pair.Key) as PyObject, pair.Value);
    }

    private IEnumerable<string> GetStringKeys()
    {
        foreach (var key in _dict.Keys)
        {
            if (key is PyStrObject strKey)
                yield return strKey.Value;
        }
    }

    PyObject IDictionary<string, PyObject>.this[string key]
    {
        get => _dict[ConvertKey(key)];
        set => _dict[ConvertKey(key)] = value;
    }

    PyObject IReadOnlyDictionary<string, PyObject>.this[string key] => ((IDictionary<string, PyObject>)this)[key];

    ICollection<string> IDictionary<string, PyObject>.Keys => [.. GetStringKeys()];

    IEnumerable<string> IReadOnlyDictionary<string, PyObject>.Keys => ((IDictionary<string, PyObject>)this).Keys;

    int ICollection<KeyValuePair<string, PyObject>>.Count => _dict.Count(static pair => pair.Key is PyStrObject);
    int IReadOnlyCollection<KeyValuePair<string, PyObject>>.Count => _dict.Count(static pair => pair.Key is PyStrObject);

    ICollection<PyObject> IDictionary<string, PyObject>.Values => [.. _dict.Where(static pair => pair.Key is PyStrObject).Select(static pair => pair.Value)];
    
    IEnumerable<PyObject> IReadOnlyDictionary<string, PyObject>.Values => [.. _dict.Where(static pair => pair.Key is PyStrObject).Select(static pair => pair.Value)];

    void IDictionary<string, PyObject>.Add(string key, PyObject value)
    {
        Add(ConvertKey(key), value);
    }

    void ICollection<KeyValuePair<string, PyObject>>.Add(KeyValuePair<string, PyObject> item)
    {
        Add(ConvertPair(item));
    }

    void ICollection<KeyValuePair<string, PyObject>>.Clear()
    {
        foreach (var key in GetStringKeys())
            _dict.Remove(ConvertKey(key));
    }

    bool ICollection<KeyValuePair<string, PyObject>>.Contains(KeyValuePair<string, PyObject> item)
    {
        return Contains(ConvertPair(item));
    }

    bool IDictionary<string, PyObject>.ContainsKey(string key)
    {
        return ContainsKey(ConvertKey(key));
    }

    bool IReadOnlyDictionary<string, PyObject>.ContainsKey(string key)
    {
        return ContainsKey(ConvertKey(key));
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

    bool IDictionary<string, PyObject>.Remove(string key)
    {
        return _dict.Remove(ConvertKey(key));
    }

    bool ICollection<KeyValuePair<string, PyObject>>.Remove(KeyValuePair<string, PyObject> item)
    {
        return Remove(ConvertPair(item));
    }

    bool IDictionary<string, PyObject>.TryGetValue(string key, [NotNullWhen(true)] out PyObject? value)
    {
        return _dict.TryGetValue(ConvertKey(key), out value);
    }

    bool IReadOnlyDictionary<string, PyObject>.TryGetValue(string key, [NotNullWhen(true)] out PyObject? value)
    {
        return _dict.TryGetValue(ConvertKey(key), out value);
    }
}
