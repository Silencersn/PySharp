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
        private readonly FrozenDictionary<string, int> _localVariablesToIndex;
        internal readonly PyFrameGlobals? _globals;
        private IDictionary<string, PyObject?>? _locals;
        private DictAdapter? _localsAdapter;
        private PyDictObject? _pyDict;

        public PyFrameLocals(FrozenDictionary<string, int> localVariablesToIndex)
        {
            _localVariablesToIndex = localVariablesToIndex;
            _localsPlus = new PyObject[_localVariablesToIndex.Count];
        }
        public PyFrameLocals(PyFrameGlobals globals)
        {
            _globals = globals;
            _localsPlus = [];
            _localVariablesToIndex = FrozenDictionary<string, int>.Empty;
            _locals = globals.Globals!;
            _localsAdapter = globals.GlobalsAdapter;
            _pyDict = globals.PyDict;
        }
        private PyFrameLocals(FrozenDictionary<string, int> localVariablesToIndex, PyObject?[] localPlus)
        {
            _localVariablesToIndex = localVariablesToIndex;
            _localsPlus = localPlus;
        }

        internal PyObject?[] LocalsPlus => _localsPlus;
        public IDictionary<string, PyObject?> Locals => _locals ??= new LocalDictionary(_localsPlus, _localVariablesToIndex);
        public DictAdapter LocalsAdapter => _localsAdapter ??= new DictAdapter(Locals);
        public PyDictObject PyDict => _pyDict ??= PyDictObject.CreateProxy(LocalsAdapter);

        public PyFrameLocals Clone()
        {
            var newLocalPlus = new PyObject?[_localsPlus.Length];
            Array.Copy(_localsPlus, newLocalPlus, _localsPlus.Length);

            var clone = new PyFrameLocals(_localVariablesToIndex, newLocalPlus);

            if (_locals is LocalDictionary localDict)
            {
                var origExtra = localDict.GetExtraLocals();
                var cloneDict = new LocalDictionary(newLocalPlus, _localVariablesToIndex, origExtra);
                clone._locals = cloneDict;
            }
            else
            {
                throw new UnreachableException("Do not call Clone if it is created by globals");
            }
            return clone;
        }

        // TODO: AI Generated, need review
        private sealed class LocalDictionary : IDictionary<string, PyObject?>
        {
            private readonly PyObject?[] _localPlus;
            private readonly FrozenDictionary<string, int> _localVariablesToIndex;
            private readonly Dictionary<string, PyObject?> _extraLocals = [];

            public LocalDictionary(PyObject?[] localPlus, FrozenDictionary<string, int> localVariablesToIndex)
            {
                _localPlus = localPlus;
                _localVariablesToIndex = localVariablesToIndex;
            }
            internal LocalDictionary(PyObject?[] localPlus, FrozenDictionary<string, int> localVariablesToIndex, Dictionary<string, PyObject?> extraLocals)
            {
                _localPlus = localPlus;
                _localVariablesToIndex = localVariablesToIndex;
                _extraLocals = new(extraLocals);
            }

            public PyObject? this[string key]
            {
                get
                {
                    if (_localVariablesToIndex.TryGetValue(key, out var idx))
                        return _localPlus[idx];
                    if (_extraLocals.TryGetValue(key, out var val))
                        return val;
                    return null;
                }
                set
                {
                    if (_localVariablesToIndex.TryGetValue(key, out var idx))
                        _localPlus[idx] = value;
                    else
                        _extraLocals[key] = value;
                }
            }

            public ICollection<string> Keys => [.. _localVariablesToIndex.Keys, .. _extraLocals.Keys];

            public ICollection<PyObject?> Values => [.. _localVariablesToIndex.Keys.Select(k => _localPlus[_localVariablesToIndex[k]]), .. _extraLocals.Values];

            public int Count => _localVariablesToIndex.Count + _extraLocals.Count;

            public bool IsReadOnly => false;

            public void Add(string key, PyObject? value)
            {
                if (_localVariablesToIndex.TryGetValue(key, out var idx))
                {
                    if (_localPlus[idx] != null)
                        throw new ArgumentException($"Key '{key}' already exists.");
                    _localPlus[idx] = value;
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
                for (int i = 0; i < _localPlus.Length; i++)
                    _localPlus[i] = null;
                _extraLocals.Clear();
            }

            public bool Contains(KeyValuePair<string, PyObject?> item)
            {
                if (_localVariablesToIndex.TryGetValue(item.Key, out var idx))
                    return Equals(_localPlus[idx], item.Value);
                return _extraLocals.TryGetValue(item.Key, out var val) && Equals(val, item.Value);
            }

            public bool ContainsKey(string key)
            {
                if (_localVariablesToIndex.ContainsKey(key))
                    return true;
                return _extraLocals.ContainsKey(key);
            }

            public void CopyTo(KeyValuePair<string, PyObject?>[] array, int arrayIndex)
            {
                foreach (var k in _localVariablesToIndex.Keys)
                    array[arrayIndex++] = new KeyValuePair<string, PyObject?>(k, _localPlus[_localVariablesToIndex[k]]);
                foreach (var kv in _extraLocals)
                    array[arrayIndex++] = kv;
            }

            public IEnumerator<KeyValuePair<string, PyObject?>> GetEnumerator()
            {
                foreach (var k in _localVariablesToIndex.Keys)
                    yield return new KeyValuePair<string, PyObject?>(k, _localPlus[_localVariablesToIndex[k]]);
                foreach (var kv in _extraLocals)
                    yield return kv;
            }

            public bool Remove(string key)
            {
                if (_localVariablesToIndex.TryGetValue(key, out var idx))
                {
                    if (_localPlus[idx] != null)
                    {
                        _localPlus[idx] = null;
                        return true;
                    }
                    return false;
                }
                return _extraLocals.Remove(key);
            }

            public bool Remove(KeyValuePair<string, PyObject?> item)
            {
                if (_localVariablesToIndex.TryGetValue(item.Key, out var idx))
                {
                    if (Equals(_localPlus[idx], item.Value))
                    {
                        _localPlus[idx] = null;
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
                if (_localVariablesToIndex.TryGetValue(key, out var idx))
                {
                    value = _localPlus[idx];
                    return true;
                }
                if (_extraLocals.TryGetValue(key, out value))
                    return true;
                value = null;
                return false;
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

            internal Dictionary<string, PyObject?> GetExtraLocals() => new(_extraLocals);
        }
    }
}
