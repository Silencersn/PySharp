using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;

namespace PySharp.Modules.Builtins;

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

[PyType("list_iterator")]
public sealed partial class PyListIteratorObjectType : PyTypeObject<PyListIteratorObjectType, PyListIteratorObject>
{
    public override string Name => "list_iterator";

    protected override PyResult Iter(PyCallContext context, PyListIteratorObject self)
    {
        return self;
    }

    protected override PyResult Next(PyCallContext context, PyListIteratorObject self)
    {
        if (self._index is -2 || ++self._index >= self._list.Count)
        {
            self._index = -2;
            return PyResult.StopIteration();
        }
        return self._list[self._index];
    }
}
