using PySharp.Modules.Builtins;
using PySharp.Runtime.Calls;
using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace PySharp.Runtime;

// name-op storage for a class body running in a namespace handed back by a
// metaclass __prepare__ hook (PEP 3115): every access goes through the
// generic item protocol on the mapping, so dict subclasses with overridden
// __setitem__/__getitem__ and arbitrary user mappings all see the class
// body's stores. KeyError means "name not in the namespace" and falls back
// to globals/builtins like CPython's LOAD_NAME; any other error propagates.
internal sealed class PyPreparedNamespaceLocals(PyCallContext context, PyObject mapping) : IPyVariablesLocalsDict, IEnumerable
{
    internal PyObject Mapping => mapping;

    private PyRuntimeException Throwable(PyResult result)
    {
        Debug.Assert(result.IsError);
        return new PyRuntimeException(context, result.Exception);
    }

    private bool TryGetItem(string key, [MaybeNullWhen(false)] out PyObject? value)
    {
        var result = PySpecialMethods.GetItem(context, mapping, PyStrObject.FromString(key));
        if (result.IsError)
        {
            if (PyKeyErrorObjectType.Shared.IsInstance(result.Exception))
            {
                value = null;
                return false;
            }
            throw Throwable(result);
        }

        value = result.Value;
        return true;
    }

    public PyObject? this[string key]
    {
        get => TryGetItem(key, out var value) ? value : throw new KeyNotFoundException();
        set
        {
            // name stores never hand out managed null (the interface's
            // PyObject? only mirrors PyDictObject's looser contract)
            Debug.Assert(value is not null);
            var result = PySpecialMethods.SetItem(context, mapping, PyStrObject.FromString(key), value);
            if (result.IsError)
                throw Throwable(result);
        }
    }

    public bool TryGetValue(string key, [MaybeNullWhen(false)] out PyObject? value)
    {
        return TryGetItem(key, out value);
    }

    public bool Remove(string key)
    {
        var result = PySpecialMethods.DelItem(context, mapping, PyStrObject.FromString(key));
        if (!result.IsError)
            return true;

        if (PyKeyErrorObjectType.Shared.IsInstance(result.Exception))
            return false;

        throw Throwable(result);
    }

    public bool ContainsKey(string key)
    {
        return TryGetItem(key, out _);
    }

    // iterating a mapping yields its keys (CPython dict(mapping) protocol);
    // GetLocals() snapshots through this — non-string keys cannot be name
    // targets and are skipped
    public IEnumerator<KeyValuePair<string, PyObject?>> GetEnumerator()
    {
        var keys = PyUtils.IterableToList(context, mapping).PyUnwrap(context).AsSpan().ToArray();

        foreach (var key in keys)
        {
            if (key is not PyStrObject keyStr)
                continue;

            TryGetItem(keyStr.Value, out var value);
            yield return new KeyValuePair<string, PyObject?>(keyStr.Value, value);
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
