using PySharp.Modules.Builtins;
using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;

namespace PySharp.Modules.Threading;

/// <summary>
/// threading.Timer — a Thread that runs its function after a delay unless
/// cancelled first. The interval travels as the original object and only
/// converts to a number when run() waits on it (threading.py Timer).
/// </summary>
public sealed partial class PyTimerObject : PyThreadObject, IDisposable
{
    internal readonly PyObject _interval;
    internal readonly PyEventObject _finished;

    // the constructor arguments stored verbatim for the args/kwargs
    // attributes; None arrives, a fresh list/dict is built for it like
    // threading.Timer.__init__ does
    internal readonly PyObject _argsObject;
    internal readonly PyObject _kwargsObject;

    public override PyTypeObject DefaultPyType => PyTimerObjectType.Shared;

    internal PyTimerObject(PyObject function, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs, string name, PyObject interval, PyObject argsObject, PyObject kwargsObject)
        : base(function, args, kwargs, name)
    {
        _interval = interval;
        _argsObject = argsObject;
        _kwargsObject = kwargsObject;
        _finished = new PyEventObject();
    }

    // host-side deterministic release over the owned lock, the
    // PyLockObject pattern; Python code never disposes a timer
    public void Dispose()
    {
        _finished.Dispose();
    }
}

[PyType("Timer", Module = "threading")]
public sealed partial class PyTimerObjectType : PyTypeObject<PyTimerObject>
{
    public override IReadOnlyList<PyTypeObject> Bases => [PyThreadObjectType.Shared];

    [PyExport(PySpecialNames.New, nameof(NewImpl))]
    private static partial PyBuiltinFunctionOrMethodObject _new { get; }

    [PyFunctionParameters("interval", "function", "args=None", "kwargs=None")]
    private static PyResult NewImpl(PyCallContext context, PyArguments arguments)
    {
        // threading.Timer.__init__ stores its args/kwargs verbatim (a tuple
        // stays a tuple) and only turns the None defaults into a fresh
        // list/dict; the call-path expansion happens in run(), so a
        // non-sequence surfaces there like CPython's star-unpack, not at
        // construction
        PyObject argsObject = arguments[2] is PyNoneObject
            ? PyListObject.CreateList([])
            : arguments[2];
        PyObject kwargsObject = arguments[3] is PyNoneObject
            ? new PyDictObject()
            : arguments[3];

        // the name settles through the same Thread-<n> default and the
        // daemon flag inherits the creating thread (Thread.__init__ with
        // neither name nor daemon given)
        var name = PyThreadObjectType.NextDefaultName(context, arguments[1]);
        var daemon = PyThreadObjectType.TryGetActiveThread(context.PyEnvironment)?._daemonic ?? PyBoolObject.False;

        return new PyTimerObject(arguments[1], [], new Dictionary<string, PyObject>(), name, arguments[0], argsObject, kwargsObject)
        {
            _daemonic = daemon,
        };
    }

    protected override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        var obj = _new.Call(context, args, kwargs);
        if (obj.IsError)
            return obj;
        obj.Value._pyType = cls;
        return obj;
    }

    protected override PyResult Repr(PyCallContext context, PyTimerObject self)
    {
        return self.FormatRepr(context);
    }

    // threading.Timer.run: wait out the interval on the finished event;
    // fire the function only when the wait timed out, then set the event
    // either way — a raising function skips the trailing set (threading.py
    // has no finally there)
    [PyMethod("run")]
    [PyFunctionParameters()]
    private static PyResult Run(PyCallContext context, PyTimerObject self, PyArguments arguments)
    {
        var converted = PySpecialMethods.Float(context, self._interval);
        if (converted.IsError)
            return converted;
        var timeout = converted.Value.Value;

        var waited = self._finished.WaitCore(context, timeout);
        if (waited.IsError)
            return waited;
        if (((PyBoolObject)waited.Value).BoolValue)
            return PyNoneObject.None;

        // f(*args, **kwargs) with the verbatim constructor arguments; a
        // non-sequence/non-mapping is the star-unpack failure CPython
        // raises here, typed after the actual object
        IReadOnlyList<PyObject> args;
        switch (self._argsObject)
        {
            case PyTupleObject tuple:
                args = [.. tuple];
                break;
            case PyListObject list:
                args = [.. list];
                break;
            default:
                return PyResult.TypeError($"argument after * must be an iterable, not {self._argsObject.PyType.Name}");
        }
        var kwargs = new Dictionary<string, PyObject>();
        if (self._kwargsObject is PyDictObject dict)
        {
            foreach (var pair in dict.Entries)
            {
                if (pair.Key is not PyStrObject str)
                    return PyResult.TypeError(null);
                kwargs[str.Value] = pair.Value;
            }
        }
        else
        {
            return PyResult.TypeError($"argument after ** must be a mapping, not {self._kwargsObject.PyType.Name}");
        }

        var fired = self._target.Call(context, args, kwargs);
        if (fired.IsError)
            return fired;
        return self._finished.SetFlag(context);
    }

    [PyMethod("cancel")]
    [PyFunctionParameters()]
    private static PyResult Cancel(PyCallContext context, PyTimerObject self, PyArguments arguments)
    {
        return self._finished.SetFlag(context);
    }

    [PyProperty("interval")]
    private static PyResult Get_Interval(PyCallContext context, PyTimerObject self)
    {
        return self._interval;
    }

    [PyProperty("function")]
    private static PyResult Get_Function(PyCallContext context, PyTimerObject self)
    {
        return self._target;
    }

    [PyProperty("args")]
    private static PyResult Get_Args(PyCallContext context, PyTimerObject self)
    {
        return self._argsObject;
    }

    [PyProperty("kwargs")]
    private static PyResult Get_Kwargs(PyCallContext context, PyTimerObject self)
    {
        return self._kwargsObject;
    }

    [PyProperty("finished")]
    private static PyResult Get_Finished(PyCallContext context, PyTimerObject self)
    {
        return self._finished;
    }
}
