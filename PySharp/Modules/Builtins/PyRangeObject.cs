using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.Comparison;
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

[PyType("range", IsSealed = true)]
public sealed partial class PyRangeObjectType : PyTypeObject<PyRangeObject>
{

    protected override PyResult Repr(PyCallContext context, PyRangeObject self)
    {
        if (self.Step == 1)
            return PyStrObject.FromString($"range({self.Start}, {self.Stop})");
        return PyStrObject.FromString($"range({self.Start}, {self.Stop}, {self.Step})");
    }

    // CPython range_equals: lengths first, then an empty range equals any
    // empty range, then start, then a single-element range ignores the step
    // (it cannot affect the value), then step.
    internal static bool RangeEquals(PyRangeObject left, PyRangeObject right)
    {
        if (ReferenceEquals(left, right))
            return true;
        if (left.RangeLen != right.RangeLen)
            return false;
        if (left.RangeLen.IsZero)
            return true;
        if (left.Start != right.Start)
            return false;
        return left.RangeLen.IsOne || left.Step == right.Step;
    }

    protected override PyResult Eq(PyCallContext context, PyRangeObject self, PyObject other)
    {
        if (other is not PyRangeObject otherRange)
            return PyNotImplementedObject.NotImplemented;
        return PyBoolObject.FromBoolean(RangeEquals(self, otherRange));
    }

    protected override PyResult Hash(PyCallContext context, PyRangeObject self)
    {
        // CPython range_hash: hashes (length, start, step) as a tuple, with
        // None substituting the parts that cannot differ when the length is
        // 0 (everything) or 1 (the step).
        PyObject start = self.RangeLen.IsZero ? PyNoneObject.None : PyIntObject.FromInteger(self.Start);
        PyObject step = self.RangeLen.IsZero || self.RangeLen.IsOne ? PyNoneObject.None : PyIntObject.FromInteger(self.Step);
        var tuple = PyTupleObject.CreateTuple(PyIntObject.FromInteger(self.RangeLen), start, step);
        return PySpecialMethods.Hash(context, tuple);
    }

    protected override PyResult Iter(PyCallContext context, PyRangeObject self)
    {
        if (!self._isLong)
            return new PyRangeIteratorObject((long)self.Start, (long)self.Step, (long)self.RangeLen);

        return new PyLongRangeIteratorObject(self.Start, self.Step, self.RangeLen);
    }

    // CPython range exposes an explicit __bool__ (unlike list/str, whose
    // truthiness comes from the dispatch-layer __len__ fallback).
    protected override PyResult Bool(PyCallContext context, PyRangeObject self)
    {
        return PyBoolObject.FromBoolean(self.RangeLen > 0);
    }

    protected override PyResult Len(PyCallContext context, PyRangeObject self)
    {
        return PyIntObject.FromInteger(self.RangeLen);
    }

    protected override PyResult Contains(PyCallContext context, PyRangeObject self, PyObject item)
    {
        // CPython range_contains: int (and bool) operands take the O(1)
        // arithmetic test — x is a member iff (x - start) is a multiple of
        // step and the index lands inside [0, len).
        if (item is PyIntObject intObj)
        {
            var offset = intObj.Value - self.Start;
            var (index, remainder) = BigInteger.DivRem(offset, self.Step);
            return PyBoolObject.FromBoolean(remainder.IsZero && index.Sign >= 0 && index < self.RangeLen);
        }

        // Non-int operands keep the generic element-enumeration semantics
        // (there is no base Contains implementation to fall back to).
        var iter = PySpecialMethods.Iter(context, self);
        if (iter.IsError)
            return iter;

        var element = PySpecialMethods.Next(context, iter.Value);
        while (!element.IsStopIteration)
        {
            if (element.IsError)
                return element;

            // CPython range_contains falls back to _PySequence_IterSearch,
            // which compares through PyObject_RichCompareBool — the identity
            // shortcut applies
            var eq = PyComparer.Eq(context, element.Value, item);
            if (eq.IsError)
                return eq;

            if (eq.Value.BoolValue)
                return PyBoolObject.True;

            element = PySpecialMethods.Next(context, iter.Value);
        }

        return PyBoolObject.False;
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
            var indicesResult = slice.Indices(context, self.RangeLen, out var indices);
            if (indicesResult.IsError)
                return indicesResult;
            var (sStart, sStop, sStep, _) = indices;
            // CPython compute_slice computes substart/substop from the slice
            // indices unconditionally and passes them to range_new, so an
            // empty or reversed slice still reports the computed bounds
            // (the length simply comes out zero) — the bounds are never
            // collapsed to (0, 0)
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
        // range_new takes up to three positional arguments and no keywords,
        // each one converted through __index__
        if (kwargs.Count is not 0)
            return PyResult.TypeError(PySR.Runtime_Range_TakesNoKeywordArguments);

        if (args.Count is 0)
            return PyResult.TypeError(PySR.Runtime_Range_ExpectedAtLeastArgument, args.Count);

        if (args.Count > 3)
            return PyResult.TypeError(PySR.Runtime_Range_ExpectedAtMostArguments, args.Count);

        var start = PySpecialMethods.Index(context, args[0]);
        if (start.IsError)
            return start;

        if (args.Count is 1)
            return PyRangeObject.CreateRange(start.Value.Value);

        var stop = PySpecialMethods.Index(context, args[1]);
        if (stop.IsError)
            return stop;

        if (args.Count is 2)
            return PyRangeObject.CreateRange(start.Value.Value, stop.Value.Value, BigInteger.One);

        var step = PySpecialMethods.Index(context, args[2]);
        if (step.IsError)
            return step;

        if (step.Value.Value.IsZero)
            return PyResult.ValueError(PySR.Runtime_Range_Arg3Zero);

        return PyRangeObject.CreateRange(start.Value.Value, stop.Value.Value, step.Value.Value);
    }
}
