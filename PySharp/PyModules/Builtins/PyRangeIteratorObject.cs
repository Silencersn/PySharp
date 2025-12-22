using PySharp.PyRuntime.Calls;

namespace PySharp.PyModules.Builtins;

public class PyRangeIteratorObject : PyObject
{
    internal readonly IEnumerator<PyIntObject> _enumerator;
    public override PyTypeObject DefaultPyType => PyRangeIteratorObjectType.Shared;

    internal PyRangeIteratorObject(IEnumerable<PyIntObject> enumerable)
    {
        _enumerator = enumerable.GetEnumerator();
    }
}

public sealed class PyRangeIteratorObjectType : PyTypeObject<PyRangeIteratorObjectType, PyRangeIteratorObject>
{
    public override string Name => "range_iterator";

    protected internal override PyResult Iter(PyCallContext context, PyRangeIteratorObject self)
    {
        return self;
    }

    protected internal override PyResult Next(PyCallContext context, PyRangeIteratorObject self)
    {
        if (!self._enumerator.MoveNext())
            return PyResult.RaiseStopIteration();
        return self._enumerator.Current;
    }

    protected internal override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        return PyResult.RaiseTypeError(null);
    }
}