using PySharp.Modules.Builtins;
using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;

namespace PySharp.Modules.Threading;

/// <summary>
/// threading.Lock — the same object as CPython's <c>_thread.LockType</c>
/// (<c>locktype</c>): a non-reentrant mutex whose blocking waits the
/// environment shutdown can still interrupt.
/// </summary>
public sealed partial class PyLockObject : PyObject, IDisposable
{
    // SemaphoreSlim is non-reentrant, matching the CPython lock: a thread
    // that re-acquires blocks on itself. Waits on it are interruptible by
    // Thread.Interrupt, which the environment teardown relies on.
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public override PyTypeObject DefaultPyType => PyLockObjectType.Shared;

    internal bool TryAcquire(double timeoutSeconds, bool wait)
    {
        if (!wait)
            return _semaphore.Wait(0);
        if (timeoutSeconds < 0)
            return _semaphore.Wait(Timeout.Infinite);
        return _semaphore.Wait(ToMilliseconds(timeoutSeconds));
    }

    internal void PyRelease()
    {
        _semaphore.Release();
    }

    // host-side deterministic release, the PyQueueObject pattern; Python
    // code never disposes a lock, so the semaphore otherwise lives with
    // the process
    public void Dispose()
    {
        _semaphore.Dispose();
    }

    internal bool IsLocked => _semaphore.CurrentCount is 0;

    // CPython rounds timeouts up to whole milliseconds (ROUND_TIMEOUT) so a
    // granted wait is never shorter than requested; zero stays an immediate
    // probe
    internal static int ToMilliseconds(double seconds)
    {
        if (seconds <= 0)
            return 0;
        return (int)Math.Ceiling(seconds * 1000);
    }
}

[PyType("lock", Module = "_thread")]
public sealed partial class PyLockObjectType : PyTypeObject<PyLockObject>
{
    [PyExport(PySpecialNames.New, nameof(NewImpl))]
    private static partial PyBuiltinFunctionOrMethodObject _new { get; }

    [PyFunctionParameters()]
    private static PyResult NewImpl(PyCallContext context, PyArguments arguments)
    {
        return new PyLockObject();
    }

    protected override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        var obj = _new.Call(context, args, kwargs);
        if (obj.IsError)
            return obj;
        obj.Value._pyType = cls;
        return obj;
    }

    protected override PyResult Repr(PyCallContext context, PyLockObject self)
    {
        var state = self.IsLocked ? "locked" : "unlocked";
        return PyStrObject.FromString($"<{state} _thread.lock object at 0x{self.PyId:X16}>");
    }

    [PyMethod("acquire")]
    [PyFunctionParameters("blocking=True", "timeout=-1")]
    private static PyResult Acquire(PyCallContext context, PyLockObject self, PyArguments arguments)
    {
        double timeoutSeconds = -1;
        if (TryParseTimeout(context, arguments, ref timeoutSeconds) is { } error)
            return error;

        var blocking = PySpecialMethods.Bool(context, arguments[0]);
        if (blocking.IsError)
            return blocking;
        if (!blocking.Value.BoolValue && timeoutSeconds is not -1)
            return PyResult.RaiseException(PyValueErrorObjectType.Shared, PySR.Runtime_Threading_TimeoutForNonBlocking);

        return PyBoolObject.FromBoolean(self.TryAcquire(timeoutSeconds, blocking.Value.BoolValue));
    }

    // CPython's contract: a timeout of exactly -1 is the "wait forever"
    // sentinel (-1.0 compares equal), any other negative value is
    // rejected, and None is a plain conversion TypeError. Returns null on
    // success, the error result otherwise.
    internal static PyResult? TryParseTimeout(PyCallContext context, PyArguments arguments, ref double timeoutSeconds)
    {
        if (arguments[1] is PyNoneObject)
            return PyResult.TypeError(PySR.Runtime_Threading_TimeoutMustBeNumber, "NoneType");

        var converted = PySpecialMethods.Float(context, arguments[1]);
        if (converted.IsError)
            return converted;

        timeoutSeconds = converted.Value.Value;
        if (timeoutSeconds < 0 && timeoutSeconds is not -1)
            return PyResult.RaiseException(PyValueErrorObjectType.Shared, PySR.Runtime_Threading_TimeoutNonNegative);

        return ValidateTimeoutRange(context, timeoutSeconds);
    }

    // CPython converts a timeout to a nanosecond PyTime_t and then, on
    // Windows, to the WaitForSingleObject millisecond range: a value past
    // the 64-bit nanosecond range (~292 years) or past 0xFFFFFFFE
    // milliseconds (~49.7 days, the source of _thread.TIMEOUT_MAX) fails
    // with OverflowError before any waiting starts. Returns null on
    // success, the error result otherwise.
    internal static PyResult? ValidateTimeoutRange(PyCallContext context, double timeoutSeconds)
    {
        if (timeoutSeconds > 9_223_372_036.854)
            return PyResult.RaiseException(PyOverflowErrorObjectType.Shared, PySR.Runtime_Threading_TimeStampOutOfRange);
        if (Math.Ceiling(timeoutSeconds * 1e6) > 4_294_967_294_000.0)
            return PyResult.RaiseException(PyOverflowErrorObjectType.Shared, PySR.Runtime_Threading_TimeoutTooLarge);
        return null;
    }

    [PyMethod("release")]
    [PyFunctionParameters()]
    private static PyResult Release(PyCallContext context, PyLockObject self, PyArguments arguments)
    {
        // SemaphoreFullException is the .NET face of "release unlocked lock"
        try
        {
            self.PyRelease();
        }
        catch (SemaphoreFullException)
        {
            return PyResult.RaiseException(PyRuntimeErrorObjectType.Shared, PySR.Runtime_Threading_ReleaseUnlockedLock);
        }
        return PyNoneObject.None;
    }

    [PyMethod("locked")]
    [PyFunctionParameters()]
    private static PyResult Locked(PyCallContext context, PyLockObject self, PyArguments arguments)
    {
        return PyBoolObject.FromBoolean(self.IsLocked);
    }

    protected override PyResult Enter(PyCallContext context, PyLockObject self)
    {
        self.TryAcquire(-1, true);
        return self;
    }

    protected override PyResult Exit(PyCallContext context, PyLockObject self, PyObject excType, PyObject excVal, PyObject excTb)
    {
        return Release(context, self, PyArguments.Empty);
    }
}

