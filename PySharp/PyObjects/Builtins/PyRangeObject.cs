using PySharp.PyRuntime;

namespace PySharp.PyObjects.Builtins;

public class PyRangeObject : PyObject
{
    public override PyTypeObject PyType => PyBuiltinTypes.Range;

    private readonly int _start, _stop, _step;

    private PyRangeObject(int start, int stop, int step)
    {
        _start = start;
        _stop = stop;
        _step = step;
    }

    public int Start => _start;
    public int Stop => _stop;
    public int Step => _step;

    public static PyRangeObject CreateRange(int stop)
    {
        return new PyRangeObject(0, stop, 1);
    }
    public static PyRangeObject CreateRange(int start, int stop, int step = 1)
    {
        return new PyRangeObject(start, stop, step);
    }

    public override PyObject? Repr()
    {
        if (_step is 1)
            return PyStrObject.FromString($"range({_start}, {_stop})");

        return PyStrObject.FromString($"range({_start}, {_stop}, {_step})");
    }

    public override PyObject? Iter()
    {
        return new PyRangeIteratorObject(this);
    }
}

public sealed class PyRangeObjectType : PyTypeObject
{
    public override string Name => "range";

    public override PyObject? New(IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        if (kwargs.Count is not 0)
            return PyVirtualMachine.RaiseTypeError(null);

        if (args.Count is 1)
        {
            if (args[0] is not PyIntObject stopObj)
                return PyVirtualMachine.RaiseTypeError(null);

            return PyRangeObject.CreateRange(stopObj.Int32Value);
        }
        else if (args.Count is 2)
        {
            if (args[0] is not PyIntObject startObj)
                return PyVirtualMachine.RaiseTypeError(null);

            if (args[1] is not PyIntObject stopObj)
                return PyVirtualMachine.RaiseTypeError(null);

            return PyRangeObject.CreateRange(startObj.Int32Value, stopObj.Int32Value);
        }
        else if (args.Count is 3)
        {
            if (args[0] is not PyIntObject startObj)
                return PyVirtualMachine.RaiseTypeError(null);

            if (args[1] is not PyIntObject stopObj)
                return PyVirtualMachine.RaiseTypeError(null);

            if (args[2] is not PyIntObject stepObj)
                return PyVirtualMachine.RaiseTypeError(null);

            if (stepObj.Int32Value is 0)
                return PyVirtualMachine.RaiseValueError("range() arg 3 must not be zero");

            return PyRangeObject.CreateRange(startObj.Int32Value, stopObj.Int32Value, stepObj.Int32Value);
        }

        return PyVirtualMachine.RaiseTypeError(null);
    }
}
