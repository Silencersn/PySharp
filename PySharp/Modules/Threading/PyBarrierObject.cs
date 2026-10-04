using PySharp.Modules.Builtins;
using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;

namespace PySharp.Modules.Threading;

/// <summary>
/// threading.Barrier — the cyclic two-phase (filling/draining) barrier of
/// threading.py, with the resetting and broken sub-states and the
/// optional per-cycle action callback.
/// </summary>
public sealed partial class PyBarrierObject : PyObject, IDisposable
{
    internal readonly PyLockObject _lock;
    internal readonly PyConditionObject _cond;
    internal readonly PyObject _action;
    internal readonly double? _timeout;
    internal readonly int _parties;
    // 0 filling, 1 draining, -1 resetting, -2 broken (threading.py)
    internal int _state;
    internal int _count;

    public override PyTypeObject DefaultPyType => PyBarrierObjectType.Shared;

    internal PyBarrierObject(int parties, PyObject action, double? timeout)
    {
        _lock = new PyLockObject();
        _cond = new PyConditionObject(_lock, PyCallContext.NonContextDependency);
        _action = action;
        _timeout = timeout;
        _parties = parties;
        _state = 0;
        _count = 0;
    }

    // host-side deterministic release over the owned lock, the
    // PyLockObject pattern; Python code never disposes a barrier
    public void Dispose()
    {
        _lock.Dispose();
    }
}

[PyType("Barrier", Module = "threading")]
public sealed partial class PyBarrierObjectType : PyTypeObject<PyBarrierObject>
{
    [PyExport(PySpecialNames.New, nameof(NewImpl))]
    private static partial PyBuiltinFunctionOrMethodObject _new { get; }

    [PyFunctionParameters("parties", "action=None", "timeout=None")]
    private static PyResult NewImpl(PyCallContext context, PyArguments arguments)
    {
        var partiesResult = PySpecialMethods.Index(context, arguments[0]);
        if (partiesResult.IsError)
            return partiesResult;
        var parties = partiesResult.Value.Int32Value;
        if (parties < 1)
            return PyResult.RaiseException(PyValueErrorObjectType.Shared, PySR.Runtime_Threading_BarrierParties);

        double? timeout = null;
        if (arguments[2] is not PyNoneObject)
        {
            var converted = PySpecialMethods.Float(context, arguments[2]);
            if (converted.IsError)
                return converted;
            timeout = converted.Value.Value;
            if (PyLockObjectType.ValidateTimeoutRange(context, timeout.Value) is { } rangeError)
                return rangeError;
        }

        return new PyBarrierObject(parties, arguments[1], timeout);
    }

