using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;
using System.Diagnostics;
using System.Numerics;

namespace PySharp.Modules.Builtins;

public class PyRangeObject : PyObject
{
    public override PyTypeObject DefaultPyType => PyRangeObjectType.Shared;

    internal readonly BigInteger _start, _stop, _step, _len;
    internal readonly bool _isLong;

    private PyRangeObject(BigInteger start, BigInteger stop, BigInteger step)
    {
        Debug.Assert(step != 0);
        _start = start;
        _stop = stop;
        _step = step;
        if (step > 0 && start < stop)
            _len = (stop - start + step - 1) / step;
        else if (step < 0 && start > stop)
            _len = (start - stop - step - 1) / -step;

        if (_start.GetByteCount() > 8 || _stop.GetByteCount() > 8 || _len.GetByteCount() > 8)
            _isLong = true;
    }

    public BigInteger Start => _start;
    public BigInteger Stop => _stop;
    public BigInteger Step => _step;
    public BigInteger RangeLen => _len;

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
        ArgumentOutOfRangeException.ThrowIfZero(step);
        return new PyRangeObject(start, stop, step);
    }
}

[PyType("range")]
public sealed partial class PyRangeObjectType : PyTypeObject<PyRangeObject>
{

    protected override PyResult Repr(PyCallContext context, PyRangeObject self)
    {
        if (self.Step == 1)
            return PyStrObject.FromString($"range({self.Start}, {self.Stop})");
        return PyStrObject.FromString($"range({self.Start}, {self.Stop}, {self.Step})");
    }

    protected override PyResult Iter(PyCallContext context, PyRangeObject self)
    {
        if (!self._isLong)
            return new PyRangeIteratorObject((long)self.Start, (long)self.Step, (long)self.RangeLen);

        return new PyLongRangeIteratorObject(self.Start, self.Step, self.RangeLen);
    }

    protected override PyResult Len(PyCallContext context, PyRangeObject self)
    {
        return PyIntObject.FromInteger(self.RangeLen);
    }

    protected override PyResult Reversed(PyCallContext context, PyRangeObject self)
    {
        // reversed(range(start, stop, step)) = range(start + (len-1)*step, start - step, -step)
        BigInteger newStart = self.Start + (self.RangeLen - 1) * self.Step;
        BigInteger newStop = self.Start - self.Step;
        BigInteger newStep = -self.Step;
        return PyRangeObject.CreateRange(newStart, newStop, newStep);
    }

    protected override PyResult GetItem(PyCallContext context, PyRangeObject self, PyObject item)
    {
        if (item is PySliceObject slice)
        {
            var (sStart, sStop, sStep, sLength) = slice.Indices((int)self.RangeLen);
            if (sLength is 0)
                return PyRangeObject.CreateRange(self.Start, self.Start, self.Step);

            BigInteger newStart = self.Start + sStart * self.Step;
            BigInteger newStop = self.Start + sStop * self.Step;
            BigInteger newStep = self.Step * sStep;
            return PyRangeObject.CreateRange(newStart, newStop, newStep);
        }

        var idxResult = PySpecialMethods.Index(context, item);
        if (idxResult.IsError)
            return idxResult;

        var idx = idxResult.Value.Value;
        if (idx < 0)
        {
            if (-idx > self.RangeLen)
                return PyResult.IndexError("range object index out of range");
            idx += self.RangeLen;
        }

        if (idx < 0 || idx >= self.RangeLen)
            return PyResult.IndexError("range object index out of range");

        BigInteger value = self.Start + idx * self.Step;
        return PyIntObject.FromInteger(value);
    }

    protected override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        if (kwargs.Count is not 0)
            return PyResult.TypeError(null);
        if (args.Count is 1)
        {
            if (args[0] is not PyIntObject stopObj)
                return PyResult.TypeError(null);
            return PyRangeObject.CreateRange(stopObj.Value);
        }
        else if (args.Count is 2)
        {
            if (args[0] is not PyIntObject startObj)
                return PyResult.TypeError(null);
            if (args[1] is not PyIntObject stopObj)
                return PyResult.TypeError(null);
            return PyRangeObject.CreateRange(startObj.Value, stopObj.Value, BigInteger.One);
        }
        else if (args.Count is 3)
        {
            if (args[0] is not PyIntObject startObj)
                return PyResult.TypeError(null);
            if (args[1] is not PyIntObject stopObj)
                return PyResult.TypeError(null);
            if (args[2] is not PyIntObject stepObj)
                return PyResult.TypeError(null);
            if (stepObj.Value.IsZero)
                return PyResult.ValueError(PySR.Runtime_Range_Arg3Zero);
            return PyRangeObject.CreateRange(startObj.Value, stopObj.Value, stepObj.Value);
        }
        return PyResult.TypeError(null);
    }
}
