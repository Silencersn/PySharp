using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;

namespace PySharp.Modules.Builtins;

public class PyIteratorObject : PyObject
{
    internal readonly PyObject _iter;
    internal int _index; // -1: end

    public override PyTypeObject DefaultPyType => PyIteratorObjectType.Shared;

    internal PyIteratorObject(PyObject iter)
    {
        _iter = iter;
        _index = 0;
    }
}

[PyType("iterator")]
public sealed partial class PyIteratorObjectType : PyTypeObject<PyIteratorObject>
{

    protected override PyResult Iter(PyCallContext context, PyIteratorObject self)
    {
        return self;
    }

    protected override PyResult Next(PyCallContext context, PyIteratorObject self)
    {
        if (self._index is -1)
            return PyResult.StopIteration();

        var result = PySpecialMethods.GetItem(context, self._iter, PyIntObject.FromInteger(self._index));
        if (result.IsError)
        {
            if (!PyIndexErrorObjectType.Shared.IsInstance(result.Exception))
                return result;

            self._index = -1;
            return PyResult.StopIteration();
        }

        self._index++;
        return result;
    }
}
