using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;

namespace PySharp.Modules.Builtins;

/// <summary>
/// Iterator for memoryview objects. Yields integer values for each element.
/// </summary>
[AIGenerated]
public sealed class PyMemoryViewIteratorObject : PyObject
{
    private readonly PyMemoryViewObject _mv;
    private int _index;

    public override PyTypeObject DefaultPyType => PyMemoryViewIteratorObjectType.Shared;

    internal PyMemoryViewIteratorObject(PyMemoryViewObject mv)
    {
        _mv = mv;
        _index = 0;
    }

    internal PyResult PyIter(PyCallContext context)
    {
        return this;
    }

    internal PyResult PyNext(PyCallContext context)
    {
        // CPython memoryiter_next checks exhaustion first: a spent iterator
        // keeps returning StopIteration even after its view was released
        if (_index >= _mv.Shape[0])
            return PyResult.StopIteration();

        var released = _mv.CheckReleased();
        if (released is not null)
            return released.Value;

        var byteVal = _mv.ReadElement(_index);
        _index++;
        return PyIntObject.FromInteger(byteVal);
    }
}

[AIGenerated]
[PyType("memoryview_iterator")]
public sealed partial class PyMemoryViewIteratorObjectType : PyTypeObject<PyMemoryViewIteratorObject>
{
    protected override PyResult Iter(PyCallContext context, PyMemoryViewIteratorObject self)
    {
        return self.PyIter(context);
    }

    protected override PyResult Next(PyCallContext context, PyMemoryViewIteratorObject self)
    {
        return self.PyNext(context);
    }
}
