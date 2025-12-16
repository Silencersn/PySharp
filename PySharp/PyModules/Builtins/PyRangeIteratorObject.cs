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

    protected internal override PyObject? IterImpl()
    {
        return this;
    }

    protected internal override PyObject? NextImpl()
    {
        if (!_enumerator.MoveNext())
            return PyVirtualMachine.RaiseStopIteration();

        return _enumerator.Current;
    }
}

public sealed class PyRangeIteratorObjectType : PyPrimitiveTypeObject<PyRangeIteratorObjectType, PyRangeIteratorObject>
{
    public override string Name => "range_iterator";

    protected internal override PyObject? New(PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        return PyVirtualMachine.RaiseTypeError(null);
    }
}