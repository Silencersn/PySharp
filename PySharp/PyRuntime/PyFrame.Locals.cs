using PySharp.PyModules.Builtins;
using PySharp.PyRuntime.Comparison;
using PySharp.Utility;
using System.Collections;
using System.Collections.Frozen;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace PySharp.PyRuntime;

partial class PyFrame
{
    internal sealed class PyFrameLocals
    {
        private readonly PyObject?[] _localsPlus;
        private readonly FrozenDictionary<string, int> _localsTable;
        private LocalDictionary? _locals;
        private DictAdapter? _localsAdapter;
        private PyDictObject? _pyDict;

        public PyFrameLocals(FrozenDictionary<string, int> localsTable)
        {
            _localsTable = localsTable;
            _localsPlus = new PyObject[_localsTable.Count];
        }
        private PyFrameLocals(FrozenDictionary<string, int> localsTable, PyObject?[] localPlus)
        {
            _localsTable = localsTable;
            _localsPlus = localPlus;
        }

        internal PyObject?[] LocalsPlus => _localsPlus;
        public LocalDictionary Locals => _locals ??= new LocalDictionary(_localsTable, _localsPlus);
        public DictAdapter LocalsAdapter => _localsAdapter ??= new DictAdapter(Locals);
        public PyDictObject PyDict => _pyDict ??= PyDictObject.CreateProxy(LocalsAdapter);

        public PyFrameLocals Clone()
        {
            var clone = new PyFrameLocals(_localsTable, [.. _localsPlus]);

            if (_locals is null)
                return clone;

            clone._locals = new LocalDictionary(_localsTable, clone._localsPlus, _locals.ExtraLocals?.ToDictionary());
            if (_localsAdapter is not null)
                clone._localsAdapter = new DictAdapter(clone._locals, _localsAdapter._extraDict.ToDictionary());
            return clone;
        }

        internal sealed class LocalDictionary : IDictionary<string, PyObject?>
        {
            private readonly FrozenDictionary<string, int> _localsTable;
            private readonly PyObject?[] _localsPlus;
            private Dictionary<string, PyObject?>? _extraLocals;

            internal Dictionary<string, PyObject?>? ExtraLocals => _extraLocals;

            internal LocalDictionary(FrozenDictionary<string, int> localsTable, PyObject?[] localsPlus, Dictionary<string, PyObject?>? extraLocals)
            {
                _localsPlus = localsPlus;
                _localsTable = localsTable;
                _extraLocals = extraLocals;
            }
            internal LocalDictionary(FrozenDictionary<string, int> localsTable, PyObject?[] localsPlus) : this(localsTable, localsPlus, null)
            {
            }

            public PyObject? this[string key]
            {
                get
                {
                    if (_localsTable.TryGetValue(key, out var index))
                        return _localsPlus[index];

                    return _extraLocals?.GetValueOrDefault(key);
                }
                set
                {
                    if (_localsTable.TryGetValue(key, out var index))
                    {
                        _localsPlus[index] = value;
                        return;
                    }
                    _extraLocals ??= [];
                    _extraLocals[key] = value;
                }
            }

            public ICollection<string> Keys => _extraLocals is null ? _localsTable.Keys : [.. _localsTable.Keys, .. _extraLocals.Keys];

            public ICollection<PyObject?> Values => _extraLocals is null ? [.._localsPlus] : [.. _localsPlus, .. _extraLocals.Values];

            public int Count => _localsTable.Count + _extraLocals?.Count ?? 0;

            bool ICollection<KeyValuePair<string, PyObject?>>.IsReadOnly => false;

            public void Add(string key, PyObject? value)
            {
                if (_localsTable.TryGetValue(key, out var index))
                {
                    if (_localsPlus[index] is not null)
                        throw new ArgumentException($"An item with the same key has already been added. Key: {key}");
                    _localsPlus[index] = value;
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
                _localsPlus.AsSpan().Clear();
                _extraLocals?.Clear();
            }

            bool ICollection<KeyValuePair<string, PyObject?>>.Contains(KeyValuePair<string, PyObject?> item)
            {
                if (_localsTable.TryGetValue(item.Key, out var index))
                    return PyObjectComparer.Default.Equals(_localsPlus[index], item.Value);

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
                    array[arrayIndex++] = KeyValuePair.Create(pair.Key, _localsPlus[pair.Value]);

                if (_extraLocals is null)
                    return;

                foreach (var pair in _extraLocals)
                    array[arrayIndex++] = pair;
            }

            public IEnumerator<KeyValuePair<string, PyObject?>> GetEnumerator()
            {
                foreach (var pair in _localsTable)
                    yield return KeyValuePair.Create(pair.Key, _localsPlus[pair.Value]);

                if (_extraLocals is null)
                    yield break;

                foreach (var pair in _extraLocals)
                    yield return pair;
            }

            public bool Remove(string key)
            {
                if (_localsTable.TryGetValue(key, out var index))
                {
                    if (_localsPlus[index] is null)
                        return false;

                    _localsPlus[index] = null;
                    return true;
                }

                return _extraLocals?.Remove(key) ?? false;
            }

            bool ICollection<KeyValuePair<string, PyObject?>>.Remove(KeyValuePair<string, PyObject?> item)
            {
                if (_localsTable.TryGetValue(item.Key, out var index))
                {
                    if (_localsPlus[index] is null)
                        return item.Value is null;

                    if (!PyObjectComparer.Default.Equals(_localsPlus[index], item.Value))
                        return false;

                    _localsPlus[index] = null;
                    return true;
                }

                return (_extraLocals as ICollection<KeyValuePair<string, PyObject?>>)?.Remove(item) ?? false;
            }

            public bool TryGetValue(string key, [MaybeNullWhen(false)] out PyObject? value)
            {
                if (_localsTable.TryGetValue(key, out var index))
                {
                    value = _localsPlus[index];
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
}
