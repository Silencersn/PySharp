using PySharp.PyRuntime;

namespace PySharp.PyModules.Builtins;

public class PyListIteratorObject : PyObject
{
    private readonly PyListObject _list;
    private int _index;

    public PyListIteratorObject(PyListObject pyListObject)
    {
        ArgumentNullException.ThrowIfNull(pyListObject);

        _list = pyListObject;
        _index = -1;
    }

    public override PyObject? Iter()
    {
        return this;
    }

    public override PyObject? Next()
    {
        if (_index is -2 || ++_index >= _list._list.Count)
        {
            _index = -2;
            return PyVirtualMachine.RaiseStopIteration();
        }

        return _list._list[_index];
    }
}

public sealed class PyListIteratorObjectType : PyTypeObject
{
    public override string Name => "list_iterator";

    public override PyObject? New(IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        return PyVirtualMachine.RaiseTypeError(null);
    }
}