/// <summary>
/// threading.RLock — CPython's <c>_thread.RLock</c> C implementation: a
/// reentrant mutex tracking owner id and recursion count, plus the
/// condition-variable helpers (<c>_release_save</c> family) CPython's
/// Condition protocol probes for.
/// </summary>
public sealed partial class PyRLockObject : PyObject
{
    // The gate is the monitor itself: the owner holds it between the first
    // acquire and the final release, and re-entrant acquires never touch
    // it (the count is owner-exclusive state, mutated only by the owner).
    private readonly object _gate = new();
    private int _ownerId;
    private int _count;

    public override PyTypeObject DefaultPyType => PyRLockObjectType.Shared;

    internal int OwnerId => _ownerId;
    internal int Count => _count;

    internal bool TryAcquire(double timeoutSeconds, bool wait)
    {
        var current = Environment.CurrentManagedThreadId;
        if (_ownerId == current)
        {
            _count++;
            return true;
        }

        var milliseconds = wait
            ? timeoutSeconds < 0 ? Timeout.Infinite : PyLockObject.ToMilliseconds(timeoutSeconds)
            : 0;
        if (!Monitor.TryEnter(_gate, milliseconds))
            return false;

        _ownerId = current;
        _count = 1;
        return true;
    }

    internal bool TryRelease()
    {
        if (_ownerId != Environment.CurrentManagedThreadId || _count is 0)
            return false;

        _count--;
        if (_count is 0)
        {
            _ownerId = 0;
            Monitor.Exit(_gate);
        }
        return true;
    }

    internal bool PyIsOwned()
    {
        return _ownerId == Environment.CurrentManagedThreadId && _count > 0;
    }

    // The Condition._release_save protocol: fully drop the monitor and
    // hand the recursion state back for _acquire_restore. Caller owns it.
    internal (int Count, int Owner) ReleaseSave()
    {
        var saved = (_count, _ownerId);
        _count = 0;
        _ownerId = 0;
        Monitor.Exit(_gate);
        return saved;
    }

    internal void AcquireRestore((int Count, int Owner) state)
    {
        Monitor.Enter(_gate);
        _count = state.Count;
        _ownerId = state.Owner;
    }
}

