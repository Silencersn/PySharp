using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;

namespace PySharp.Modules.Builtins;

[AIGenerated]
public sealed class PyBytesIteratorObject : PyObject
{
    private readonly PyBytesObject _bytes;
    private int _index;

    public override PyTypeObject DefaultPyType => PyBytesIteratorObjectType.Shared;

    internal PyBytesIteratorObject(PyBytesObject bytes)
    {
        _bytes = bytes;
        _index = 0;
    }

    internal PyResult PyIter(PyCallContext context)
    {
        return this;
    }

    internal PyResult PyNext(PyCallContext context)
    {
        if (_index >= _bytes.Length)
            return PyResult.StopIteration();
        return PyIntObject.FromInteger(_bytes[_index++]);
    }
}

[AIGenerated]
[PyType("bytes_iterator")]
public sealed partial class PyBytesIteratorObjectType : PyTypeObject<PyBytesIteratorObject>
{
    protected override PyResult Iter(PyCallContext context, PyBytesIteratorObject self)
    {
        return self.PyIter(context);
    }

    protected override PyResult Next(PyCallContext context, PyBytesIteratorObject self)
    {
        return self.PyNext(context);
    }
}
