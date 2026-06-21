using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;

namespace PySharp.Modules.Builtins;

public class PySetIteratorObject : PyObject
{
    internal HashSet<PyObject>.Enumerator _enumerator;
    internal bool _started;
    internal bool _exhausted;

    public override PyTypeObject DefaultPyType => PySetIteratorObjectType.Shared;

    public PySetIteratorObject(PySetObject pySetObject)
    {
        ArgumentNullException.ThrowIfNull(pySetObject);
        _enumerator = pySetObject.GetEnumerator();
        _started = false;
        _exhausted = false;
    }

    public PySetIteratorObject(PyFrozenSetObject pyFrozenSetObject)
    {
        ArgumentNullException.ThrowIfNull(pyFrozenSetObject);
        _enumerator = pyFrozenSetObject.GetEnumerator();
        _started = false;
        _exhausted = false;
    }
}

[PyType("set_iterator")]
public sealed partial class PySetIteratorObjectType : PyTypeObject<PySetIteratorObject>
{
    protected override PyResult Iter(PyCallContext context, PySetIteratorObject self)
    {
        return self;
    }

    protected override PyResult Next(PyCallContext context, PySetIteratorObject self)
    {
        if (self._exhausted)
            return PyResult.StopIteration();

        if (!self._started)
        {
            self._started = true;
        }

        if (self._enumerator.MoveNext())
            return self._enumerator.Current;

        self._exhausted = true;
        self._enumerator.Dispose();
        return PyResult.StopIteration();
    }
}
