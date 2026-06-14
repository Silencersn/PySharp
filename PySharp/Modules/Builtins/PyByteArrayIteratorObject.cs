using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;

namespace PySharp.Modules.Builtins;

[AIGenerated]
public sealed class PyByteArrayIteratorObject : PyObject
{
    private readonly PyByteArrayObject _byteArray;
    private int _index;

    public override PyTypeObject DefaultPyType => PyByteArrayIteratorObjectType.Shared;

    internal PyByteArrayIteratorObject(PyByteArrayObject byteArray)
    {
        _byteArray = byteArray;
        _index = 0;
    }

    internal PyResult Next()
    {
        if (_index >= _byteArray.Length)
            return PyResult.StopIteration();

        return PyIntObject.FromInteger(_byteArray[_index++]);
    }
}

[AIGenerated]
[PyType("bytearray_iterator")]
public sealed partial class PyByteArrayIteratorObjectType : PyTypeObject<PyByteArrayIteratorObject>
{
    protected override PyResult Iter(PyCallContext context, PyByteArrayIteratorObject self)
    {
        return self;
    }

    protected override PyResult Next(PyCallContext context, PyByteArrayIteratorObject self)
    {
        return self.Next();
    }
}
