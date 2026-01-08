using PySharp.PyRuntime.Calls;

namespace PySharp.PyModules.Builtins;

public class PyDictItemsObject : PyObject
{
    internal readonly PyDictObject _dict;
    public override PyTypeObject DefaultPyType => PyDictItemsObjectType.Shared;

    internal PyDictItemsObject(PyDictObject dict)
    {
        _dict = dict;
    }
}

public sealed class PyDictItemsObjectType : PyTypeObject<PyDictItemsObjectType, PyDictItemsObject>
{
    public override string Name => "dict_items";

    protected override PyResult Iter(PyCallContext context, PyDictItemsObject self)
    {
        return new PyDictItemIteratorObject(self);
    }
}

public class PyDictItemIteratorObject : PyObject
{
    internal readonly PyDictItemsObject _items;
    internal readonly IEnumerator<PyObject> _keyEnumerator;

    public override PyTypeObject DefaultPyType => PyDictItemIteratorObjectType.Shared;

    internal PyDictItemIteratorObject(PyDictItemsObject items)
    {
        _items = items;
        _keyEnumerator = items._dict._dict.Keys.GetEnumerator();
    }
}

public sealed class PyDictItemIteratorObjectType : PyTypeObject<PyDictItemIteratorObjectType, PyDictItemIteratorObject>
{
    public override string Name => "dict_itemiterator";

    protected override PyResult Iter(PyCallContext context, PyDictItemIteratorObject self)
    {
        return self;
    }

    protected override PyResult Next(PyCallContext context, PyDictItemIteratorObject self)
    {
        if (self._keyEnumerator.MoveNext())
            return PyTupleObject.CreateTuple(self._keyEnumerator.Current, self._items._dict._dict[self._keyEnumerator.Current]);

        return PyResult.RaiseStopIteration();
    }
}