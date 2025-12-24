using PySharp.PyRuntime.Calls;

namespace PySharp.PyModules.Builtins;

public class PyTupleIteratorObject : PyObject
{
    internal readonly PyTupleObject _tuple;
    internal int _index;

    public override PyTypeObject DefaultPyType => PyTupleIteratorObjectType.Shared;

    public PyTupleIteratorObject(PyTupleObject pyTupleObject)
    {
        ArgumentNullException.ThrowIfNull(pyTupleObject);
        _tuple = pyTupleObject;
        _index = -1;
    }
}

public sealed class PyTupleIteratorObjectType : PyTypeObject<PyTupleIteratorObjectType, PyTupleIteratorObject>
{
    public override string Name => "tuple_iterator";

    protected internal override PyResult Iter(PyCallContext context, PyTupleIteratorObject self)
    {
        return self;
    }

    protected internal override PyResult Next(PyCallContext context, PyTupleIteratorObject self)
    {
        if (self._index is -2 || ++self._index >= self._tuple._array.Length)
        {
            self._index = -2;
            return PyResult.RaiseStopIteration();
        }
        return self._tuple.GetItem(context, PyIntObject.FromInteger(self._index));
    }
}
