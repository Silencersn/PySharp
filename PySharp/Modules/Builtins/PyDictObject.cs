using PySharp.Compilation.CodeAnalysis;
using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.Comparison;
using PySharp.Runtime.PyAttributes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace PySharp.Modules.Builtins;

public partial class PyDictObject : PyObject, IPyObjectRecursiveRepr
{
    private int _count;
    private int[] _buckets;
    private Entry[] _entries;

    public PyDictObject()
    {
        _count = 0;
        _buckets = [];
        _entries = [];
    }
    public PyDictObject(PyDictObject dict)
    {
        _count = dict.Count;
        _buckets = [..dict._buckets];
        _entries = [.. dict._entries];
    }

    public override PyTypeObject DefaultPyType => PyDictObjectType.Shared;

    internal ReadOnlySpan<Entry> Entries => _entries.AsSpan()[.._count];

    public int Count => _count;

    internal PyObject this[string key]{
        get => TryGetValue(key, out var value) ? value : throw new KeyNotFoundException();
        set => SetItem(key, value);
    }

    public static PyResult<PyDictObject> CreateDict(PyCallContext context, params IEnumerable<KeyValuePair<PyObject, PyObject>> pairs)
    {
        var dict = new PyDictObject();
        foreach (var pair in pairs)
        {
            var result = dict.SetItem(context, pair.Key, pair.Value);
            if (result.IsError)
                return result.ExceptionResult;
        }
        return dict;
    }
    public static PyDictObject CreateDict()
    {
        return new PyDictObject();
    }
    public static PyDictObject CreateDict(IDictionary<string, PyObject> dict)
    {
        var newDict = new PyDictObject();
        foreach (var pair in dict)
            newDict[pair.Key] = pair.Value;
        return newDict;
    }
    public static PyDictObject FromStringKeyDict(IDictionary<string, PyObject> dict)
    {
        if (dict is PyDictObject dictObj)
            return dictObj;

        // TODO: it should be a proxy
        var newDict = new PyDictObject();
        foreach (var pair in dict)
            newDict[pair.Key] = pair.Value;
        return newDict;
    }

    public PyResult GetItem(string key)
    {
        if (TryGetValue(key, out var value))
            return value;
        return PyResult.KeyError(PyStrObject.FromString(key));
    }

    public bool TryGetValue(string key, [NotNullWhen(true)] out PyObject? value)
    {
        value = null;
        if (_buckets.Length is 0)
            return false;

        var hashCode = (uint)PyStrObject.GetHashCode(key);
        var index = GetBucket(hashCode);
        if (index is 0)
            return false;

        ref var entry = ref _entries[index - 1];
        while (true)
        {
            if (entry.HashCode == hashCode && 
                entry.Key is PyStrObject { Value: var entryKey } &&
                string.Equals(entryKey, key, StringComparison.Ordinal))
            {
                value = entry.Value;
                return true;
            }

            if (entry.Next is -1)
                return false;

            entry = ref _entries[entry.Next];
        }
    }

    public bool ContainsKey(string key)
    {
        return TryGetValue(key, out _);
    }

    public PyResult GetItem(PyCallContext context, PyObject key)
    {
        if (_count is 0)
            return PyResult.KeyError(key);

        var hash = PySpecialMethods.Hash(context, key);
        if (hash.IsError)
            return hash;

        var hashCode = (uint)hash.Value.Int32Value;
        var index = GetBucket(hashCode);
        if (index is 0)
            return PyResult.KeyError(key);

        ref var entry = ref _entries[index - 1];
        while (true)
        {
            if (entry.HashCode == hashCode)
            {
                var eq = PyComparer.Eq(context, entry.Key, key);
                if (eq.IsError)
                    return eq;

                if (eq.Value.BoolValue)
                    return entry.Value;
            }

            if (entry.Next is -1)
                return PyResult.KeyError(key);

            entry = ref _entries[entry.Next];
        }
    }

