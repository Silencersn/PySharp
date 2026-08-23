using PySharp.Runtime;
using PySharp.Runtime.Comparison;
using PySharp.Runtime.PyAttributes;
using System;
using System.Collections;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace PySharp.Modules.Builtins;

// TODO: temp impl
// TODO: cell
internal sealed class PyFrameLocalsProxyObject : PyObject, IPyVariablesLocalsDict
{
    private readonly FrozenDictionary<string, int> _localsTable;
    private readonly Memory<PyObject?> _localsPlusMemory;
    private Span<PyObject?> LocalsPlusSpan => _localsPlusMemory.Span;
    private PyDictObject? _extraLocals;

    internal PyDictObject? ExtraLocals => _extraLocals;
    internal IPyVariablesLocalsDict? ExtraLocalsAsInterface => _extraLocals;

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

    public PyObject this[string key]
    {
        get
        {
            if (_localsTable.TryGetValue(key, out var index))
                return LocalsPlusSpan[index]!;

            if (_extraLocals is null)
                return null!;

            var ret = _extraLocals.GetItem(key);
            if (ret.IsSuccessful)
                return ret.Value;

            return null!;
        }
        set
        {
            if (_localsTable.TryGetValue(key, out var index))
            {
                LocalsPlusSpan[index] = value;
                return;
            }

            ArgumentNullException.ThrowIfNull(value);
            _extraLocals ??= new();
            _extraLocals[key] = value;
        }
    }

    public int Count => _localsTable.Count + _extraLocals?.Count ?? 0;

    public void Add(string key, PyObject? value)
    {
        if (_localsTable.TryGetValue(key, out var index))
        {
            if (LocalsPlusSpan[index] is not null)
                throw new ArgumentException($"An item with the same key has already been added. Key: {key}");
            LocalsPlusSpan[index] = value;
        }
        else
        {
            ArgumentNullException.ThrowIfNull(value);
            _extraLocals ??= new();
            // TODO
            _extraLocals.SetItem(key, value);
        }
    }

    public void Clear()
    {
        LocalsPlusSpan.Clear();
        _extraLocals?.Clear();
    }

    public bool ContainsKey(string key)
    {
        if (_localsTable.ContainsKey(key))
            return true;

        return _extraLocals?.ContainsKey(key) ?? false;
    }

    public IEnumerator<KeyValuePair<string, PyObject?>> GetEnumerator()
    {
        foreach (var pair in _localsTable)
            yield return KeyValuePair.Create(pair.Key, LocalsPlusSpan[pair.Value]);

        if (_extraLocals is null)
            yield break;

        foreach (var pair in _extraLocals)
            yield return pair!;
    }

    public bool Remove(string key)
    {
        if (_localsTable.TryGetValue(key, out var index))
        {
            if (LocalsPlusSpan[index] is null)
                return false;

            LocalsPlusSpan[index] = null;
            return true;
        }

        return ExtraLocalsAsInterface?.Remove(key) ?? false;
    }

    public bool TryGetValue(string key, [MaybeNullWhen(false)] out PyObject? value)
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
}

[PyType("FrameLocalsProxy")]
internal sealed partial class PyFrameLocalsProxyObjectType : PyTypeObject<PyFrameLocalsProxyObject>;