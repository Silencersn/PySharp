using PySharp.Modules.Builtins;
using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;
using System.Diagnostics;

namespace PySharp.Modules.Threading;

/// <summary>
/// threading.Condition — a condition variable over any lock-like object
/// (Lock, RLock, or a duck-typed substitute). The CPython implementation
/// probes the lock for the _release_save / _acquire_restore / _is_owned
/// extension points and falls back to plain acquire/release otherwise;
/// this port follows the same protocol through dynamic method calls.
/// </summary>
public sealed partial class PyConditionObject : PyObject
{
    internal readonly PyObject _lock;
    // whether the lock carries the RLock extension points, probed once at
    // construction exactly like threading.py's hasattr checks
    private readonly bool _hasReleaseSave;
    private readonly bool _hasAcquireRestore;
    private readonly bool _hasIsOwned;
    // waiter locks handed out by notify; every mutation happens while the
    // condition's own lock is held, like CPython's deque discipline
    internal readonly List<PyLockObject> _waiters = [];

    public override PyTypeObject DefaultPyType => PyConditionObjectType.Shared;

    internal PyConditionObject(PyObject lockObject, PyCallContext context)
    {
        _lock = lockObject;
        _hasReleaseSave = HasAttribute(context, lockObject, "_release_save");
        _hasAcquireRestore = HasAttribute(context, lockObject, "_acquire_restore");
        _hasIsOwned = HasAttribute(context, lockObject, "_is_owned");
    }

    private static bool HasAttribute(PyCallContext context, PyObject obj, string name)
    {
        var result = PyTypeObject.DefaultGetAttribute(context, obj, PyStrObject.FromString(name));
        return !result.IsError;
    }

    internal PyBoolOrError IsOwned(PyCallContext context)
    {
        if (_hasIsOwned)
        {
            var owned = _lock.CallMethod(context, "_is_owned");
            if (owned.IsError)
                return PyBoolOrError.OfError(owned.Exception);
            return BoolResult(context, owned.Value);
        }

        // threading.py's fallback: try a non-blocking acquire — a miss
        // proves the current thread does not own the lock
        var probe = _lock.CallMethod(context, "acquire", [PyBoolObject.False]);
        if (probe.IsError)
            return PyBoolOrError.OfError(probe.Exception);
        var acquired = BoolResult(context, probe.Value);
        if (acquired.IsError)
            return acquired;
        if (acquired.Value)
        {
            var release = _lock.CallMethod(context, "release");
            if (release.IsError)
                return PyBoolOrError.OfError(release.Exception);
            return new PyBoolOrError(false, null);
        }
        return new PyBoolOrError(true, null);
    }

    internal PyResult ReleaseSave(PyCallContext context)
    {
        if (_hasReleaseSave)
            return _lock.CallMethod(context, "_release_save");
        return _lock.CallMethod(context, "release");
    }

    internal PyResult AcquireRestore(PyCallContext context, PyObject state)
    {
        if (_hasAcquireRestore)
            return _lock.CallMethod(context, "_acquire_restore", [state]);
        return _lock.CallMethod(context, "acquire");
    }

    // truthiness probe that surfaces a __bool__ failure as an error result
    internal static PyBoolOrError BoolResult(PyCallContext context, PyObject value)
    {
        var result = PySpecialMethods.Bool(context, value);
        if (result.IsError)
            return PyBoolOrError.OfError(result.Exception);
        return new PyBoolOrError(result.Value.BoolValue, null);
    }
}

// A boolean probe whose truthiness evaluation can itself fail (__bool__
// raising); PyResult<T> is constrained to PyObject so this carries the
// error channel itself
internal readonly record struct PyBoolOrError(bool Value, PyExceptionObject? Error)
{
    public bool IsError => Error is not null;

    public static PyBoolOrError OfError(PyExceptionObject exception) => new(default, exception);
}

[PyType("Condition", Module = "threading")]
public sealed partial class PyConditionObjectType : PyTypeObject<PyConditionObject>
{
    [PyExport(PySpecialNames.New, nameof(NewImpl))]
    private static partial PyBuiltinFunctionOrMethodObject _new { get; }

