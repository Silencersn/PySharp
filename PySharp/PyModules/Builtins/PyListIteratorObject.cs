using PySharp.PyRuntime.Calls;

namespace PySharp.PyModules.Builtins;

public class PyListIteratorObject : PyObject
{
    internal readonly PyListObject _list;
    internal int _index;

    public override PyTypeObject DefaultPyType => PyListIteratorObjectType.Shared;

    public PyListIteratorObject(PyListObject pyListObject)
    {
        ArgumentNullException.ThrowIfNull(pyListObject);
        _list = pyListObject;
        _index = -1;
    }
}

public sealed class PyListIteratorObjectType : PyTypeObject<PyListIteratorObjectType, PyListIteratorObject>
{
    public override string Module => "builtins";
    public override string Name => "list_iterator";

    protected override PyResult Iter(PyCallContext context, PyListIteratorObject self)
    {
        return self;
    }

    protected override PyResult Next(PyCallContext context, PyListIteratorObject self)
    {
        if (self._index is -2 || ++self._index >= self._list._list.Count)
        {
            self._index = -2;
            return PyResult.RaiseStopIteration();
        }
        return self._list._list[self._index];
    }
}
