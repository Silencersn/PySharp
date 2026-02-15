using PySharp.Runtime.Calls;
using System.Numerics;

namespace PySharp.Modules.Builtins;

public class PyRangeIteratorObject : PyObject
{
    internal long _start, _step, _len;
    public override PyTypeObject DefaultPyType => PyRangeIteratorObjectType.Shared;
    internal PyRangeIteratorObject(long start, long step, long len)
    {
        _start = start;
        _step = step;
        _len = len;
    }
}

public sealed class PyRangeIteratorObjectType : PyTypeObject<PyRangeIteratorObjectType, PyRangeIteratorObject>
{
    public override string Module => "builtins";
    public override string Name => "range_iterator";

    protected override PyResult Iter(PyCallContext context, PyRangeIteratorObject self)
    {
        return self;
    }

    protected override PyResult Next(PyCallContext context, PyRangeIteratorObject self)
    {
        if (self._len <= 0)
            return PyResult.StopIteration();

        var result = self._start;
        self._start += self._step;
        self._len--;
        return PyIntObject.FromInteger(result);
    }

    protected override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        return PyResult.TypeError(null);
    }
}

public class PyLongRangeIteratorObject : PyObject
{
    internal BigInteger _start, _step, _len;
    public override PyTypeObject DefaultPyType => PyLongRangeIteratorObjectType.Shared;
    internal PyLongRangeIteratorObject(BigInteger start, BigInteger step, BigInteger len)
    {
        _start = start;
        _step = step;
        _len = len;
    }
}

public sealed class PyLongRangeIteratorObjectType : PyTypeObject<PyLongRangeIteratorObjectType, PyLongRangeIteratorObject>
{
    public override string Module => "builtins";
    public override string Name => "longrange_iterator";

    protected override PyResult Iter(PyCallContext context, PyLongRangeIteratorObject self)
    {
        return self;
    }

    protected override PyResult Next(PyCallContext context, PyLongRangeIteratorObject self)
    {
        if (self._len <= 0)
            return PyResult.StopIteration();

        var result = self._start;
        self._start += self._step;
        self._len--;
        return PyIntObject.FromInteger(result);
    }

    protected override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        return PyResult.TypeError(null);
    }
}