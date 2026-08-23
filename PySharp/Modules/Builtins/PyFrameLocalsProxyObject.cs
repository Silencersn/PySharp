using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.Comparison;
using PySharp.Runtime.PyAttributes;
using System;
using System.Collections;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace PySharp.Modules.Builtins;

internal sealed class PyFrameLocalsProxyObject : PyObject, IPyVariablesLocalsDict, IPyObjectRecursiveRepr
{
    private readonly FrozenDictionary<string, int> _localsTable;
    private readonly Memory<PyObject?> _localsPlusMemory;
    private Span<PyObject?> LocalsPlusSpan => _localsPlusMemory.Span;
    private PyDictObject? _extraLocals;

    internal PyDictObject? ExtraLocals => _extraLocals;

    public override PyTypeObject DefaultPyType => PyFrameLocalsProxyObjectType.Shared;

    internal PyFrameLocalsProxyObject(FrozenDictionary<string, int> localsTable, Memory<PyObject?> localsPlusMemory, PyDictObject? extraLocals)
    {
        _localsPlusMemory = localsPlusMemory;
        _localsTable = localsTable;
        _extraLocals = extraLocals;
    }
    internal PyFrameLocalsProxyObject(FrozenDictionary<string, int> localsTable, Memory<PyObject?> localsPlusMemory) : this(localsTable, localsPlusMemory, null)
    {
    }

    PyObject? IPyVariablesLocalsDict.this[string key]
    {
        get
        {
            if (_localsTable.TryGetValue(key, out var index))
                return LocalsPlusSpan[index];

            if (_extraLocals is null)
                return null;

            var ret = _extraLocals.GetItem(key);
            if (ret.IsSuccessful)
                return ret.Value;

            return null;
        }
        set
        {
            if (_localsTable.TryGetValue(key, out var index))
            {
                LocalsPlusSpan[index] = value;
                return;
            }

            _extraLocals ??= new();
            _extraLocals.InternalSetItem(key, value);
        }
    }

    bool IPyVariablesLocalsDict.ContainsKey(string key)
    {
        if (_localsTable.ContainsKey(key))
            return true;

        return _extraLocals?.ContainsKey(key) ?? false;
    }

    IEnumerator<KeyValuePair<string, PyObject?>> IPyVariablesLocalsDict.GetEnumerator()
    {
        foreach (var pair in _localsTable)
            yield return KeyValuePair.Create(pair.Key, LocalsPlusSpan[pair.Value]);

        if (_extraLocals is null)
            yield break;

        foreach (var pair in _extraLocals)
        {
            if (pair.Key is not PyStrObject { Value: var str })
                continue;
            yield return KeyValuePair.Create(str, pair.Value)!;
        }
    }

    internal IEnumerable<KeyValuePair<PyObject, PyObject>> EnumeratePairs()
    {
        foreach (var pair in _localsTable)
        {
            var value = LocalsPlusSpan[pair.Value];
            if (value is null)
                continue;

            if (value is PyCellObject cell)
            {
                value = cell.Value;
                if (value is null)
                    continue;
            }

            yield return KeyValuePair.Create<PyObject, PyObject>(PyStrObject.FromString(pair.Key), value);
        }

        if (_extraLocals is null)
            yield break;

        foreach (var pair in _extraLocals)
            yield return KeyValuePair.Create(pair.Key, pair.Value);
    }

    bool IPyVariablesLocalsDict.Remove(string key)
    {
        if (_localsTable.TryGetValue(key, out var index))
        {
            if (LocalsPlusSpan[index] is null)
                return false;

            LocalsPlusSpan[index] = null;
            return true;
        }

        return _extraLocals?.DelItem(key) ?? false;
    }

    bool IPyVariablesLocalsDict.TryGetValue(string key, [MaybeNullWhen(false)] out PyObject? value)
    {
        if (_localsTable.TryGetValue(key, out var index))
        {
            value = LocalsPlusSpan[index];
            return true;
        }

        if (_extraLocals is null)
        {
            value = null;
            return false;
        }

        return _extraLocals.TryGetValue(key, out value);
    }

    PyResult<PyStrObject> IPyObjectRecursiveRepr.RecursiveRepr(PyCallContext context, HashSet<PyObject> ids)
    {
        return PyUtils.DictionaryRecursiveRepr(context, this, EnumeratePairs().Select(static entry => KeyValuePair.Create(entry.Key, entry.Value)), "{", "}", ids);
    }
}

[PyType("FrameLocalsProxy")]
internal sealed partial class PyFrameLocalsProxyObjectType : PyTypeObject<PyFrameLocalsProxyObject>
{
    protected override PyResult Repr(PyCallContext context, PyFrameLocalsProxyObject self)
    {
        return IPyObjectRecursiveRepr.RecursiveRepr(context, self);
    }
}