    public void SetItem(string key, PyObject value)
    {
        if (_count == _entries.Length)
            EnsureCapacity(_count + 1);

        var hashCode = (uint)PyStrObject.GetHashCode(key);
        var index = GetBucket(hashCode);

        if (index is not 0)
        {
            ref var entry = ref _entries[index - 1];
            while (true)
            {
                if (entry.HashCode == hashCode &&
                    entry.Key is PyStrObject { Value: var entryKey } &&
                    string.Equals(entryKey, key, StringComparison.Ordinal))
                {
                    entry.Value = value;
                    return;
                }

                if (entry.Next is -1)
                    break;

                entry = ref _entries[entry.Next];
            }
        }

        {
            ref var entry = ref _entries[_count];
            entry.HashCode = hashCode;
            entry.Key = PyStrObject.FromString(key);
            entry.Value = value;
            PushEntryIntoBucket(ref entry, _count);
            _count++;
            return;
        }
    }

    internal PyResult InternalSetItem(PyCallContext context, PyObject key, PyObject? value)
    {
        var hash = PySpecialMethods.Hash(context, key);
        if (hash.IsError)
            return hash;

        if (_count == _entries.Length)
            EnsureCapacity(_count + 1);

        var hashCode = (uint)hash.Value.Int32Value;
        var index = GetBucket(hashCode) - 1;

        if (index is not -1)
        {
            while (true)
            {
                ref var entry = ref _entries[index];

                if (entry.HashCode == hashCode)
                {
                    var eq = PyComparer.Eq(context, entry.Key, key);
                    if (eq.IsError)
                        return eq;

                    if (eq.Value.BoolValue)
                    {
                        // set
                        if (value is not null)
                        {
                            entry.Value = value;
                            return PyNoneObject.None;
                        }

                        // del
                        value = entry.Value;

                        for (int i = index + 1; i < _count; i++)
                            _entries[i - 1] = _entries[i];

                        _count--;

                        // TODO: perf
                        _buckets.AsSpan().Clear();
                        for (int i = 0; i < _count; i++)
                            PushEntryIntoBucket(ref _entries[i], i);
                        return value;
                    }

                }

                if (entry.Next is -1)
                    break;

                index = entry.Next;
            }
        }

        if (value is null)
            return PyResult.KeyError(key);

        {
            ref var entry = ref _entries[_count];
            entry.HashCode = hashCode;
            entry.Key = key;
            entry.Value = value;
            PushEntryIntoBucket(ref entry, _count);
            _count++;
            return PyNoneObject.None;
        }
    }

    public PyResult SetItem(PyCallContext context, PyObject key, PyObject value)
    {
        return InternalSetItem(context, key, value);
    }

    public PyResult DelItem(PyCallContext context, PyObject key)
    {
        var result = InternalSetItem(context, key, value: null);
        if (result.IsError)
            return result;
        return PyNoneObject.None;
    }

    internal PyResult Pop(PyCallContext context, PyObject key)
    {
        return InternalSetItem(context, key, value: null);
    }

    internal PyResult PopItem()
    {
        if (_count is 0)
            return PyResult.KeyError(PySR.Runtime_Dictionary_PopEmptyDict);

        ref var entry = ref _entries[--_count];
        var tuple = PyTupleObject.CreateTuple(entry.Key, entry.Value);
        entry = default;

        // TODO: perf
        _buckets.AsSpan().Clear();
        for (int i = 0; i < _count; i++)
            PushEntryIntoBucket(ref _entries[i], i);

        return tuple;
    }

    internal PyResult Update(PyCallContext context, PyObject iterableOrMapping)
    {
        // TODO: perf
        var dict = PyUtils.ToDict(context, iterableOrMapping);
        if (dict.IsError)
            return dict;

        var entries = dict.Value.Entries;
        for (int i = 0; i < entries.Length; i++)
        {
            var entry = entries[i];
            var result = SetItem(context, entry.Key, entry.Value);
            if (result.IsError)
                return result;
        }

        return PyNoneObject.None;
    }

