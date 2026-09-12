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

    protected override PyResult Reversed(PyCallContext context, PyDictItemsObject self)
    {
        return PyDictItemIteratorObject.ReversedItems(self);
    }
}

public sealed class PyDictItemIteratorObject : PyObject
{
    internal readonly PyDictItemsObject _items;
    internal int _index;
    internal readonly int _count;
    private readonly bool _reverse;

    public override PyTypeObject DefaultPyType { get; }

    private PyDictItemIteratorObject(PyTypeObject type, PyDictItemsObject items, bool reverse)
    {
        DefaultPyType = type;
        _items = items;
        _reverse = reverse;
        _index = reverse ? items._dict.Count : -1;
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

        if (_reverse)
        {
            if (_index - 1 < 0)
            {
                _index = -2;
                return PyResult.StopIteration();
            }

            return MakeResult(_items._dict.Entries[--_index]);
        }

        if (_index + 1 >= _count)
        {
            _index = -2;
            return PyResult.StopIteration();
        }

        return MakeResult(_items._dict.Entries[++_index]);
    }

    private PyResult MakeResult(PyDictObject.Entry entry) => DefaultPyType switch
    {
        PyDictItemIteratorObjectType or PyDictReverseItemIteratorObjectType => PyTupleObject.CreateTuple(entry.Key, entry.Value),
        PyDictKeyIteratorObjectType or PyDictReverseKeyIteratorObjectType => entry.Key,
        PyDictValueIteratorObjectType or PyDictReverseValueIteratorObjectType => entry.Value,
        _ => throw new UnreachableException()
    };

    internal static PyDictItemIteratorObject Items(PyDictItemsObject items)
    {
        return new PyDictItemIteratorObject(PyDictItemIteratorObjectType.Shared, items, reverse: false);
    }
    internal static PyDictItemIteratorObject Keys(PyDictItemsObject items)
    {
        return new PyDictItemIteratorObject(PyDictKeyIteratorObjectType.Shared, items, reverse: false);
    }
    internal static PyDictItemIteratorObject Values(PyDictItemsObject items)
    {
        return new PyDictItemIteratorObject(PyDictValueIteratorObjectType.Shared, items, reverse: false);
    }
    internal static PyDictItemIteratorObject ReversedItems(PyDictItemsObject items)
    {
        return new PyDictItemIteratorObject(PyDictReverseItemIteratorObjectType.Shared, items, reverse: true);
    }
    internal static PyDictItemIteratorObject ReversedKeys(PyDictItemsObject items)
    {
        return new PyDictItemIteratorObject(PyDictReverseKeyIteratorObjectType.Shared, items, reverse: true);
    }
    internal static PyDictItemIteratorObject ReversedValues(PyDictItemsObject items)
    {
        return new PyDictItemIteratorObject(PyDictReverseValueIteratorObjectType.Shared, items, reverse: true);
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

    protected override PyResult Reversed(PyCallContext context, PyDictItemsObject self)
    {
        return PyDictItemIteratorObject.ReversedKeys(self);
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

    protected override PyResult Reversed(PyCallContext context, PyDictItemsObject self)
    {
        return PyDictItemIteratorObject.ReversedValues(self);
    }
}

[PyType("dict_reverseitemiterator")]
public sealed partial class PyDictReverseItemIteratorObjectType : PyTypeObject<PyDictItemIteratorObject>
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

[PyType("dict_reversekeyiterator")]
public sealed partial class PyDictReverseKeyIteratorObjectType : PyTypeObject<PyDictItemIteratorObject>
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

[PyType("dict_reversevalueiterator")]
public sealed partial class PyDictReverseValueIteratorObjectType : PyTypeObject<PyDictItemIteratorObject>
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
