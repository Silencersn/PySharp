using PySharp.PyModules.Builtins;
using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace PySharp.Utility;

internal sealed class DictAdapter : IDictionary<PyObject, PyObject>
{
    private readonly IDictionary<string, PyObject?> _origDict;
    private readonly Dictionary<PyObject, PyObject> _extraDict;

    public DictAdapter(IDictionary<string, PyObject?> dict)
    {
        _origDict = dict;
        _extraDict = [];
    }

    PyObject IDictionary<PyObject, PyObject>.this[PyObject key]
    {
        get
        {
            if (key is PyStrObject strObj)
                return _origDict[strObj.Value] ?? throw new KeyNotFoundException(strObj.Value);
            return _extraDict[key];
        }
        set
        {
            if (key is PyStrObject strObj)
                _origDict[strObj.Value] = value;
            else
                _extraDict[key] = value;
        }
    }

    ICollection<PyObject> IDictionary<PyObject, PyObject>.Keys => [
            .. _extraDict.Keys,
                .. _origDict.Select(static kvp => PyStrObject.FromString(kvp.Key)),
            ];

    ICollection<PyObject> IDictionary<PyObject, PyObject>.Values => [
            .. _extraDict.Values,
                .. _origDict.Values.Where(static value => value is not null)!,
            ];

    int ICollection<KeyValuePair<PyObject, PyObject>>.Count => _origDict.Count(static kvp => kvp.Value is not null) + _extraDict.Count;

    bool ICollection<KeyValuePair<PyObject, PyObject>>.IsReadOnly => false;

    void IDictionary<PyObject, PyObject>.Add(PyObject key, PyObject value)
    {
        if (key is PyStrObject strObj)
            _origDict.Add(strObj.Value, value);
        else
            _extraDict[key] = value;
    }

    void ICollection<KeyValuePair<PyObject, PyObject>>.Add(KeyValuePair<PyObject, PyObject> item)
    {
        if (item.Key is PyStrObject strObj)
            _origDict.Add(strObj.Value, item.Value);
        else
            _extraDict[item.Key] = item.Value;
    }

    void ICollection<KeyValuePair<PyObject, PyObject>>.Clear()
    {
        _origDict.Clear();
        _extraDict.Clear();
    }

    bool ICollection<KeyValuePair<PyObject, PyObject>>.Contains(KeyValuePair<PyObject, PyObject> item)
    {
        if (item.Key is PyStrObject strObj)
            return _origDict.Contains(KeyValuePair.Create<string, PyObject?>(strObj.Value, item.Value));
        return _extraDict.Contains(item);
    }

    bool IDictionary<PyObject, PyObject>.ContainsKey(PyObject key)
    {
        if (key is PyStrObject strObj)
            return _origDict.ContainsKey(strObj.Value);
        return _extraDict.ContainsKey(key);
    }

    void ICollection<KeyValuePair<PyObject, PyObject>>.CopyTo(KeyValuePair<PyObject, PyObject>[] array, int arrayIndex)
    {
        foreach (var kvp in _origDict)
        {
            if (kvp.Value is null)
                continue;

            array[arrayIndex++] = KeyValuePair.Create<PyObject, PyObject>(PyStrObject.FromString(kvp.Key), kvp.Value);
        }
        foreach (var kvp in _extraDict)
        {
            array[arrayIndex++] = kvp;
        }
    }

    IEnumerator<KeyValuePair<PyObject, PyObject>> IEnumerable<KeyValuePair<PyObject, PyObject>>.GetEnumerator()
    {
        return _origDict
            .Where(static kvp => kvp.Value is not null)
            .Select(static kvp => KeyValuePair.Create((PyObject)PyStrObject.FromString(kvp.Key), kvp.Value!))
            .Concat(_extraDict)
            .GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return ((IEnumerable<KeyValuePair<PyObject, PyObject>>)this).GetEnumerator();
    }

    bool IDictionary<PyObject, PyObject>.Remove(PyObject key)
    {
        if (key is PyStrObject strObj)
            return _origDict.Remove(strObj.Value);
        return _extraDict.Remove(key);
    }

    bool ICollection<KeyValuePair<PyObject, PyObject>>.Remove(KeyValuePair<PyObject, PyObject> item)
    {
        if (item.Key is PyStrObject strObj)
            return _origDict.Remove(KeyValuePair.Create<string, PyObject?>(strObj.Value, item.Value));
        return ((ICollection<KeyValuePair<PyObject, PyObject>>)_extraDict).Remove(item);
    }

    bool IDictionary<PyObject, PyObject>.TryGetValue(PyObject key, [NotNullWhen(true)] out PyObject? value)
    {
        if (key is PyStrObject strObj)
            return _origDict.TryGetValue(strObj.Value, out value) && value is not null;
        return _extraDict.TryGetValue(key, out value);
    }
}
