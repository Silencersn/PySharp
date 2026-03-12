using PySharp.Modules.Builtins;
using PySharp.Runtime.Comparison;
using System;
using System.Collections;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace PySharp.Runtime;

partial class PyVariables
{
    internal sealed class LocalDictionary : IDictionary<string, PyObject?>
    {
        private readonly FrozenDictionary<string, int> _localsTable;
        private readonly Memory<PyObject?> _localsPlusMemory;
        private Span<PyObject?> LocalsPlusSpan => _localsPlusMemory.Span;
        private Dictionary<string, PyObject?>? _extraLocals;

        internal Dictionary<string, PyObject?>? ExtraLocals => _extraLocals;

        internal LocalDictionary(FrozenDictionary<string, int> localsTable, Memory<PyObject?> localsPlusMemory, Dictionary<string, PyObject?>? extraLocals)
        {
            _localsPlusMemory = localsPlusMemory;
            _localsTable = localsTable;
            _extraLocals = extraLocals;
        }
        internal LocalDictionary(FrozenDictionary<string, int> localsTable, Memory<PyObject?> localsPlusMemory) : this(localsTable, localsPlusMemory, null)
        {
        }

        public PyObject? this[string key]
        {
            get
            {
                if (_localsTable.TryGetValue(key, out var index))
                    return LocalsPlusSpan[index];

                return _extraLocals?.GetValueOrDefault(key);
            }
            set
            {
                if (_localsTable.TryGetValue(key, out var index))
                {
                    LocalsPlusSpan[index] = value;
                    return;
                }
                _extraLocals ??= [];
                _extraLocals[key] = value;
            }
        }

        public ICollection<string> Keys => _extraLocals is null ? _localsTable.Keys : [.. _localsTable.Keys, .. _extraLocals.Keys];

        public ICollection<PyObject?> Values => _extraLocals is null ? [.. LocalsPlusSpan] : [.. LocalsPlusSpan, .. _extraLocals.Values];

        public int Count => _localsTable.Count + _extraLocals?.Count ?? 0;

        bool ICollection<KeyValuePair<string, PyObject?>>.IsReadOnly => false;

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
                _extraLocals ??= [];
                _extraLocals.Add(key, value);
            }
        }

        void ICollection<KeyValuePair<string, PyObject?>>.Add(KeyValuePair<string, PyObject?> item)
        {
            Add(item.Key, item.Value);
        }

        public void Clear()
        {
            LocalsPlusSpan.Clear();
            _extraLocals?.Clear();
        }

        bool ICollection<KeyValuePair<string, PyObject?>>.Contains(KeyValuePair<string, PyObject?> item)
        {
            if (_localsTable.TryGetValue(item.Key, out var index))
                return PyObjectComparer.Default.Equals(LocalsPlusSpan[index], item.Value);

            if (_extraLocals is null)
                return false;

            return _extraLocals.TryGetValue(item.Key, out var value) && PyObjectComparer.Default.Equals(value, item.Value);
        }

        public bool ContainsKey(string key)
        {
            if (_localsTable.ContainsKey(key))
                return true;

            return _extraLocals?.ContainsKey(key) ?? false;
        }

        void ICollection<KeyValuePair<string, PyObject?>>.CopyTo(KeyValuePair<string, PyObject?>[] array, int arrayIndex)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(arrayIndex + Count, array.Length);

            foreach (var pair in _localsTable)
                array[arrayIndex++] = KeyValuePair.Create(pair.Key, LocalsPlusSpan[pair.Value]);

            if (_extraLocals is null)
                return;

            foreach (var pair in _extraLocals)
                array[arrayIndex++] = pair;
        }

        public IEnumerator<KeyValuePair<string, PyObject?>> GetEnumerator()
        {
            foreach (var pair in _localsTable)
                yield return KeyValuePair.Create(pair.Key, LocalsPlusSpan[pair.Value]);

            if (_extraLocals is null)
                yield break;

            foreach (var pair in _extraLocals)
                yield return pair;
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

            return _extraLocals?.Remove(key) ?? false;
        }

        bool ICollection<KeyValuePair<string, PyObject?>>.Remove(KeyValuePair<string, PyObject?> item)
        {
            if (_localsTable.TryGetValue(item.Key, out var index))
            {
                if (LocalsPlusSpan[index] is null)
                    return item.Value is null;

                if (!PyObjectComparer.Default.Equals(LocalsPlusSpan[index], item.Value))
                    return false;

                LocalsPlusSpan[index] = null;
                return true;
            }

            return (_extraLocals as ICollection<KeyValuePair<string, PyObject?>>)?.Remove(item) ?? false;
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

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
