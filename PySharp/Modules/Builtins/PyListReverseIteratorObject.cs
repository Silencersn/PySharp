using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;

namespace PySharp.Modules.Builtins;

public class PyListReverseIteratorObject : PyObject
{
    internal readonly PyListObject _list;
    internal int _index;

    public override PyTypeObject DefaultPyType => PyListReverseIteratorObjectType.Shared;

    internal PyListReverseIteratorObject(PyListObject list, int len)
    {
        _list = list;
        _index = len - 1;
    }
}

[PyType("list_reverseiterator")]
public sealed partial class PyListReverseIteratorObjectType : PyTypeObject<PyListReverseIteratorObject>
{
    protected override PyResult Iter(PyCallContext context, PyListReverseIteratorObject self)
    {
        return self;
    }

    // CPython listreviter_next: the index must fall within the current
    // length, so clearing the list stops the iterator on the next step
    protected override PyResult Next(PyCallContext context, PyListReverseIteratorObject self)
    {
        if (self._index < 0 || self._index >= self._list.Count)
        {
            self._index = -1;
            return PyResult.StopIteration();
        }

        var item = self._list[self._index];
        self._index--;
        return item;
    }
}
