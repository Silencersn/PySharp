using PySharp.Modules.Builtins;
using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;
using System.Diagnostics;

namespace PySharp.Modules.Threading;

/// <summary>
/// threading.Semaphore — the counter guarding release()/acquire() calls,
/// implemented over the Condition protocol exactly like threading.py
/// (Condition(Lock()) plus a value). BoundedSemaphore subclasses it with
/// a release ceiling.
/// </summary>
public partial class PySemaphoreObject : PyObject, IDisposable
{
    internal readonly PyLockObject _lock;
    internal readonly PyConditionObject _cond;
    internal int _value;

    public override PyTypeObject DefaultPyType => PySemaphoreObjectType.Shared;

    internal PySemaphoreObject(int value)
    {
        _lock = new PyLockObject();
        _cond = new PyConditionObject(_lock, PyCallContext.NonContextDependency);
        _value = value;
    }

    // host-side deterministic release over the owned lock, the
    // PyLockObject pattern; Python code never disposes a semaphore
    public void Dispose()
    {
        _lock.Dispose();
    }

    private static double MonotonicSeconds => (double)Stopwatch.GetTimestamp() / Stopwatch.Frequency;

    internal PyResult TryAcquire(PyCallContext context, bool blocking, double? timeout)
    {
        _lock.TryAcquire(-1, true);

        // threading.py's budget loop: the wall-clock deadline is fixed on
        // the first round and later rounds only wait out the remainder
        var deadline = 0.0;
        var deadlineSet = false;
        while (_value is 0)
        {
            if (!blocking)
                break;
            if (timeout is not null)
            {
                if (!deadlineSet)
                {
                    deadline = MonotonicSeconds + timeout.Value;
                    deadlineSet = true;
                }
                else
                {
                    timeout = deadline - MonotonicSeconds;
                }
                if (timeout <= 0)
                    break;
            }
            var waited = PyConditionObjectType.PyWait(context, _cond, timeout);
            if (waited.IsError)
                return ExitLock(context, waited);
        }

        PyResult result;
        if (_value is not 0)
        {
            _value -= 1;
            result = PyBoolObject.True;
        }
        else
        {
            result = PyBoolObject.False;
        }
        return ExitLock(context, result);
    }

    internal PyResult ExitLock(PyCallContext context, PyResult result)
    {
        _lock.PyRelease();
        return result;
    }

    // release is shared by both semaphore kinds: bump the counter under
    // the condition lock and notify n waiters; the bounded flavor adds
    // its ceiling check first
    internal PyResult PyRelease(PyCallContext context, int n, int ceiling)
    {
        _lock.TryAcquire(-1, true);

        if (_value + n > ceiling)
        {
            return ExitLock(context,
                PyResult.RaiseException(PyValueErrorObjectType.Shared, PySR.Runtime_Threading_SemaphoreReleasedTooMany));
        }

        _value += n;
        var notified = PyConditionObjectType.PyNotify(context, _cond, n);
        if (notified.IsError)
            return ExitLock(context, notified);
        return ExitLock(context, PyNoneObject.None);
    }
}

[PyType("Semaphore", Module = "threading")]
public sealed partial class PySemaphoreObjectType : PyTypeObject<PySemaphoreObject>
{
    [PyExport(PySpecialNames.New, nameof(NewImpl))]
    private static partial PyBuiltinFunctionOrMethodObject _new { get; }

    [PyFunctionParameters("value=1")]
    private static PyResult NewImpl(PyCallContext context, PyArguments arguments)
    {
        var result = PySpecialMethods.Index(context, arguments[0]);
        if (result.IsError)
            return result;
        var value = result.Value.Int32Value;
        if (value < 0)
            return PyResult.RaiseException(PyValueErrorObjectType.Shared, PySR.Runtime_Threading_SemaphoreValueNegative);
        return new PySemaphoreObject(value);
    }