    protected override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        var obj = _new.Call(context, args, kwargs);
        if (obj.IsError)
            return obj;
        obj.Value._pyType = cls;
        return obj;
    }

    protected override PyResult Repr(PyCallContext context, PyBarrierObject self)
    {
        if (self._state is -2)
            return PyStrObject.FromString($"<{self.PyType.ReprName} at 0x{self.PyId:x}: broken>");
        var waiters = self._state is 0 ? self._count : 0;
        return PyStrObject.FromString(
            $"<{self.PyType.ReprName} at 0x{self.PyId:x}: waiters={waiters}/{self._parties}>");
    }

    [PyMethod("wait")]
    [PyFunctionParameters("timeout=None")]
    private static PyResult Wait(PyCallContext context, PyBarrierObject self, PyArguments arguments)
    {
        // threading.py: an omitted or None timeout falls back to the
        // barrier's own default
        double? timeout = self._timeout;
        if (arguments[0] is not PyNoneObject)
        {
            var converted = PySpecialMethods.Float(context, arguments[0]);
            if (converted.IsError)
                return converted;
            timeout = converted.Value.Value;
            if (PyLockObjectType.ValidateTimeoutRange(context, timeout.Value) is { } rangeError)
                return rangeError;
        }

        return WaitCore(context, self, timeout);
    }

    private static PyResult WaitCore(PyCallContext context, PyBarrierObject self, double? timeout)
    {
        self._lock.TryAcquire(-1, true);
        try
        {
            // block while draining or resetting
            while (self._state is -1 or 1)
            {
                var drained = PyConditionObjectType.PyWait(context, self._cond, null);
                if (drained.IsError)
                    return drained;
            }
            if (self._state < 0)
                return PyResult.RaiseException(PyBrokenBarrierErrorObjectType.Shared);

            var index = self._count;
            self._count += 1;
            try
            {
                if (index + 1 == self._parties)
                {
                    // last thread in: run the action and release everyone
                    var released = ReleaseCycle(context, self);
                    if (released.IsError)
                        throw new PyRuntimeException(context, released.Exception);
                }
                else
                {
                    var waited = WaitCycle(context, self, timeout);
                    if (waited.IsError)
                        throw new PyRuntimeException(context, waited.Exception);
                }
                return PyIntObject.FromInteger(index);
            }
            finally
            {
                self._count -= 1;
                ExitCycle(context, self);
            }
        }
        finally
        {
            self._lock.PyRelease();
        }
    }

    // optionally run the 'action', then flip to draining and wake all
    private static PyResult ReleaseCycle(PyCallContext context, PyBarrierObject self)
    {
        try
        {
            if (self._action is not PyNoneObject)
            {
                var ran = self._action.Call(context);
                if (ran.IsError)
                    throw new PyRuntimeException(context, ran.Exception);
            }
            self._state = 1;
            return PyConditionObjectType.PyNotify(context, self._cond, int.MaxValue);
        }
        catch (PyRuntimeException)
        {
            // an action failure breaks the barrier and re-raises
            BreakBarrier(context, self);
            throw;
        }
    }

    // wait until released; a timeout or a broken state raises
    private static PyResult WaitCycle(PyCallContext context, PyBarrierObject self, double? timeout)
    {
        var predicate = new BarrierStatePredicate(self);
        var waited = predicate.Wait(context, self._cond, timeout);
        if (waited.IsError)
            return PyResult.FromException(waited.Error!);
        if (!waited.Value)
        {
            // timed out: break the barrier
            BreakBarrier(context, self);
            return PyResult.RaiseException(PyBrokenBarrierErrorObjectType.Shared);
        }
        if (self._state < 0)
            return PyResult.RaiseException(PyBrokenBarrierErrorObjectType.Shared);
        return PyNoneObject.None;
    }

    // last thread out flips the cycle back to filling
    private static void ExitCycle(PyCallContext context, PyBarrierObject self)
    {
        if (self._count is 0 && self._state is -1 or 1)
        {
            self._state = 0;
            var notified = PyConditionObjectType.PyNotify(context, self._cond, int.MaxValue);
            if (notified.IsError)
                throw new PyRuntimeException(context, notified.Exception);
        }
    }

    private static void BreakBarrier(PyCallContext context, PyBarrierObject self)
    {
        self._state = -2;
        var notified = PyConditionObjectType.PyNotify(context, self._cond, int.MaxValue);
        if (notified.IsError)
            throw new PyRuntimeException(context, notified.Exception);
    }

    [PyMethod("reset")]
    [PyFunctionParameters()]
    private static PyResult Reset(PyCallContext context, PyBarrierObject self, PyArguments arguments)
    {
        self._lock.TryAcquire(-1, true);
        try
        {
            if (self._count > 0)
            {
                if (self._state is 0 || self._state is -2)
                    self._state = -1;
            }
            else
            {
                self._state = 0;
            }
            return PyConditionObjectType.PyNotify(context, self._cond, int.MaxValue);
        }
        finally
        {
            self._lock.PyRelease();
        }
    }

    [PyMethod("abort")]
    [PyFunctionParameters()]
    private static PyResult Abort(PyCallContext context, PyBarrierObject self, PyArguments arguments)
    {
        self._lock.TryAcquire(-1, true);
        try
        {
            BreakBarrier(context, self);
            return PyNoneObject.None;
        }
        finally
        {
            self._lock.PyRelease();
        }
    }

    [PyProperty("parties")]
    private static PyResult Get_Parties(PyCallContext context, PyBarrierObject self)
    {
        return PyIntObject.FromInteger(self._parties);
    }

    [PyProperty("n_waiting")]
    private static PyResult Get_NWaiting(PyCallContext context, PyBarrierObject self)
    {
        // ephemeral steady-state read (threading.py)
        return PyIntObject.FromInteger(self._state is 0 ? self._count : 0);
    }

    [PyProperty("broken")]
    private static PyResult Get_Broken(PyCallContext context, PyBarrierObject self)
    {
        return PyBoolObject.FromBoolean(self._state is -2);
    }
}

// wait_for(state != 0) as a C# object: each cond.wait wakeup re-checks
// the barrier state under the restored lock
internal sealed class BarrierStatePredicate(PyBarrierObject barrier)
{
    public PyBoolOrError Wait(PyCallContext context, PyConditionObject cond, double? timeout)
    {
        var deadlineSet = false;
        var deadline = 0.0;
        while (barrier._state is 0)
        {
            double? waittime = timeout;
            if (timeout is not null)
            {
                var now = (double)System.Diagnostics.Stopwatch.GetTimestamp() / System.Diagnostics.Stopwatch.Frequency;
                if (!deadlineSet)
                {
                    deadline = now + timeout.Value;
                    deadlineSet = true;
                }
                else
                {
                    waittime = deadline - now;
                }
                if (waittime <= 0)
                    break;
            }
            var waited = PyConditionObjectType.PyWait(context, cond, waittime);
            if (waited.IsError)
                return PyBoolOrError.OfError(waited.Exception);
        }
        return new PyBoolOrError(barrier._state is not 0, null);
    }
}

[PyException("BrokenBarrierError", Module = "threading", Bases = [typeof(PyRuntimeErrorObjectType)])]
public sealed partial class PyBrokenBarrierErrorObjectType : PyExceptionType;
