using PySharp.Modules.Builtins;
using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;

namespace PySharp.Modules.Threading;

/// <summary>
/// threading.Event — a boolean flag over Condition(Lock()), set/clear on
/// one side and wait (with optional timeout) on the other.
/// </summary>
public sealed partial class PyEventObject : PyObject, IDisposable
{
    internal readonly PyLockObject _lock;
    internal readonly PyConditionObject _cond;
    internal bool _flag;

    public override PyTypeObject DefaultPyType => PyEventObjectType.Shared;

    internal PyEventObject()
    {
        _lock = new PyLockObject();
        _cond = new PyConditionObject(_lock, PyCallContext.NonContextDependency);
        _flag = false;
    }

    // host-side deterministic release over the owned lock, the
    // PyLockObject pattern; Python code never disposes an event
    public void Dispose()
    {
        _lock.Dispose();
    }
}

[PyType("Event", Module = "threading")]
public sealed partial class PyEventObjectType : PyTypeObject<PyEventObject>
{
    [PyExport(PySpecialNames.New, nameof(NewImpl))]
    private static partial PyBuiltinFunctionOrMethodObject _new { get; }

    [PyFunctionParameters()]
    private static PyResult NewImpl(PyCallContext context, PyArguments arguments)
    {
        return new PyEventObject();
    }

    protected override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        var obj = _new.Call(context, args, kwargs);
        if (obj.IsError)
            return obj;
        obj.Value._pyType = cls;
        return obj;
    }

    protected override PyResult Repr(PyCallContext context, PyEventObject self)
    {
        var status = self._flag ? "set" : "unset";
        return PyStrObject.FromString(
            $"<{self.PyType.ReprName} at 0x{self.PyId:x}: {status}>");
    }

    [PyMethod("is_set")]
    [PyFunctionParameters()]
    private static PyResult IsSet(PyCallContext context, PyEventObject self, PyArguments arguments)
    {
        return PyBoolObject.FromBoolean(self._flag);
    }

    [PyMethod("isSet")]
    [PyFunctionParameters()]
    private static PyResult IsSetAlias(PyCallContext context, PyEventObject self, PyArguments arguments)
    {
        var warnResult = context.Warn(PyDeprecationWarningObjectType.Shared, PySR.Runtime_Threading_DeprecatedIsSet);
        if (warnResult.IsError)
            return warnResult;
        return PyBoolObject.FromBoolean(self._flag);
    }

    [PyMethod("set")]
    [PyFunctionParameters()]
    private static PyResult Set(PyCallContext context, PyEventObject self, PyArguments arguments)
    {
        self._lock.TryAcquire(-1, true);
        self._flag = true;
        var notified = PyConditionObjectType.PyNotify(context, self._cond, int.MaxValue);
        self._lock.PyRelease();
        return notified;
    }

    [PyMethod("clear")]
    [PyFunctionParameters()]
    private static PyResult Clear(PyCallContext context, PyEventObject self, PyArguments arguments)
    {
        self._lock.TryAcquire(-1, true);
        self._flag = false;
        self._lock.PyRelease();
        return PyNoneObject.None;
    }

    [PyMethod("wait")]
    [PyFunctionParameters("timeout=None")]
    private static PyResult Wait(PyCallContext context, PyEventObject self, PyArguments arguments)
    {
        double? timeout = null;
        if (arguments[0] is not PyNoneObject)
        {
            var converted = PySpecialMethods.Float(context, arguments[0]);
            if (converted.IsError)
                return converted;
            timeout = converted.Value.Value;
        }

        self._lock.TryAcquire(-1, true);
        var signaled = self._flag;
        if (!signaled)
        {
            // the timeout range check only runs on the waiting path: a set
            // event returns immediately no matter how large the timeout
            if (timeout is not null && PyLockObjectType.ValidateTimeoutRange(context, timeout.Value) is { } rangeError)
            {
                self._lock.PyRelease();
                return rangeError;
            }

            // the wait runs through the Condition protocol: it releases
            // the lock while parked and re-acquires before returning, so
            // the outer hold stays balanced
            var waited = PyConditionObjectType.PyWait(context, self._cond, timeout);
            if (waited.IsError)
            {
                self._lock.PyRelease();
                return waited;
            }
            signaled = ((PyBoolObject)waited.Value).BoolValue;
        }
        self._lock.PyRelease();
        return PyBoolObject.FromBoolean(signaled);
    }
}