    public void Clear()
    {
        Array.Clear(_entries);
        Array.Clear(_buckets);
        _count = 0;
    }

    [AIGenerated]
    public static PyResult PyFromKeys(PyCallContext context, PyTypeObject cls, PyObject iterable, PyObject? value = null)
    {
        var result = cls.Call(context);
        if (result.IsError)
            return result;

        if (result.Value is not PyDictObject dict)
            return PyResult.TypeError($"'{cls.FullName}' is not a dict type");

        var val = value ?? PyNoneObject.None;
        var iterResult = PySpecialMethods.Iter(context, iterable);
        if (iterResult.IsError)
            return iterResult;

        var iterator = iterResult.Value;
        while (true)
        {
            var next = PySpecialMethods.Next(context, iterator);
            if (next.IsError)
            {
                if (next.IsStopIteration)
                    break;
                return next;
            }
            var setResult = dict.SetItem(context, next.Value, val);
            if (setResult.IsError)
                return setResult;
        }

        return dict;
    }

    private void EnsureCapacity(int capacity)
    {
        Resize(Helper.GetPrime(capacity));
    }

    private void Resize(int newSize)
    {
        Debug.Assert(newSize >= _count);

        var newBuckets = new int[newSize];
        var newEntries = new Entry[newSize];

        _entries.AsSpan()[.._count].CopyTo(newEntries);

        _buckets = newBuckets;

        for (int i = 0; i < _count; i++)
            PushEntryIntoBucket(ref newEntries[i], i);

        _entries = newEntries;
    }

    private void PushEntryIntoBucket(ref Entry entry, int entryIndex)
    {
        ref var bucket = ref GetBucket(entry.HashCode);
        entry.Next = bucket - 1;
        bucket = entryIndex + 1;
    }

    private ref int GetBucket(uint hashCode)
    {
        int[] buckets = _buckets;
        Debug.Assert(buckets.Length > 0);
        return ref buckets[hashCode % buckets.Length];
    }

    PyResult<PyStrObject> IPyObjectRecursiveRepr.RecursiveRepr(PyCallContext context, HashSet<PyObject> ids)
    {
        // TODO: perf
        return PyUtils.DictionaryRecursiveRepr(context, this, Entries.ToArray().Select(static entry => KeyValuePair.Create(entry.Key, entry.Value)), "{", "}", ids);
    }

    private static class Helper
    {
        private const int HashPrime = 101;

        private static ReadOnlySpan<int> Primes =>
            [
                3, 7, 11, 17, 23, 29, 37, 47, 59, 71, 89, 107, 131, 163, 197, 239, 293, 353, 431, 521, 631, 761, 919,
            1103, 1327, 1597, 1931, 2333, 2801, 3371, 4049, 4861, 5839, 7013, 8419, 10103, 12143, 14591,
            17519, 21023, 25229, 30293, 36353, 43627, 52361, 62851, 75431, 90523, 108631, 130363, 156437,
            187751, 225307, 270371, 324449, 389357, 467237, 560689, 672827, 807403, 968897, 1162687, 1395263,
            1674319, 2009191, 2411033, 2893249, 3471899, 4166287, 4999559, 5999471, 7199369
            ];

        private static bool IsPrime(int candidate)
        {
            if ((candidate & 1) is 0)
                return candidate is 2;

            int limit = (int)Math.Sqrt(candidate);
            for (int divisor = 3; divisor <= limit; divisor += 2)
            {
                if ((candidate % divisor) is 0)
                    return false;
            }

            return true;
        }

        public static int GetPrime(int min)
        {
            foreach (int prime in Primes)
            {
                if (prime >= min)
                    return prime;
            }

            // Outside of our predefined table. Compute the hard way.
            for (int i = (min | 1); i < int.MaxValue; i += 2)
            {
                if (IsPrime(i) && ((i - 1) % HashPrime is not 0))
                    return i;
            }
            return min;
        }
    }

    internal struct Entry
    {
        public int Next;

        public uint HashCode;

        public PyObject Key;

        public PyObject Value;
    }
}