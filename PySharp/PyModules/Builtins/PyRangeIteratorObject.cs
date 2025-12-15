using PySharp.PyRuntime;

namespace PySharp.PyModules.Builtins;

public class PyRangeIteratorObject : PyObject
{
    private readonly IEnumerator<PyIntObject> _enumerator;

    public override PyTypeObject DefaultPyType => PyRangeIteratorObjectType.Shared;

    internal PyRangeIteratorObject(IEnumerable<PyIntObject> enumerable)
    {
        _enumerator = enumerable.GetEnumerator();
    }

    public override PyObject? Iter()
    {
        return this;
    }

    public override PyObject? Next()
    {
        if (!_enumerator.MoveNext())
            return PyVirtualMachine.RaiseStopIteration();

        return _enumerator.Current;
    }
}

public sealed class PyRangeIteratorObjectType : PyPrimitiveTypeObject<PyRangeIteratorObjectType, PyRangeIteratorObject>
{
    public override string Name => "range_iterator";

    public override PyObject? New(PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        return PyVirtualMachine.RaiseTypeError(null);
    }
}