using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;

namespace PySharp.Modules.Builtins;

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

[PyType("tuple_iterator")]
public sealed partial class PyTupleIteratorObjectType : PyTypeObject<PyTupleIteratorObject>
{

    protected override PyResult Iter(PyCallContext context, PyTupleIteratorObject self)
    {
        return self;
    }

    protected override PyResult Next(PyCallContext context, PyTupleIteratorObject self)
    {
        if (self._index is -2 || ++self._index >= self._tuple.Count)
        {
            self._index = -2;
            return PyResult.StopIteration();
        }
        return PySpecialMethods.GetItem(context, self._tuple, PyIntObject.FromInteger(self._index));
    }
}
