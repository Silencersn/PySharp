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

    PyObject? IPyObjectRecursiveRepr.RecursiveRepr(HashSet<int> ids)
    {
        return Utils.DictionaryRecursiveRepr(this, _dict, "{", "}", ids);
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

    private static readonly PyBuiltinFunctionOrMethodObject _new = new(PySpecialNames.New, NewImpl_1, NewImpl_2);

    [PyFunctionArgsDef("**kwargs")]
    private static PyResult NewImpl_1(PyCallContext context, PyArguments arguments)
    {
        return PyDictObject.CreateDict(arguments.ExtraKwargs
            .Select(pair => KeyValuePair.Create<PyObject, PyObject>(PyStrObject.FromString(pair.Key), pair.Value)));
    }

    [PyFunctionArgsDef("iterable", "/", "**kwargs")]
    private static PyResult NewImpl_2(PyCallContext context, PyArguments arguments)
    {
        var kvpiteratables = Utils.EnumeratedIterable(arguments[0]);
        if (kvpiteratables is null)
            return PyResult.CaptureExceptionFromPVM();

        var pairs = Utils.EnumeratedDictionary(kvpiteratables);
        if (pairs is null)
            return PyResult.CaptureExceptionFromPVM();

        List<KeyValuePair<PyObject, PyObject>> dict = [.. pairs];

        for (int i = 0; i < kvpiteratables.Count; i++)
        {
            var pair = Utils.EnumeratedIterable(kvpiteratables[i]);
            if (pair is null)
                return PyResult.CaptureExceptionFromPVM();

            if (pair!.Count is not 2)
                return PyResult.RaiseValueError($"dictionary update sequence element #{i} has length {pair.Count}; 2 is required");

            dict.Add(KeyValuePair.Create(pair[0], pair[1]));
        }

        foreach (var kwarg in arguments.ExtraKwargs)
        {
            dict.Add(KeyValuePair.Create<PyObject, PyObject>(PyStrObject.FromString(kwarg.Key), kwarg.Value));
        }

        return PyDictObject.CreateDict(dict);
    }

    protected internal override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        var obj = _new.Call(args, kwargs);
        if (obj is null)
            return PyResult.CaptureExceptionFromPVM();
        obj._pyType = cls;
        return obj;
    }

    protected internal override PyResult GetItem(PyCallContext context, PyDictObject self, PyObject item)
    {
        if (self._dict.TryGetValue(item, out PyObject? value))
            return value;
        return Missing(context, self, item);
    }

    protected internal override PyResult SetItem(PyCallContext context, PyDictObject self, PyObject key, PyObject value)
    {
        self.PySetItem(key, value);
        return PyNoneObject.None;
    }

    protected internal override PyResult Contains(PyCallContext context, PyDictObject self, PyObject item)
    {
        return PyBoolObject.FromBoolean(self._dict.ContainsKey(item));
    }

    protected internal override PyResult Repr(PyCallContext context, PyDictObject self)
    {
        return IPyObjectRecursiveRepr.RecursiveRepr(self) ?? PyResult.CaptureExceptionFromPVM();
    }

    protected internal override PyResult Bool(PyCallContext context, PyDictObject self)
    {
        return PyBoolObject.FromBoolean(self._dict.Count > 0);
    }

    protected internal override PyResult Len(PyCallContext context, PyDictObject self)
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
