using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;
using PySharp.PyRuntime.PyAttributes;

namespace PySharp.PyModules.Builtins;

public partial class PyDictObject : PyObject, IPyObjectRecursiveRepr
{
    internal readonly IDictionary<PyObject, PyObject> _dict;

    public override PyTypeObject DefaultPyType => PyDictObjectType.Shared;

    public PyDictObject()
    {
        _dict = new OrderedDictionary<PyObject, PyObject>();
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
            _dict = new OrderedDictionary<PyObject, PyObject>(dict);
    }

    public static PyDictObject CreateDict(params IEnumerable<KeyValuePair<PyObject, PyObject>> dict)
    {
        return new PyDictObject(dict);
    }
    public static PyDictObject CreateProxy(IDictionary<PyObject, PyObject> dict)
    {
        return new PyDictObject(dict, true);
    }

    PyResult IPyObjectRecursiveRepr.RecursiveRepr(PyCallContext context, HashSet<int> ids)
    {
        return Utils.DictionaryRecursiveRepr(context, this, _dict, "{", "}", ids);
    }
}

public sealed class PyDictObjectType : PyTypeObject<PyDictObjectType, PyDictObject>
{
    public override string Name => "dict";

    public PyDictObjectType()
    {
        AppendMethodDescriptor("items", Items);
        AppendMethodDescriptor("clear", Clear);
        AppendMethodDescriptor("get", Get);
        AppendMethodDescriptor("setdefault", SetDefault);
        AppendMethodDescriptor("pop", Pop_1, Pop_2);
        AppendMethodDescriptor("popitem", PopItem);
        AppendMethodDescriptor("copy", Copy);
    }

    private static readonly PyBuiltinFunctionOrMethodObject _new = PyBuiltinFunctionOrMethodObject.CreateFunction(PySpecialNames.New, NewImpl_1, NewImpl_2);

    [PyFunctionArgsDef("**kwargs")]
    private static PyResult NewImpl_1(PyCallContext context, PyArguments arguments)
    {
        return PyDictObject.CreateDict(arguments.ExtraKwargs
            .Select(pair => KeyValuePair.Create<PyObject, PyObject>(PyStrObject.FromString(pair.Key), pair.Value)));
    }

    [PyFunctionArgsDef("iterable", "/", "**kwargs")]
    private static PyResult NewImpl_2(PyCallContext context, PyArguments arguments)
    {
        if (!Utils.TryEnumeratedIterable(context, arguments[0], out var kvpiteratables, out var err))
            return err.Value;

        if (!Utils.TryEnumeratedPairs(context, kvpiteratables, out var pairs, out err))
            return err.Value;

        List<KeyValuePair<PyObject, PyObject>> dict = [.. pairs];

        foreach (var kwarg in arguments.ExtraKwargs)
        {
            dict.Add(KeyValuePair.Create<PyObject, PyObject>(PyStrObject.FromString(kwarg.Key), kwarg.Value));
        }

        return PyDictObject.CreateDict(dict);
    }

    protected internal override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        var obj = _new.Call(context, args, kwargs);
        if (obj.IsError)
            return obj;
        obj.Value._pyType = cls;
        return obj;
    }

    protected override PyResult GetItem(PyCallContext context, PyDictObject self, PyObject item)
    {
        if (self._dict.TryGetValue(item, out PyObject? value))
            return value;
        return Missing(context, self, item);
    }

    protected override PyResult SetItem(PyCallContext context, PyDictObject self, PyObject key, PyObject value)
    {
        self.PySetItem(key, value);
        return PyNoneObject.None;
    }

    protected override PyResult Contains(PyCallContext context, PyDictObject self, PyObject item)
    {
        return PyBoolObject.FromBoolean(self._dict.ContainsKey(item));
    }

    protected override PyResult Repr(PyCallContext context, PyDictObject self)
    {
        return IPyObjectRecursiveRepr.RecursiveRepr(context, self);
    }

    protected override PyResult Bool(PyCallContext context, PyDictObject self)
    {
        return PyBoolObject.FromBoolean(self._dict.Count > 0);
    }

    protected override PyResult Len(PyCallContext context, PyDictObject self)
    {
        return PyIntObject.FromInteger(self._dict.Count);
    }

    [PyFunctionArgsDef()]
    internal PyResult Items(PyCallContext context, PyDictObject self, PyArguments arguments)
    {
        return self.PyItems();
    }

    [PyFunctionArgsDef()]
    internal PyResult Clear(PyCallContext context, PyDictObject self, PyArguments arguments)
    {
        self.PyClear();
        return PyNoneObject.None;
    }

    [PyFunctionArgsDef("key", "default=None", "/")]
    internal PyResult Get(PyCallContext context, PyDictObject self, PyArguments arguments)
    {
        if (self.PyTryGet(arguments[0], out var value))
            return value;
        return arguments[1];
    }

    [PyFunctionArgsDef("key", "default=None", "/")]
    internal PyResult SetDefault(PyCallContext context, PyDictObject self, PyArguments arguments)
    {
        return self.PySetDefault(arguments[0], arguments[1]);
    }

    [PyFunctionArgsDef("key", "/")]
    internal PyResult Pop_1(PyCallContext context, PyDictObject self, PyArguments arguments)
    {
        var key = arguments[0];
        if (self.PyTryPop(key, out var value))
            return value;
        return PyResult.RaiseKeyError(key);
    }

    [PyFunctionArgsDef("key", "default", "/")]
    internal PyResult Pop_2(PyCallContext context, PyDictObject self, PyArguments arguments)
    {
        if (self.PyTryPop(arguments[0], out var value))
            return value;
        return arguments[1];
    }

    [PyFunctionArgsDef()]
    internal PyResult PopItem(PyCallContext context, PyDictObject self, PyArguments arguments)
    {
        if (self.PyTryPopItem(out var key, out var value))
            return PyTupleObject.CreateTuple(key, value);
        return PyResult.RaiseKeyError("popitem(): dictionary is empty");
    }

    [PyFunctionArgsDef()]
    internal PyResult Copy(PyCallContext context, PyDictObject self, PyArguments arguments)
    {
        return self.PyCopy();
    }
}
