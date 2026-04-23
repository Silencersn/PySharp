using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.Comparison;
using PySharp.Runtime.PyAttributes;
using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace PySharp.Modules.Builtins;

public partial class PyDictObject : PyObject, IPyObjectRecursiveRepr, IDictionary<PyObject, PyObject>
{
    private readonly IDictionary<PyObject, PyObject> _dict;

    public override PyTypeObject DefaultPyType => PyDictObjectType.Shared;

    public ICollection<PyObject> Keys => _dict.Keys;

    public ICollection<PyObject> Values => _dict.Values;

    public int Count => _dict.Count;

    public bool IsReadOnly => _dict.IsReadOnly;

    public PyObject this[PyObject key]
    {
        get => _dict[key];
        set => _dict[key] = value;
    }

    public PyDictObject()
    {
        _dict = new OrderedDictionary<PyObject, PyObject>(PyObjectComparer.Default);
    }
    public PyDictObject(IEnumerable<KeyValuePair<PyObject, PyObject>> dict) : this()
    {
        PyUpdate(dict);
    }
    private PyDictObject(IDictionary<PyObject, PyObject> dict, bool isProxy)
    {
        if (isProxy)
            _dict = dict;
        else
            _dict = new OrderedDictionary<PyObject, PyObject>(dict, PyObjectComparer.Default);
    }

    public static PyDictObject CreateDict(params IEnumerable<KeyValuePair<PyObject, PyObject>> dict)
    {
        return new PyDictObject(dict);
    }
    public static PyDictObject CreateProxy(IDictionary<PyObject, PyObject> dict)
    {
        return new PyDictObject(dict, true);
    }

    PyResult<PyStrObject> IPyObjectRecursiveRepr.RecursiveRepr(PyCallContext context, HashSet<int> ids)
    {
        return Utils.DictionaryRecursiveRepr(context, this, _dict, "{", "}", ids);
    }

    public IEnumerator<KeyValuePair<PyObject, PyObject>> GetEnumerator()
    {
        return _dict.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public void Add(PyObject key, PyObject value)
    {
        _dict.Add(key, value);
    }

    public bool ContainsKey(PyObject key)
    {
        return _dict.ContainsKey(key);
    }

    public bool Remove(PyObject key)
    {
        return _dict.Remove(key);
    }

    public bool TryGetValue(PyObject key, [NotNullWhen(true)] out PyObject? value)
    {
        return _dict.TryGetValue(key, out value);
    }

    public void Add(KeyValuePair<PyObject, PyObject> item)
    {
        _dict.Add(item);
    }

    public void Clear()
    {
        _dict.Clear();
    }

    public bool Contains(KeyValuePair<PyObject, PyObject> item)
    {
        return _dict.Contains(item);
    }

    public void CopyTo(KeyValuePair<PyObject, PyObject>[] array, int arrayIndex)
    {
        _dict.CopyTo(array, arrayIndex);
    }

    public bool Remove(KeyValuePair<PyObject, PyObject> item)
    {
        return _dict.Remove(item);
    }
}

[PyType("dict")]
public sealed partial class PyDictObjectType : PyTypeObject<PyDictObject>
{
    private static readonly PyBuiltinFunctionOrMethodObject _new = PyBuiltinFunctionOrMethodObject.CreateFunction(PySpecialNames.New, NewImpl_1, NewImpl_2);

    [PyFunctionParameters("**kwargs")]
    private static PyResult NewImpl_1(PyCallContext context, PyArguments arguments)
    {
        return PyDictObject.CreateDict(arguments.ExtraKwargs
            .Select(pair => KeyValuePair.Create<PyObject, PyObject>(PyStrObject.FromString(pair.Key), pair.Value)));
    }

    [PyFunctionParameters("iterable_or_mapping", "/", "**kwargs")]
    private static PyResult NewImpl_2(PyCallContext context, PyArguments arguments)
    {
        var dict = PyUtils.ToDict(context, arguments[0]);
        if (dict.IsError)
            return dict;

        foreach (var kwarg in arguments.ExtraKwargs)
            dict.Value.PySetItem(PyStrObject.FromString(kwarg.Key), kwarg.Value);

        return dict;
    }

    protected override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        var obj = _new.Call(context, args, kwargs);
        if (obj.IsError)
            return obj;
        obj.Value._pyType = cls;
        return obj;
    }

    protected override PyResult GetItem(PyCallContext context, PyDictObject self, PyObject item)
    {
        if (self.TryGetValue(item, out PyObject? value))
            return value;

        var missing = self.PyType.Slots.Missing;
        if (missing is null)
            return PyResult.KeyError(item);

        return missing(context, self, item);
    }

    protected override PyResult SetItem(PyCallContext context, PyDictObject self, PyObject key, PyObject value)
    {
        self.PySetItem(key, value);
        return PyNoneObject.None;
    }

    protected override PyResult DelItem(PyCallContext context, PyDictObject self, PyObject key)
    {
        if (self.Remove(key))
            return PyNoneObject.None;
        return PyResult.KeyError(key);
    }

