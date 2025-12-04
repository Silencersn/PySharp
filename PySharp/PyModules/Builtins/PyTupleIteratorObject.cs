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

    public override PyObject? Iter()
    {
        return this;
    }

    public override PyObject? Next()
    {
        if (_index is -2 || ++_index >= _tuple._array.Length)
        {
            _index = -2;
            return PyVirtualMachine.RaiseStopIteration();
        }
        return _tuple.GetItem(PyIntObject.FromInteger(_index));
    }
}

public sealed class PyTupleIteratorObjectType : PyTypeObject
{
    public override string Name => "tuple_iterator";

    public override PyObject? New(IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        return PyVirtualMachine.RaiseTypeError(null);
    }
}
