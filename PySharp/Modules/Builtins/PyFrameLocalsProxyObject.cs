using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;
using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;

namespace PySharp.Modules.Builtins;

internal sealed class PyFrameLocalsProxyObject : PyObject, IPyVariablesLocalsDict, IPyObjectRecursiveRepr
{
    private readonly FrozenDictionary<string, int> _localsTable;
    private readonly Memory<PyObject?> _localsPlusMemory;
    private readonly IPyVariablesLocalsDict? _parentLocals;
    private Span<PyObject?> LocalsPlusSpan => _localsPlusMemory.Span;
    private PyDictObject? _extraLocals;

    internal PyDictObject? ExtraLocals => _extraLocals;

    public override PyTypeObject DefaultPyType => PyFrameLocalsProxyObjectType.Shared;

    // parentLocals backs name lookups (e.g. an inline comprehension frame
    // resolving its outermost iterable in the enclosing namespace) but is
    // never enumerated: the enumeration is this scope's own names, like
    // CPython's separate comprehension function sees as its locals
    internal PyFrameLocalsProxyObject(FrozenDictionary<string, int> localsTable, Memory<PyObject?> localsPlusMemory, IPyVariablesLocalsDict? parentLocals)
    {
        _localsPlusMemory = localsPlusMemory;
        _localsTable = localsTable;
        _parentLocals = parentLocals;
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

            if (_extraLocals is not null)
            {
                var ret = _extraLocals.GetItem(key);
                if (ret.IsSuccessful)
                    return ret.Value;
            }

            return _parentLocals is not null && _parentLocals.TryGetValue(key, out var parentValue) ? parentValue : null;
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

        if (_extraLocals?.ContainsKey(key) ?? false)
            return true;

        return _parentLocals?.ContainsKey(key) ?? false;
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

        if (_extraLocals is not null && _extraLocals.TryGetValue(key, out value))
            return true;

        if (_parentLocals is not null && _parentLocals.TryGetValue(key, out value))
            return true;

        value = null;
        return false;
    }

    PyResult<PyStrObject> IPyObjectRecursiveRepr.RecursiveRepr(PyCallContext context, HashSet<PyObject> ids)
    {
        return PyUtils.DictionaryRecursiveRepr(context, this, EnumeratePairs(), "{", "}", ids);
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