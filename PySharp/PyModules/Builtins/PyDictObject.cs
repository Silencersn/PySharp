using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;
using PySharp.PyRuntime.PyAttributes;

namespace PySharp.PyModules.Builtins;

public partial class PyDictObject : PyObject, IPyObjectRecursiveRepr
{
    internal readonly IDictionary<PyObject, PyObject> _dict;

    public override PyTypeObject PyType => PyBuiltinTypes.Dict;

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

public sealed class PyDictObjectType : PyTypeObject
{
    public override string Name => "dict";

    public PyDictObjectType()
    {
        AppendSpecialMethodsAsDescriptors<PyDictObject>();
        AppendMethodDescriptor<PyDictObject>("items", nameof(PyDictObject.ItemsImpl));
        AppendMethodDescriptor<PyDictObject>("clear", nameof(PyDictObject.ClearImpl));
        AppendMethodDescriptor<PyDictObject>("get", nameof(PyDictObject.GetImpl));
        AppendMethodDescriptor<PyDictObject>("setdefault", nameof(PyDictObject.SetDefaultImpl));
        AppendMethodDescriptor<PyDictObject>("pop", nameof(PyDictObject.PopImpl_1), nameof(PyDictObject.PopImpl_2));
        AppendMethodDescriptor<PyDictObject>("popitem", nameof(PyDictObject.PopItemImpl));
        AppendMethodDescriptor<PyDictObject>("copy", nameof(PyDictObject.CopyImpl));
    }

    public override PyObject? New(IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        var pack = new PyArgsPack(args, kwargs);
        if (!pack.ValidateArgsCount(1))
            return PyVirtualMachine.RaiseTypeError(null);

        var kvpiteratables = Utils.EnumeratedIterable(pack[0]);
        if (kvpiteratables is null)
            return null;

        var pairs = Utils.EnumeratedDictionary(kvpiteratables);
        if (pairs is null)
            return null;

        var dict = new Dictionary<PyObject, PyObject>();

        foreach (var pair in pairs)
        {
            dict[pair.Key] = pair.Value;
        }

        for (int i = 0; i < kvpiteratables.Count; i++)
        {
            var kvp = Utils.EnumeratedIterable(kvpiteratables[i]);
            if (kvp is null)
                return null;

            if (kvp!.Count is not 2)
                return PyVirtualMachine.RaiseValueError($"dictionary update sequence element #{i} has length {kvp.Count}; 2 is required");

            dict[kvp[0]] = kvp[1];
        }

        foreach (var kvp in kwargs)
        {
            dict[PyStrObject.FromString(kvp.Key)] = kvp.Value;
        }

        return new PyDictObject(dict);
    }
}
