using PySharp.PyRuntime;

namespace PySharp.PyModules.Builtins;

public class PyDictItemsObject : PyObject
{
    internal readonly PyDictObject _dict;

    internal PyDictItemsObject(PyDictObject dict)
    {
        _dict = dict;
    }

    public override PyObject? Iter()
    {
        return new PyDictItemIterator(this);
    }
}

public class PyDictItemIterator : PyObject
{
    private readonly PyDictItemsObject _items;
    private readonly IEnumerator<PyObject> _keyEnumerator;

    internal PyDictItemIterator(PyDictItemsObject items)
    {
        _items = items;
        _keyEnumerator = items._dict._dict.Keys.GetEnumerator();
    }

    public override PyObject? Iter()
    {
        return this;
    }

    public override PyObject? Next()
    {
        if (_keyEnumerator.MoveNext())
            return PyTupleObject.CreateTuple(_keyEnumerator.Current, _items._dict._dict[_keyEnumerator.Current]);

        return PyVirtualMachine.RaiseStopIteration();
    }
}