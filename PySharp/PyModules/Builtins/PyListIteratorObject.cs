using PySharp.PyRuntime;

namespace PySharp.PyModules.Builtins;

public class PyListIteratorObject : PyObject
{
    private readonly PyListObject _list;
    private int _index;

    public override PyTypeObject DefaultPyType => PyListIteratorObjectType.Shared;

    public PyListIteratorObject(PyListObject pyListObject)
    {
        ArgumentNullException.ThrowIfNull(pyListObject);

        _list = pyListObject;
        _index = -1;
    }

    protected internal override PyObject? IterImpl()
    {
        return this;
    }

    protected internal override PyObject? NextImpl()
    {
        if (_index is -2 || ++_index >= _list._list.Count)
        {
            _index = -2;
            return PyVirtualMachine.RaiseStopIteration();
        }

        return _list._list[_index];
    }
}

public sealed class PyListIteratorObjectType : PyPrimitiveTypeObject<PyListIteratorObjectType, PyListIteratorObject>
{
    public override string Name => "list_iterator";
}