    protected override PyResult Contains(PyCallContext context, PyDictObject self, PyObject item)
    {
        return PyBoolObject.FromBoolean(self.ContainsKey(item));
    }

    protected override PyResult Repr(PyCallContext context, PyDictObject self)
    {
        return IPyObjectRecursiveRepr.RecursiveRepr(context, self);
    }

    protected override PyResult Bool(PyCallContext context, PyDictObject self)
    {
        return PyBoolObject.FromBoolean(self.Count > 0);
    }

    protected override PyResult Len(PyCallContext context, PyDictObject self)
    {
        return PyIntObject.FromInteger(self.Count);
    }

    [PyMethod("items")]
    [PyFunctionParameters()]
    private static PyResult Items(PyCallContext context, PyDictObject self, PyArguments arguments)
    {
        return self.PyItems();
    }

    [AIGenerated]
    [PyMethod("keys")]
    [PyFunctionParameters()]
    private static PyResult Keys(PyCallContext context, PyDictObject self, PyArguments arguments)
    {
        return self.PyKeys();
    }

    [AIGenerated]
    [PyMethod("values")]
    [PyFunctionParameters()]
    private static PyResult Values(PyCallContext context, PyDictObject self, PyArguments arguments)
    {
        return self.PyValues();
    }

    [PyMethod("clear")]
    [PyFunctionParameters()]
    private static PyResult Clear(PyCallContext context, PyDictObject self, PyArguments arguments)
    {
        self.PyClear();
        return PyNoneObject.None;
    }

    [PyMethod("get")]
    [PyFunctionParameters("key", "default=None", "/")]
    private static PyResult Get(PyCallContext context, PyDictObject self, PyArguments arguments)
    {
        if (self.PyTryGet(arguments[0], out var value))
            return value;
        return arguments[1];
    }

    [PyMethod("setdefault")]
    [PyFunctionParameters("key", "default=None", "/")]
    private static PyResult SetDefault(PyCallContext context, PyDictObject self, PyArguments arguments)
    {
        return self.PySetDefault(arguments[0], arguments[1]);
    }

    [PyMethod("pop", Order = 1)]
    [PyFunctionParameters("key", "/")]
    private static PyResult Pop_1(PyCallContext context, PyDictObject self, PyArguments arguments)
    {
        var key = arguments[0];
        if (self.PyTryPop(key, out var value))
            return value;
        return PyResult.KeyError(key);
    }

    [PyMethod("pop", Order = 2)]
    [PyFunctionParameters("key", "default", "/")]
    private static PyResult Pop_2(PyCallContext context, PyDictObject self, PyArguments arguments)
    {
        if (self.PyTryPop(arguments[0], out var value))
            return value;
        return arguments[1];
    }

    [PyMethod("popitem")]
    [PyFunctionParameters()]
    private static PyResult PopItem(PyCallContext context, PyDictObject self, PyArguments arguments)
    {
        if (self.PyTryPopItem(out var key, out var value))
            return PyTupleObject.CreateTuple(key, value);
        return PyResult.KeyError(PySR.Runtime_Dictionary_PopEmptyDict);
    }

    [PyMethod("copy")]
    [PyFunctionParameters()]
    private static PyResult Copy(PyCallContext context, PyDictObject self, PyArguments arguments)
    {
        return self.PyCopy();
    }

    [PyMethod("update")]
    [PyFunctionParameters("iterable_or_mapping=None", "**kwargs")]
    private static PyResult UpdateImpl(PyCallContext context, PyDictObject self, PyArguments arguments)
    {
        if (arguments[0] is not PyNoneObject)
        {
            var result = self.PyUpdate(context, arguments[0]);
            if (result.IsError)
                return result;
        }

        foreach (var pair in arguments.ExtraKwargs)
            self.PySetItem(PyStrObject.FromString(pair.Key), pair.Value);

        return PyNoneObject.None;
    }

    [PyClassMethod("fromkeys")]
    [PyFunctionParameters("iterable", "value=None", "/")]
    private static PyResult FromKeysImpl(PyCallContext context, PyTypeObject cls, PyArguments arguments)
    {
        return PyDictObject.PyFromKeys(context, cls, arguments[0], arguments[1]);
    }

    [AIGenerated]
    protected override PyResult Iter(PyCallContext context, PyDictObject self)
    {
        return new PyDictKeyIteratorObject(self);
    }

    [AIGenerated]
    protected override PyResult Eq(PyCallContext context, PyDictObject self, PyObject other)
    {
        if (other is not PyDictObject otherDict)
            return base.Eq(context, self, other);

        if (self.Count != otherDict.Count)
            return PyBoolObject.False;

        foreach (var (key, value) in self)
        {
            if (!otherDict.TryGetValue(key, out var otherValue))
                return PyBoolObject.False;

            var eqResult = PyOperators.Eq(context, value, otherValue);
            if (eqResult.IsError)
                return eqResult;

            var eq = PySpecialMethods.Bool(context, eqResult.Value);
            if (eq.IsError)
                return eq;

            if (!eq.Value.BoolValue)
                return PyBoolObject.False;
        }

        return PyBoolObject.True;
    }
}
