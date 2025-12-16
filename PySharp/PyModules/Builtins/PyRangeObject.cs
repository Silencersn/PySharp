using PySharp.PyRuntime;
using System.Diagnostics;
using System.Numerics;

namespace PySharp.PyModules.Builtins;

public class PyRangeObject : PyObject
{
    public override PyTypeObject DefaultPyType => PyRangeObjectType.Shared;

    private readonly BigInteger _start, _stop, _step;

    private PyRangeObject(BigInteger start, BigInteger stop, BigInteger step)
    {
        Debug.Assert(step != 0);

        _start = start;
        _stop = stop;
        _step = step;
    }

    public BigInteger Start => _start;
    public BigInteger Stop => _stop;
    public BigInteger Step => _step;

    private IEnumerable<PyIntObject> EnumeratePositiveStepRange()
    {
        Debug.Assert(_step > 0);
        for (BigInteger i = _start; i < _stop; i += _step)
            yield return PyIntObject.FromInteger(i);
    }
    private IEnumerable<PyIntObject> EnumerateNegativeStepRange()
    {
        Debug.Assert(_step < 0);
        for (BigInteger i = _start; i > _stop; i += _step)
            yield return PyIntObject.FromInteger(i);
    }

    internal IEnumerable<PyIntObject> EnumerateRange()
    {
        return _step > 0 ? EnumeratePositiveStepRange() : EnumerateNegativeStepRange();
    }

    public static PyRangeObject CreateRange(BigInteger stop)
    {
        return new PyRangeObject(0, stop, 1);
    }
    public static PyRangeObject CreateRange(BigInteger start, BigInteger stop, BigInteger step)
    {
        return new PyRangeObject(start, stop, step);
    }

    protected internal override PyObject? ReprImpl()
    {
        if (_step == 1)
            return PyStrObject.FromString($"range({_start}, {_stop})");

        return PyStrObject.FromString($"range({_start}, {_stop}, {_step})");
    }

    protected internal override PyObject? IterImpl()
    {
        return new PyRangeIteratorObject(EnumerateRange());
    }
}

public sealed class PyRangeObjectType : PyPrimitiveTypeObject<PyRangeObjectType, PyRangeObject>
{
    public override string Name => "range";

    protected internal override PyObject? NewImpl(PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        if (kwargs.Count is not 0)
            return PyVirtualMachine.RaiseTypeError(null);

        if (args.Count is 1)
        {
            if (args[0] is not PyIntObject stopObj)
                return PyVirtualMachine.RaiseTypeError(null);

            return PyRangeObject.CreateRange(stopObj.Value);
        }
        else if (args.Count is 2)
        {
            if (args[0] is not PyIntObject startObj)
                return PyVirtualMachine.RaiseTypeError(null);

            if (args[1] is not PyIntObject stopObj)
                return PyVirtualMachine.RaiseTypeError(null);

            return PyRangeObject.CreateRange(startObj.Value, stopObj.Value, BigInteger.One);
        }
        else if (args.Count is 3)
        {
            if (args[0] is not PyIntObject startObj)
                return PyVirtualMachine.RaiseTypeError(null);

            if (args[1] is not PyIntObject stopObj)
                return PyVirtualMachine.RaiseTypeError(null);

            if (args[2] is not PyIntObject stepObj)
                return PyVirtualMachine.RaiseTypeError(null);

            if (stepObj.Value.IsZero)
                return PyVirtualMachine.RaiseValueError("range() arg 3 must not be zero");

            return PyRangeObject.CreateRange(startObj.Value, stopObj.Value, stepObj.Value);
        }

        return PyVirtualMachine.RaiseTypeError(null);
    }
}
