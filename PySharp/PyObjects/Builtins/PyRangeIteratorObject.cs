using PySharp.PyRuntime;

namespace PySharp.PyObjects.Builtins;

public class PyRangeIteratorObject : PyObject
{
    private readonly PyRangeObject _range;
    private int _current;

    public PyRangeIteratorObject(PyRangeObject pyRangeObject)
    {
        ArgumentNullException.ThrowIfNull(pyRangeObject);

        _range = pyRangeObject;
        _current = _range.Start;
    }

    public override PyObject? Iter()
    {
        return this;
    }

    public override PyObject? Next()
    {
        if (_range.Step > 0)
        {
            if (_current >= _range.Stop)
                return PyVirtualMachine.RaiseStopIteration();
        }
        else
        {
            if (_current <= _range.Stop)
                return PyVirtualMachine.RaiseStopIteration();
        }

        var value = _current;
        _current += _range.Step;
        return PyIntObject.FromInteger(value);
    }
}

public sealed class PyRangeIteratorObjectType : PyTypeObject
{
    public override string Name => "range_iterator";

    public override PyObject? New(IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        return PyVirtualMachine.RaiseTypeError(null);
    }
}