    protected override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        var obj = _new.Call(context, args, kwargs);
        if (obj.IsError)
            return obj;
        obj.Value._pyType = cls;
        return obj;
    }

    protected override PyResult Repr(PyCallContext context, PySemaphoreObject self)
    {
        return PyStrObject.FromString(
            $"<{self.PyType.ReprName} at 0x{self.PyId:x}: value={self._value}>");
    }

    // threading.py keeps the counter as a plain instance attribute; expose it
    // so subclasses and diagnostics read the live value
    [PyProperty("_value")]
    private static PyResult Get_Value(PyCallContext context, PySemaphoreObject self)
    {
        return PyIntObject.FromInteger(self._value);
    }

    [PyProperty("_value", Type = PyPropertyMethodType.Setter)]
    private static PyResult Set_Value(PyCallContext context, PySemaphoreObject self, PyObject value)
    {
        var converted = PySpecialMethods.Index(context, value);
        if (converted.IsError)
            return converted;
        self._value = converted.Value.Int32Value;
        return PyNoneObject.None;
    }

    [PyMethod("acquire")]
    [PyFunctionParameters("blocking=True", "timeout=None")]
    private static PyResult Acquire(PyCallContext context, PySemaphoreObject self, PyArguments arguments)
    {
        var blocking = PySpecialMethods.Bool(context, arguments[0]);
        if (blocking.IsError)
            return blocking;

        double? timeout = null;
        if (arguments[1] is not PyNoneObject)
        {
            var converted = PySpecialMethods.Float(context, arguments[1]);
            if (converted.IsError)
                return converted;
            timeout = converted.Value.Value;
        }
        if (!blocking.Value.BoolValue && timeout is not null)
            return PyResult.RaiseException(PyValueErrorObjectType.Shared, PySR.Runtime_Threading_SemaphoreTimeoutForNonBlocking);

        return self.TryAcquire(context, blocking.Value.BoolValue, timeout);
    }

    [PyMethod("release")]
    [PyFunctionParameters("n=1")]
    private static PyResult Release(PyCallContext context, PySemaphoreObject self, PyArguments arguments)
    {
        var error = ParseReleaseCount(context, arguments, out var n);
        if (error is not null)
            return error.GetValueOrDefault();
        return self.PyRelease(context, n, int.MaxValue);
    }

    internal static PyResult? ParseReleaseCount(PyCallContext context, PyArguments arguments, out int n)
    {
        var countResult = PySpecialMethods.Index(context, arguments[0]);
        if (countResult.IsError)
        {
            n = 0;
            return countResult;
        }
        n = countResult.Value.Int32Value;
        if (n < 1)
        {
            var error = PyValueErrorObjectType.Shared.Create(
                PyStrObject.FromString(PySR.Runtime_Threading_SemaphoreReleaseCount));
            return PyResult.FromException(error);
        }
        return null;
    }

    protected override PyResult Enter(PyCallContext context, PySemaphoreObject self)
    {
        return self.TryAcquire(context, true, null);
    }

    protected override PyResult Exit(PyCallContext context, PySemaphoreObject self, PyObject excType, PyObject excVal, PyObject excTb)
    {
        return self.PyRelease(context, 1, int.MaxValue);
    }
}

/// <summary>
/// threading.BoundedSemaphore — a Semaphore that rejects releases beyond
/// its initial value. The Type class is a parallel sibling (PyGenerator
/// family shape): the Python-level bases chain is declared through the
/// Bases override while the C# side stays one object hierarchy.
/// </summary>
public sealed partial class PyBoundedSemaphoreObject : PySemaphoreObject
{
    internal readonly int _initialValue;

    public override PyTypeObject DefaultPyType => PyBoundedSemaphoreObjectType.Shared;

    internal PyBoundedSemaphoreObject(int value) : base(value)
    {
        _initialValue = value;
    }
}

[PyType("BoundedSemaphore", Module = "threading")]
public sealed partial class PyBoundedSemaphoreObjectType : PyTypeObject<PyBoundedSemaphoreObject>
{
    public override IReadOnlyList<PyTypeObject> Bases => [PySemaphoreObjectType.Shared];

    [PyExport(PySpecialNames.New, nameof(NewImpl))]
    private static partial PyBuiltinFunctionOrMethodObject _new { get; }

    [PyFunctionParameters("value=1")]
    private static PyResult NewImpl(PyCallContext context, PyArguments arguments)
    {
        var result = PySpecialMethods.Index(context, arguments[0]);
        if (result.IsError)
            return result;
        var value = result.Value.Int32Value;
        if (value < 0)
            return PyResult.RaiseException(PyValueErrorObjectType.Shared, PySR.Runtime_Threading_SemaphoreValueNegative);
        return new PyBoundedSemaphoreObject(value);
    }

    protected override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        var obj = _new.Call(context, args, kwargs);
        if (obj.IsError)
            return obj;
        obj.Value._pyType = cls;
        return obj;
    }

    protected override PyResult Repr(PyCallContext context, PyBoundedSemaphoreObject self)
    {
        return PyStrObject.FromString(
            $"<{self.PyType.ReprName} at 0x{self.PyId:x}: value={self._value}/{self._initialValue}>");
    }

    [PyProperty("_initial_value")]
    private static PyResult Get_InitialValue(PyCallContext context, PyBoundedSemaphoreObject self)
    {
        return PyIntObject.FromInteger(self._initialValue);
    }

    [PyMethod("release")]
    [PyFunctionParameters("n=1")]
    private static PyResult Release(PyCallContext context, PyBoundedSemaphoreObject self, PyArguments arguments)
    {
        var error = PySemaphoreObjectType.ParseReleaseCount(context, arguments, out var n);
        if (error is not null)
            return error.GetValueOrDefault();
        return self.PyRelease(context, n, self._initialValue);
    }
}
