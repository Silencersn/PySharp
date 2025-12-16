using PySharp.PyRuntime;

namespace PySharp.PyModules.Builtins;

public class PyTupleIteratorObject : PyObject
{
    private readonly PyTupleObject _tuple;
    private int _index;

    public PyTupleIteratorObject(PyTupleObject pyTupleObject)
    {
        ArgumentNullException.ThrowIfNull(pyTupleObject);
        _tuple = pyTupleObject;
        _index = -1;
    }

    protected internal override PyObject? IterImpl()
    {
        return this;
    }

    protected internal override PyObject? NextImpl()
    {
        if (_index is -2 || ++_index >= _tuple._array.Length)
        {
            _index = -2;
            return PyVirtualMachine.RaiseStopIteration();
        }
        return _tuple.GetItem(PyIntObject.FromInteger(_index));
    }
}

public sealed class PyTupleIteratorObjectType : PyPrimitiveTypeObject<PyTupleIteratorObjectType, PyTupleIteratorObject>
{
    public override string Name => "tuple_iterator";
}