    [PyFunctionParameters("lock=None")]
    private static PyResult NewImpl(PyCallContext context, PyArguments arguments)
    {
        // threading.py defaults to a fresh RLock
        var lockObject = arguments[0] is PyNoneObject ? new PyRLockObject() : arguments[0];
        return new PyConditionObject(lockObject, context);
    }

    protected override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        var obj = _new.Call(context, args, kwargs);
        if (obj.IsError)
            return obj;
        obj.Value._pyType = cls;
        return obj;
    }

    protected override PyResult Repr(PyCallContext context, PyConditionObject self)
    {
        var lockRepr = PySpecialMethods.Repr(context, self._lock);
        if (lockRepr.IsError)
            return lockRepr;
        return PyStrObject.FromString($"<Condition({lockRepr.Value.Value}, {self._waiters.Count})>");
    }

    [PyMethod("acquire")]
    [PyFunctionParameters("blocking=True", "timeout=-1")]
    private static PyResult Acquire(PyCallContext context, PyConditionObject self, PyArguments arguments)
    {
        // forwarded to the underlying lock verbatim, including the
        // lock's own parameter validation
        double timeoutSeconds = -1;
        if (PyLockObjectType.TryParseTimeout(context, arguments, ref timeoutSeconds) is { } error)
            return error;

        return self._lock.CallMethod(context, "acquire", [arguments[0], arguments[1]]);
    }

    [PyMethod("release")]
    [PyFunctionParameters()]
    private static PyResult Release(PyCallContext context, PyConditionObject self, PyArguments arguments)
    {
        return self._lock.CallMethod(context, "release");
    }

    [PyMethod("locked")]
    [PyFunctionParameters()]
    private static PyResult Locked(PyCallContext context, PyConditionObject self, PyArguments arguments)
    {
        return self._lock.CallMethod(context, "locked");
    }

    protected override PyResult Enter(PyCallContext context, PyConditionObject self)
    {
        var result = self._lock.CallMethod(context, "__enter__");
        if (result.IsError)
            return result;
        return self;
    }

    protected override PyResult Exit(PyCallContext context, PyConditionObject self, PyObject excType, PyObject excVal, PyObject excTb)
    {
        return self._lock.CallMethod(context, "__exit__", [excType, excVal, excTb]);
    }

    [PyMethod("wait")]
    [PyFunctionParameters("timeout=None")]
    private static PyResult Wait(PyCallContext context, PyConditionObject self, PyArguments arguments)
    {
        double? timeout = null;
        if (arguments[0] is not PyNoneObject)
        {
            var converted = PySpecialMethods.Float(context, arguments[0]);
            if (converted.IsError)
                return converted;
            timeout = converted.Value.Value;
        }

        return PyWait(context, self, timeout);
    }

    internal static PyResult PyWait(PyCallContext context, PyConditionObject self, double? timeout)
    {
        var owned = self.IsOwned(context);
        if (owned.IsError)
            return PyResult.FromException(owned.Error!);
        if (!owned.Value)
            return PyResult.RaiseException(PyRuntimeErrorObjectType.Shared, PySR.Runtime_Threading_WaitOnUnacquiredLock);

        // the waiter starts life locked; notify's release is the wakeup
        var waiter = new PyLockObject();
        waiter.TryAcquire(-1, true);
        self._waiters.Add(waiter);
        var savedState = self.ReleaseSave(context);
        if (savedState.IsError)
            return savedState;

        var gotit = false;
        Exception? pending = null;
        try
        {
            if (timeout is null)
            {
                waiter.TryAcquire(-1, true);
                gotit = true;
            }
            else if (timeout > 0)
            {
                gotit = waiter.TryAcquire(timeout.Value, true);
            }
            else
            {
                gotit = waiter.TryAcquire(0, false);
            }
        }
        catch (Exception exception)
        {
            pending = exception;
        }

        // threading.py wait's finally discipline: the lock state is
        // restored and an unnotified waiter retired on every path
        var restored = self.AcquireRestore(context, savedState.Value);
        if (!gotit)
            self._waiters.Remove(waiter);

        if (pending is not null)
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw(pending);
        if (restored.IsError)
            return restored;
        return PyBoolObject.FromBoolean(gotit);
    }

    [PyMethod("wait_for")]
    [PyFunctionParameters("predicate", "timeout=None")]
    private static PyResult WaitFor(PyCallContext context, PyConditionObject self, PyArguments arguments)
    {
        double? timeout = null;
        if (arguments[1] is not PyNoneObject)
        {
            var converted = PySpecialMethods.Float(context, arguments[1]);
            if (converted.IsError)
                return converted;
            timeout = converted.Value.Value;
        }

        // threading.py: the predicate runs first; a timeout only ever
        // shrinks the remaining budget across wait rounds
        var result = arguments[0].Call(context);
        if (result.IsError)
            return result;

        Stopwatch? clock = null;
        var deadline = 0.0;
        while (true)
        {
            var truthy = PySpecialMethods.Bool(context, result.Value);
            if (truthy.IsError)
                return truthy;
            if (truthy.Value.BoolValue)
                break;

            double? waittime = timeout;
            if (timeout is not null)
            {
                clock ??= Stopwatch.StartNew();
                if (deadline is 0.0)
                    deadline = clock.Elapsed.TotalSeconds + timeout.Value;
                else
                    waittime = deadline - clock.Elapsed.TotalSeconds;
                if (waittime <= 0)
                    break;
            }

            var waitResult = PyWait(context, self, waittime);
            if (waitResult.IsError)
                return waitResult;

            result = arguments[0].Call(context);
            if (result.IsError)
                return result;
        }
        return result;
    }

    [PyMethod("notify")]
    [PyFunctionParameters("n=1")]
    private static PyResult Notify(PyCallContext context, PyConditionObject self, PyArguments arguments)
    {
        var countResult = PySpecialMethods.Index(context, arguments[0]);
        if (countResult.IsError)
            return countResult;
        return PyNotify(context, self, countResult.Value.Int32Value);
    }

    internal static PyResult PyNotify(PyCallContext context, PyConditionObject self, int n)
    {
        var owned = self.IsOwned(context);
        if (owned.IsError)
            return PyResult.FromException(owned.Error!);
        if (!owned.Value)
            return PyResult.RaiseException(PyRuntimeErrorObjectType.Shared, PySR.Runtime_Threading_NotifyOnUnacquiredLock);

        // gh-92530 shape: a waiter whose release already ran (an
        // interrupted notify) is skipped without consuming the budget
        while (self._waiters.Count > 0 && n > 0)
        {
            var waiter = self._waiters[0];
            if (waiter.IsLocked)
            {
                waiter.PyRelease();
                n--;
            }
            self._waiters.RemoveAt(0);
        }
        return PyNoneObject.None;
    }

    [PyMethod("notify_all")]
    [PyFunctionParameters()]
    private static PyResult NotifyAll(PyCallContext context, PyConditionObject self, PyArguments arguments)
    {
        return PyNotify(context, self, self._waiters.Count);
    }

    [PyMethod("notifyAll")]
    [PyFunctionParameters()]
    private static PyResult NotifyAllAlias(PyCallContext context, PyConditionObject self, PyArguments arguments)
    {
        var warnResult = context.Warn(PyDeprecationWarningObjectType.Shared, PySR.Runtime_Threading_DeprecatedNotifyAll);
        if (warnResult.IsError)
            return warnResult;
        return NotifyAll(context, self, arguments);
    }

    // The protocol surfaces CPython's Condition defines (threading.py
    // assigns the lock's own implementations over these as instance
    // attributes when it has them; the probes inside this class pick the
    // same branch either way)

    [PyMethod("_is_owned")]
    [PyFunctionParameters()]
    private static PyResult IsOwnedMethod(PyCallContext context, PyConditionObject self, PyArguments arguments)
    {
        var owned = self.IsOwned(context);
        if (owned.IsError)
            return PyResult.FromException(owned.Error!);
        return PyBoolObject.FromBoolean(owned.Value);
    }

    [PyMethod("_release_save")]
    [PyFunctionParameters()]
    private static PyResult ReleaseSaveMethod(PyCallContext context, PyConditionObject self, PyArguments arguments)
    {
        return self.ReleaseSave(context);
    }

    [PyMethod("_acquire_restore")]
    [PyFunctionParameters("x")]
    private static PyResult AcquireRestoreMethod(PyCallContext context, PyConditionObject self, PyArguments arguments)
    {
        return self.AcquireRestore(context, arguments[0]);
    }
}
