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

    public override PyObject? GetItem(PyObject item)
    {
        if (_dict.TryGetValue(item, out PyObject? value))
            return value;

        return Missing(item);
    }

    public override PyObject? SetItem(PyObject key, PyObject value)
    {
        PySetItem(key, value);
        return PyNoneObject.None;
    }

    public override PyObject? Contains(PyObject item)
    {
        return PyBoolObject.FromBoolean(_dict.ContainsKey(item));
    }

    public override PyObject? Len()
    {
        return PyIntObject.FromInteger(_dict.Count);
    }

    public override PyBoolObject Bool()
    {
        return PyBoolObject.FromBoolean(_dict.Count > 0);
    }

    public override PyObject? Repr()
    {
        return IPyObjectRecursiveRepr.RecursiveRepr(this);
    }

    PyObject? IPyObjectRecursiveRepr.RecursiveRepr(HashSet<int> ids)
    {
        return Utils.DictionaryRecursiveRepr(this, _dict, "{", "}", ids);
    }

    [PyFunctionArgsDef()]
    internal PyDictItemsObject ItemsImpl(PyArguments arguments)
    {
        return PyItems();
    }

    [PyFunctionArgsDef()]
    internal PyNoneObject ClearImpl(PyArguments arguments)
    {
        PyClear();
        return PyNoneObject.None;
    }

    [PyFunctionArgsDef("key", "default=None", "/")]
    internal PyObject GetImpl(PyArguments arguments)
    {
        if (PyTryGet(arguments[0], out var value))
            return value;
        return arguments[1];
    }

    [PyFunctionArgsDef("key", "default=None", "/")]
    internal PyObject SetDefaultImpl(PyArguments arguments)
    {
        return PySetDefault(arguments[0], arguments[1]);
    }

    [PyFunctionArgsDef("key", "/")]
    internal PyObject? PopImpl_1(PyArguments arguments)
    {
        var key = arguments[0];
        if (PyTryPop(key, out var value))
            return value;
        return PyVirtualMachine.RaiseKeyError(key);
    }

    [PyFunctionArgsDef("key", "default", "/")]
    internal PyObject PopImpl_2(PyArguments arguments)
    {
        if (PyTryPop(arguments[0], out var value))
            return value;
        return arguments[1];
    }

    [PyFunctionArgsDef()]
    internal PyObject? PopItemImpl(PyArguments arguments)
    {
        if (PyTryPopItem(out var key, out var value))
            return PyTupleObject.CreateTuple(key, value);
        return PyVirtualMachine.RaiseKeyError("popitem(): dictionary is empty");
    }

    [PyFunctionArgsDef()]
    internal PyDictObject CopyImpl(PyArguments arguments)
    {
        return PyCopy();
    }
}

public sealed class PyDictObjectType : PyPrimitiveTypeObject<PyDictObjectType, PyDictObject>
{
    public override string Name => "dict";

    public PyDictObjectType()
    {
        AppendMethodDescriptor<PyDictObject>("items", nameof(PyDictObject.ItemsImpl));
        AppendMethodDescriptor<PyDictObject>("clear", nameof(PyDictObject.ClearImpl));
        AppendMethodDescriptor<PyDictObject>("get", nameof(PyDictObject.GetImpl));
        AppendMethodDescriptor<PyDictObject>("setdefault", nameof(PyDictObject.SetDefaultImpl));
        AppendMethodDescriptor<PyDictObject>("pop", nameof(PyDictObject.PopImpl_1), nameof(PyDictObject.PopImpl_2));
        AppendMethodDescriptor<PyDictObject>("popitem", nameof(PyDictObject.PopItemImpl));
        AppendMethodDescriptor<PyDictObject>("copy", nameof(PyDictObject.CopyImpl));
    }

    private static readonly PyBuiltinFunctionOrMethodObject _new = new(PySpecialNames.New, NewImpl_1, NewImpl_2);

    [PyFunctionArgsDef("**kwargs")]
    private static PyDictObject NewImpl_1(PyArguments arguments)
    {
        return PyDictObject.CreateDict(arguments.ExtraKwargs
            .Select(pair => KeyValuePair.Create<PyObject, PyObject>(PyStrObject.FromString(pair.Key), pair.Value)));
    }

    [PyFunctionArgsDef("iterable", "/", "**kwargs")]
    private static PyObject? NewImpl_2(PyArguments arguments)
    {
        var kvpiteratables = Utils.EnumeratedIterable(arguments[0]);
        if (kvpiteratables is null)
            return null;

        var pairs = Utils.EnumeratedDictionary(kvpiteratables);
        if (pairs is null)
            return null;

        List<KeyValuePair<PyObject, PyObject>> dict = [.. pairs];

        for (int i = 0; i < kvpiteratables.Count; i++)
        {
            var pair = Utils.EnumeratedIterable(kvpiteratables[i]);
            if (pair is null)
                return null;

            if (pair!.Count is not 2)
                return PyVirtualMachine.RaiseValueError($"dictionary update sequence element #{i} has length {pair.Count}; 2 is required");

            dict.Add(KeyValuePair.Create(pair[0], pair[1]));
        }

        foreach (var kwarg in arguments.ExtraKwargs)
        {
            dict.Add(KeyValuePair.Create<PyObject, PyObject>(PyStrObject.FromString(kwarg.Key), kwarg.Value));
        }

        return PyDictObject.CreateDict(dict);
    }

    public override PyObject? New(PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        return _new.Call(args, kwargs);
    }
}
