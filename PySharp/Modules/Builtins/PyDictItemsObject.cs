using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;
using System.Diagnostics;

namespace PySharp.Modules.Builtins;

public sealed class PyDictItemsObject : PyObject
{
    internal readonly PyDictObject _dict;
    public override PyTypeObject DefaultPyType { get; }

    private PyDictItemsObject(PyTypeObject type, PyDictObject dict)
    {
        DefaultPyType = type;
        _dict = dict;
    }

    internal static PyDictItemsObject Items(PyDictObject dict)
    {
        return new PyDictItemsObject(PyDictItemsObjectType.Shared, dict);
    }
    internal static PyDictItemsObject Keys(PyDictObject dict)
    {
        return new PyDictItemsObject(PyDictKeysObjectType.Shared, dict);
    }
    internal static PyDictItemsObject Values(PyDictObject dict)
    {
        return new PyDictItemsObject(PyDictValuesObjectType.Shared, dict);
    }
}

[PyType("dict_items")]
public sealed partial class PyDictItemsObjectType : PyTypeObject<PyDictItemsObject>
{
    protected override PyResult Iter(PyCallContext context, PyDictItemsObject self)
    {
        return PyDictItemIteratorObject.Items(self);
    }
}

public sealed class PyDictItemIteratorObject : PyObject
{
    internal readonly PyDictItemsObject _items;
    internal int _index;
    internal readonly int _count;

    public override PyTypeObject DefaultPyType { get; }

    private PyDictItemIteratorObject(PyTypeObject type, PyDictItemsObject items)
    {
        DefaultPyType = type;
        _items = items;
        _index = -1;
        _count = items._dict.Count;
    }

    internal PyResult Next()
    {
        if (_index is -2)
            return PyResult.StopIteration();

        if (_count != _items._dict.Count)
            _index = -3;

        if (_index is -3)
            return PyResult.RuntimeError("dictionary changed size during iteration");

        if (_index + 1 >= _count)
        {
            _index = -2;
            return PyResult.StopIteration();
        }

        var entry = _items._dict.Entries[++_index];
        return DefaultPyType switch
        {
            PyDictItemIteratorObjectType => PyTupleObject.CreateTuple(entry.Key, entry.Value),
            PyDictKeyIteratorObjectType => entry.Key,
            PyDictValueIteratorObjectType => entry.Value,
            _ => throw new UnreachableException()
        };
    }

    internal static PyDictItemIteratorObject Items(PyDictItemsObject items)
    {
        return new PyDictItemIteratorObject(PyDictItemIteratorObjectType.Shared, items);
    }
    internal static PyDictItemIteratorObject Keys(PyDictItemsObject items)
    {
        return new PyDictItemIteratorObject(PyDictKeyIteratorObjectType.Shared, items);
    }
    internal static PyDictItemIteratorObject Values(PyDictItemsObject items)
    {
        return new PyDictItemIteratorObject(PyDictValueIteratorObjectType.Shared, items);
    }

}

[PyType("dict_itemiterator")]
public sealed partial class PyDictItemIteratorObjectType : PyTypeObject<PyDictItemIteratorObject>
{
    protected override PyResult Iter(PyCallContext context, PyDictItemIteratorObject self)
    {
        return self;
    }
    protected override PyResult Next(PyCallContext context, PyDictItemIteratorObject self)
    {
        return self.Next();
    }
}

[PyType("dict_keys")]
public sealed partial class PyDictKeysObjectType : PyTypeObject<PyDictItemsObject>
{
    protected override PyResult Iter(PyCallContext context, PyDictItemsObject self)
    {
        return PyDictItemIteratorObject.Keys(self);
    }
}

[PyType("dict_keyiterator")]
public sealed partial class PyDictKeyIteratorObjectType : PyTypeObject<PyDictItemIteratorObject>
{
    protected override PyResult Iter(PyCallContext context, PyDictItemIteratorObject self)
    {
        return self;
    }
    protected override PyResult Next(PyCallContext context, PyDictItemIteratorObject self)
    {
        return self.Next();
    }
}

[PyType("dict_values")]
public sealed partial class PyDictValuesObjectType : PyTypeObject<PyDictItemsObject>
{
    protected override PyResult Iter(PyCallContext context, PyDictItemsObject self)
    {
        return PyDictItemIteratorObject.Values(self);
    }
}

[PyType("dict_valueiterator")]
public sealed partial class PyDictValueIteratorObjectType : PyTypeObject<PyDictItemIteratorObject>
{
    protected override PyResult Iter(PyCallContext context, PyDictItemIteratorObject self)
    {
        return self;
    }
    protected override PyResult Next(PyCallContext context, PyDictItemIteratorObject self)
    {
        return self.Next();
    }
}
