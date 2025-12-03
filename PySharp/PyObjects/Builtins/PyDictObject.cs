using PySharp.PyRuntime;
using PySharp.PyRuntime.PyAttributes;
using System.Text;

namespace PySharp.PyObjects.Builtins;

public partial class PyDictObject : PyObject, IPyObjectRecursiveRepr
{
    internal readonly OrderedDictionary<PyObject, PyObject> _dict;

    public override PyTypeObject PyType => PyBuiltinTypes.Dict;

    public PyDictObject()
    {
        _dict = [];
    }
    public PyDictObject(IEnumerable<KeyValuePair<PyObject, PyObject>> dict) : this()
    {
        PyUpdate(dict);
    }

    public static PyDictObject CreateDict(IEnumerable<KeyValuePair<PyObject, PyObject>> dict)
    {
        return new PyDictObject(dict);
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
}

public sealed class PyDictObjectType : PyTypeObject
{
    public override string Name => "dict";

    public PyDictObjectType()
    {
        AppendMethodDescriptor<PyDictObject>("items", nameof(PyDictObject.ItemsImpl));
    }

    public override PyObject? New(IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        var pack = new PyArgsPack(args, kwargs);
        if (!pack.ValidateArgsCount(1))
            return PyVirtualMachine.RaiseTypeError(null);

        var kvpiteratables = Utils.EnumerabledIterable(pack[0]);
        if (kvpiteratables is null)
            return null;

        var pairs = Utils.EnumerabledDictionary(kvpiteratables);
        if (pairs is null)
            return null;

        var dict = new Dictionary<PyObject, PyObject>();

        foreach (var pair in pairs)
        {
            dict[pair.Key] = pair.Value;
        }

        for (int i = 0; i < kvpiteratables.Count; i++)
        {
            var kvp = Utils.EnumerabledIterable(kvpiteratables[i]);
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