[PyType("RLock", Module = "_thread")]
public sealed partial class PyRLockObjectType : PyTypeObject<PyRLockObject>
{
    [PyExport(PySpecialNames.New, nameof(NewImpl))]
    private static partial PyBuiltinFunctionOrMethodObject _new { get; }

    [PyFunctionParameters()]
    private static PyResult NewImpl(PyCallContext context, PyArguments arguments)
    {
        return new PyRLockObject();
    }

    protected override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        var obj = _new.Call(context, args, kwargs);
        if (obj.IsError)
            return obj;
        obj.Value._pyType = cls;
        return obj;
    }

    protected override PyResult Repr(PyCallContext context, PyRLockObject self)
    {
        var state = self.Count > 0 ? "locked" : "unlocked";
        return PyStrObject.FromString(
            $"<{state} _thread.RLock object owner={self.OwnerId} count={self.Count} at 0x{self.PyId:X16}>");
    }

    [PyMethod("acquire")]
    [PyFunctionParameters("blocking=True", "timeout=-1")]
    private static PyResult Acquire(PyCallContext context, PyRLockObject self, PyArguments arguments)
    {
        double timeoutSeconds = -1;
        if (PyLockObjectType.TryParseTimeout(context, arguments, ref timeoutSeconds) is { } error)
            return error;

        var blocking = PySpecialMethods.Bool(context, arguments[0]);
        if (blocking.IsError)
            return blocking;
        if (!blocking.Value.BoolValue && timeoutSeconds is not -1)
            return PyResult.RaiseException(PyValueErrorObjectType.Shared, PySR.Runtime_Threading_TimeoutForNonBlocking);

        return PyBoolObject.FromBoolean(self.TryAcquire(timeoutSeconds, blocking.Value.BoolValue));
    }

    [PyMethod("release")]
    [PyFunctionParameters()]
    private static PyResult Release(PyCallContext context, PyRLockObject self, PyArguments arguments)
    {
        if (!self.TryRelease())
            return PyResult.RaiseException(PyRuntimeErrorObjectType.Shared, PySR.Runtime_Threading_ReleaseUnacquiredLock);
        return PyNoneObject.None;
    }

    [PyMethod("locked")]
    [PyFunctionParameters()]
    private static PyResult Locked(PyCallContext context, PyRLockObject self, PyArguments arguments)
    {
        return PyBoolObject.FromBoolean(self.Count > 0);
    }

    [PyMethod("_release_save")]
    [PyFunctionParameters()]
    private static PyResult ReleaseSave(PyCallContext context, PyRLockObject self, PyArguments arguments)
    {
        if (self.Count is 0)
            return PyResult.RaiseException(PyRuntimeErrorObjectType.Shared, PySR.Runtime_Threading_ReleaseUnacquiredLock);
        var (count, owner) = self.ReleaseSave();
        return PyTupleObject.CreateTuple(PyIntObject.FromInteger(count), PyIntObject.FromInteger(owner));
    }

    [PyMethod("_acquire_restore")]
    [PyFunctionParameters("state")]
    private static PyResult AcquireRestore(PyCallContext context, PyRLockObject self, PyArguments arguments)
    {
        if (arguments[0] is not PyTupleObject { Count: 2 } state ||
            state[0] is not PyIntObject count || state[1] is not PyIntObject owner)
            return PyResult.TypeError(PySR.Runtime_Threading_InvalidRLockState);

        self.AcquireRestore((count.Int32Value, owner.Int32Value));
        return PyNoneObject.None;
    }

    [PyMethod("_is_owned")]
    [PyFunctionParameters()]
    private static PyResult IsOwned(PyCallContext context, PyRLockObject self, PyArguments arguments)
    {
        return PyBoolObject.FromBoolean(self.PyIsOwned());
    }

    [PyMethod("_recursion_count")]
    [PyFunctionParameters()]
    private static PyResult RecursionCount(PyCallContext context, PyRLockObject self, PyArguments arguments)
    {
        return PyIntObject.FromInteger(self.PyIsOwned() ? self.Count : 0);
    }

    protected override PyResult Enter(PyCallContext context, PyRLockObject self)
    {
        self.TryAcquire(-1, true);
        return self;
    }

    protected override PyResult Exit(PyCallContext context, PyRLockObject self, PyObject excType, PyObject excVal, PyObject excTb)
    {
        return Release(context, self, PyArguments.Empty);
    }
}
