using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;

namespace PySharp.Modules.Builtins;

public class PyDictItemsObject : PyObject
{
    internal readonly PyDictObject _dict;
    public override PyTypeObject DefaultPyType => PyDictItemsObjectType.Shared;

    internal PyDictItemsObject(PyDictObject dict)
    {
        _dict = dict;
    }
}

[PyType("dict_items")]
public sealed partial class PyDictItemsObjectType : PyTypeObject<PyDictItemsObject>
{

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
        _keyEnumerator = items._dict.Keys.GetEnumerator();
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
        if (self._keyEnumerator.MoveNext())
            return PyTupleObject.CreateTuple(self._keyEnumerator.Current, self._items._dict[self._keyEnumerator.Current]);

        return PyResult.StopIteration();
    }
}

[AIGenerated]
public class PyDictKeysObject : PyObject
{
    internal readonly PyDictObject _dict;
    public override PyTypeObject DefaultPyType => PyDictKeysObjectType.Shared;

    internal PyDictKeysObject(PyDictObject dict)
    {
        _dict = dict;
    }
}

[AIGenerated]
[PyType("dict_keys")]
public sealed partial class PyDictKeysObjectType : PyTypeObject<PyDictKeysObject>
{
    protected override PyResult Iter(PyCallContext context, PyDictKeysObject self)
    {
        return new PyDictKeyIteratorObject(self);
    }
}

[AIGenerated]
public class PyDictKeyIteratorObject : PyObject
{
    internal readonly IEnumerator<PyObject> _keyEnumerator;

    public override PyTypeObject DefaultPyType => PyDictKeyIteratorObjectType.Shared;

    internal PyDictKeyIteratorObject(PyDictKeysObject keys)
    {
        _keyEnumerator = keys._dict.Keys.GetEnumerator();
    }

    internal PyDictKeyIteratorObject(PyDictObject dict)
    {
        _keyEnumerator = dict.Keys.GetEnumerator();
    }
}

[AIGenerated]
[PyType("dict_keyiterator")]
public sealed partial class PyDictKeyIteratorObjectType : PyTypeObject<PyDictKeyIteratorObject>
{
    protected override PyResult Iter(PyCallContext context, PyDictKeyIteratorObject self)
    {
        return self;
    }

    protected override PyResult Next(PyCallContext context, PyDictKeyIteratorObject self)
    {
        if (self._keyEnumerator.MoveNext())
            return self._keyEnumerator.Current;

        return PyResult.StopIteration();
    }
}

[AIGenerated]
public class PyDictValuesObject : PyObject
{
    internal readonly PyDictObject _dict;
    public override PyTypeObject DefaultPyType => PyDictValuesObjectType.Shared;

    internal PyDictValuesObject(PyDictObject dict)
    {
        _dict = dict;
    }
}

[AIGenerated]
[PyType("dict_values")]
public sealed partial class PyDictValuesObjectType : PyTypeObject<PyDictValuesObject>
{
    protected override PyResult Iter(PyCallContext context, PyDictValuesObject self)
    {
        return new PyDictValueIteratorObject(self);
    }
}

[AIGenerated]
public class PyDictValueIteratorObject : PyObject
{
    internal readonly IEnumerator<PyObject> _valueEnumerator;

    public override PyTypeObject DefaultPyType => PyDictValueIteratorObjectType.Shared;

    internal PyDictValueIteratorObject(PyDictValuesObject values)
    {
        _valueEnumerator = values._dict.Values.GetEnumerator();
    }
}

[AIGenerated]
[PyType("dict_valueiterator")]
public sealed partial class PyDictValueIteratorObjectType : PyTypeObject<PyDictValueIteratorObject>
{
    protected override PyResult Iter(PyCallContext context, PyDictValueIteratorObject self)
    {
        return self;
    }

    protected override PyResult Next(PyCallContext context, PyDictValueIteratorObject self)
    {
        if (self._valueEnumerator.MoveNext())
            return self._valueEnumerator.Current;

        return PyResult.StopIteration();
    }
}
