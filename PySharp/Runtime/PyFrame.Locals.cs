using PySharp.Modules.Builtins;
using PySharp.Runtime.Comparison;
using PySharp.Utility;
using System.Collections;
using System.Collections.Frozen;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace PySharp.Runtime;

partial class PyFrame
{
    internal sealed class PyFrameLocals
    {
        private readonly PyObject?[] _localsPlus;
        internal readonly FrozenDictionary<string, int> _localsTable;
        internal readonly int _cellCount;
        private IDictionary<string, PyObject?>? _locals;
        private PyDictObject? _pyDict;

        internal PyFrameLocals(FrozenDictionary<string, int> localsTable, int cellCount)
        {
            _localsTable = localsTable;
            _cellCount = cellCount;
            _localsPlus = new PyObject[_localsTable.Count];
        }
        private PyFrameLocals(FrozenDictionary<string, int> localsTable, int cellCount, PyObject?[] localPlus)
        {
            _localsTable = localsTable;
            _cellCount = cellCount;
            _localsPlus = localPlus;
        }
        private PyFrameLocals(IDictionary<string, PyObject?> locals)
        {
            _localsTable = FrozenDictionary<string, int>.Empty;
            _localsPlus = [];
            _locals = locals;
        }
        internal PyFrameLocals(PyDictObject dict)
        {
            _localsTable = FrozenDictionary<string, int>.Empty;
            _localsPlus = [];
            _pyDict = dict;
            _locals = new StringKeyDict(_pyDict)!;
        }

        internal PyObject?[] LocalsPlus => _localsPlus;
        public IDictionary<string, PyObject?> Locals => _locals ??= new LocalDictionary(_localsTable, _localsPlus);
        public PyDictObject PyDict => _pyDict ??= PyDictObject.CreateProxy(new DictAdapter(Locals));

        public PyFrameLocals Clone()
        {
            // only clone string keys, PyObject keys will be ignored

            var clone = new PyFrameLocals(_localsTable, _cellCount, [.. _localsPlus]);
            if (_locals is null)
                return clone;

            var extraDict = _locals is LocalDictionary localDict ? localDict.ExtraLocals : _locals;
            clone._locals = new LocalDictionary(_localsTable, clone._localsPlus, extraDict is null ? null : new(extraDict));
            return clone;
        }

        public PyFrameLocals ToClassClosure(PyCodeObject code)
        {
            PyCellObject[] freeVars = [..code.FreeVars.Select(name =>
            {
                var obj = Locals[name];
                Debug.Assert(obj is PyCellObject);
                return (PyCellObject)obj;
            })];

            var skipCount = _localsPlus.Length - _cellCount;

            return new PyFrameLocals(code.LocalsTable, freeVars.Length, freeVars);
        }

        internal void InitCells(ReadOnlySpan<PyCellObject> closure)
        {
            if (closure.Length is 0)
                return;

            closure.CopyTo(UnsafeUtils.CastSpan<PyObject?, PyCellObject>(_localsPlus.AsSpan()[^closure.Length..]));
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

            public ICollection<PyObject?> Values => _extraLocals is null ? [.. _localsPlus] : [.. _localsPlus, .. _extraLocals.Values];

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
