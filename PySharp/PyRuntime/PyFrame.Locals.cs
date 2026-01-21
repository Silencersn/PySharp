using PySharp.PyModules.Builtins;
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
        internal readonly PyFrameGlobals? _globals;
        private IDictionary<string, PyObject?>? _locals;
        private DictAdapter? _localsAdapter;
        private PyDictObject? _pyDict;

        public PyFrameLocals(FrozenDictionary<string, int> localsTable)
        {
            _localsTable = localsTable;
            _localsPlus = new PyObject[_localsTable.Count];
        }
        public PyFrameLocals(PyFrameGlobals globals)
        {
            _globals = globals;
            _localsPlus = [];
            _localsTable = FrozenDictionary<string, int>.Empty;
            _locals = globals.Globals!;
            _localsAdapter = globals.GlobalsAdapter;
            _pyDict = globals.PyDict;
        }
        private PyFrameLocals(FrozenDictionary<string, int> localsTable, PyObject?[] localPlus)
        {
            _localsTable = localsTable;
            _localsPlus = localPlus;
        }

        internal PyObject?[] LocalsPlus => _localsPlus;
        public IDictionary<string, PyObject?> Locals => _locals ??= new LocalDictionary(this);
        public DictAdapter LocalsAdapter => _localsAdapter ??= new DictAdapter(Locals);
        public PyDictObject PyDict => _pyDict ??= PyDictObject.CreateProxy(LocalsAdapter);

        public PyFrameLocals Clone()
        {
            var clone = new PyFrameLocals(_localsTable, [.. _localsPlus]);

            if (_locals is LocalDictionary localDict)
            {
                clone._locals = new LocalDictionary(clone, localDict._extraLocals.ToDictionary());
                if (_localsAdapter is not null)
                    clone._localsAdapter = new DictAdapter(clone._locals, _localsAdapter._extraDict.ToDictionary());
            }
            else if (_locals is not null)
            {
                throw new UnreachableException("Do not call Clone if it is created by globals");
            }
            return clone;
        }

        private sealed class LocalDictionary : IDictionary<string, PyObject?>
        {
            private readonly PyFrameLocals _locals;
            private PyObject?[] LocalPlus => _locals._localsPlus;
            private FrozenDictionary<string, int> LocalsTable => _locals._localsTable;
            internal readonly Dictionary<string, PyObject?> _extraLocals;

            public LocalDictionary(PyFrameLocals locals) : this(locals, [])
            {
            }
            internal LocalDictionary(PyFrameLocals locals, Dictionary<string, PyObject?> extraLocals)
            {
                _locals = locals;
                _extraLocals = extraLocals;
            }

            public PyObject? this[string key]
            {
                get
                {
                    if (LocalsTable.TryGetValue(key, out var idx))
                        return LocalPlus[idx];
                    if (_extraLocals.TryGetValue(key, out var val))
                        return val;
                    return null;
                }
                set
                {
                    if (LocalsTable.TryGetValue(key, out var idx))
                        LocalPlus[idx] = value;
                    else
                        _extraLocals[key] = value;
                }
            }

            public ICollection<string> Keys => [.. LocalsTable.Keys, .. _extraLocals.Keys];

            public ICollection<PyObject?> Values => [.. LocalsTable.Keys.Select(k => LocalPlus[LocalsTable[k]]), .. _extraLocals.Values];

            public int Count => LocalsTable.Count + _extraLocals.Count;

            public bool IsReadOnly => false;

            public void Add(string key, PyObject? value)
            {
                if (LocalsTable.TryGetValue(key, out var idx))
                {
                    if (LocalPlus[idx] is not null)
                        throw new ArgumentException($"Key '{key}' already exists.");
                    LocalPlus[idx] = value;
                }
                else
                {
                    if (_extraLocals.ContainsKey(key))
                        throw new ArgumentException($"Key '{key}' already exists.");
                    _extraLocals.Add(key, value);
                }
            }

            public void Add(KeyValuePair<string, PyObject?> item) => Add(item.Key, item.Value);

            public void Clear()
            {
                for (int i = 0; i < LocalPlus.Length; i++)
                    LocalPlus[i] = null;
                _extraLocals.Clear();
            }

            public bool Contains(KeyValuePair<string, PyObject?> item)
            {
                if (LocalsTable.TryGetValue(item.Key, out var idx))
                    return Equals(LocalPlus[idx], item.Value);
                return _extraLocals.TryGetValue(item.Key, out var val) && Equals(val, item.Value);
            }

            public bool ContainsKey(string key)
            {
                if (LocalsTable.ContainsKey(key))
                    return true;
                return _extraLocals.ContainsKey(key);
            }

            public void CopyTo(KeyValuePair<string, PyObject?>[] array, int arrayIndex)
            {
                foreach (var k in LocalsTable.Keys)
                    array[arrayIndex++] = new KeyValuePair<string, PyObject?>(k, LocalPlus[LocalsTable[k]]);
                foreach (var kv in _extraLocals)
                    array[arrayIndex++] = kv;
            }

            public IEnumerator<KeyValuePair<string, PyObject?>> GetEnumerator()
            {
                foreach (var k in LocalsTable.Keys)
                    yield return new KeyValuePair<string, PyObject?>(k, LocalPlus[LocalsTable[k]]);
                foreach (var kv in _extraLocals)
                    yield return kv;
            }

            public bool Remove(string key)
            {
                if (LocalsTable.TryGetValue(key, out var idx))
                {
                    if (LocalPlus[idx] is not null)
                    {
                        LocalPlus[idx] = null;
                        return true;
                    }
                    return false;
                }
                return _extraLocals.Remove(key);
            }

            public bool Remove(KeyValuePair<string, PyObject?> item)
            {
                if (LocalsTable.TryGetValue(item.Key, out var idx))
                {
                    if (Equals(LocalPlus[idx], item.Value))
                    {
                        LocalPlus[idx] = null;
                        return true;
                    }
                    return false;
                }
                if (_extraLocals.TryGetValue(item.Key, out var val) && Equals(val, item.Value))
                    return _extraLocals.Remove(item.Key);
                return false;
            }

            public bool TryGetValue(string key, [MaybeNullWhen(false)] out PyObject? value)
            {
                if (LocalsTable.TryGetValue(key, out var idx))
                {
                    value = LocalPlus[idx];
                    return true;
                }
                if (_extraLocals.TryGetValue(key, out value))
                    return true;
                value = null;
                return false;
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}
