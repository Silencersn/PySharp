using PySharp.Modules.Builtins;
using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.Environments;
using PySharp.Runtime.PyAttributes;
using System.Diagnostics;

namespace PySharp.Modules.Threading;

public partial class PyThreadObject : PyObject
{
    internal readonly PyObject _target;
    internal readonly IReadOnlyList<PyObject> _args;
    internal readonly IReadOnlyDictionary<string, PyObject> _kwargs;
    internal Thread? _thread;
    // the Thread-N default name or the explicit name= argument, settled at
    // construction — the excepthook report and the repr read this
    internal string _name;
    // threading.py stores the constructor's daemon value as-is (daemon=1
    // keeps the int); only truthiness decides the repr suffix and the
    // background mapping, and the default inherits the creating thread
    internal PyObject _daemonic;
    // the identity fields settle when the worker first runs, before the
    // started event flips (threading.py _bootstrap_inner)
    internal long? _ident;
    internal long? _nativeId;
    // flips at the top of the worker under the gate; start() waits on it
    // so a started thread observably has ident/native_id and a live
    // is_alive() (threading.py's _started Event). Volatile because the
    // repr/is_alive/daemon setters read it without taking the gate.
    internal volatile bool _started;
    internal readonly object _startedGate = new();
    // marks the synthetic objects current_thread() hands out for threads
    // that were not created through this module
    internal bool _isDummy;

    public override PyTypeObject DefaultPyType => PyThreadObjectType.Shared;

    internal PyThreadObject(PyObject target, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs, string name)
    {
        _target = target;
        _args = args;
        _kwargs = kwargs;
        _thread = null;
        _name = name;
        _daemonic = PyBoolObject.False;
    }
}

[PyType("Thread", Module = "threading")]
public sealed partial class PyThreadObjectType : PyTypeObject<PyThreadObject>
{
    // CPython names threads with a process-wide counter consumed at
    // construction time (threading.py _newname), not with the OS thread id
    private static int _nameCounter;

    // the Thread-<n> default name, suffixed with the target's __name__
    // when it has one (threading.py __init__ / _newname); shared with Timer
    internal static string NextDefaultName(PyCallContext context, PyObject target)
    {
        var name = $"Thread-{Interlocked.Increment(ref _nameCounter)}";
        if (target is not PyNoneObject)
        {
            var targetName = PyTypeObject.DefaultGetAttribute(context, target, PyStrObject.FromString("__name__"));
            if (!targetName.IsError && targetName.Value is PyStrObject targetNameString)
                name += $" ({targetNameString.Value})";
        }
        return name;
    }

    // _PyOS_MIN_STACK_SIZE + SYSTEM_PAGE_SIZE on Windows: the smallest
    // nonzero size stack_size accepts
    internal const long MinimumStackSize = 53248;

    internal static void RegisterActive(PyEnvironment environment, PyThreadObject thread)
    {
        Debug.Assert(thread._ident is not null);
        var state = environment.ThreadingState;
        lock (state.Gate)
        {
            state.Active[thread._ident.Value] = thread;
        }
    }

    // conditional removal: idents are recycled once a thread exits, so the
    // dying worker must not unregister a successor that reused its ident
    internal static void UnregisterActive(PyEnvironment environment, long ident, PyThreadObject thread)
    {
        var state = environment.ThreadingState;
        lock (state.Gate)
        {
            if (state.Active.TryGetValue(ident, out var registered) && ReferenceEquals(registered, thread))
                state.Active.Remove(ident);
        }
    }

    internal static PyThreadObject? TryGetActiveThread(PyEnvironment environment)
    {
        var state = environment.ThreadingState;
        lock (state.Gate)
        {
            return state.Active.GetValueOrDefault(Environment.CurrentManagedThreadId);
        }
    }

    // active_count() over _active alone: a worker registers itself before
    // the started flag flips and start() blocks on that flip, so there is
    // no observable threading.py _limbo window
    internal static int ActiveThreadCount(PyEnvironment environment)
    {
        var state = environment.ThreadingState;
        lock (state.Gate)
        {
            return state.Active.Count;
        }
    }

    // enumerate(): the _active values in insertion order
    internal static List<PyThreadObject> SnapshotActiveThreads(PyEnvironment environment)
    {
        var state = environment.ThreadingState;
        lock (state.Gate)
        {
            return [.. state.Active.Values];
        }
    }

    internal static PyThreadObject GetMainThread(PyEnvironment environment)
    {
        var state = environment.ThreadingState;
        lock (state.Gate)
        {
            if (state.MainThread is null)
            {
                var main = CreateSpecialThread(PyMainThreadObjectType.Shared, "MainThread", PyBoolObject.False);
                state.Active[main._ident!.Value] = main;
                state.MainThread = main;
            }
            return state.MainThread;
        }
    }

    // current_thread() on a thread that never went through this module
    // (threading.py _DummyThread): alive, daemonic, registered so repeated
    // calls agree, and joinable never
    internal static PyThreadObject CreateDummyThread(PyEnvironment environment)
    {
        var dummy = CreateSpecialThread(PyDummyThreadObjectType.Shared,
            $"Dummy-{Interlocked.Increment(ref _nameCounter)}", PyBoolObject.True);
        RegisterActive(environment, dummy);
        return dummy;
    }

    // the shared shape of _MainThread and _DummyThread instances: alive
    // from birth, identity fields settled to the running thread, backed by
    // the live OS thread so the Join(0) probes in repr/is_alive hold — the
    // only threads that never travel through PyStart
    private static PyThreadObject CreateSpecialThread(PyTypeObject pyType, string name, PyObject daemonic)
    {
        return new PyThreadObject(PyNoneObject.None, [], new Dictionary<string, PyObject>(), name)
        {
            _pyType = pyType,
            _daemonic = daemonic,
            _ident = Environment.CurrentManagedThreadId,
            _nativeId = Environment.CurrentManagedThreadId,
            _started = true,
            _thread = Thread.CurrentThread,
        };
    }

    // stack_size() always returns the value configured before the call and
    // then installs the new one; size 0 resets to the platform default
    internal static long ExchangeStackSize(PyEnvironment environment, long size)
    {
        return Interlocked.Exchange(ref environment.ThreadingState.StackSize, size);
    }

    internal static long ConfiguredStackSize(PyEnvironment environment)
    {
        return Interlocked.Read(ref environment.ThreadingState.StackSize);
    }

    [PyExport(PySpecialNames.New, nameof(NewImpl))]
    private static partial PyBuiltinFunctionOrMethodObject _new { get; }

    [PyFunctionParameters("group=None", "target=None", "name=None", "args=()", "kwargs={}", "*", "daemon=None", "context=None")]
    private static PyResult NewImpl(PyCallContext context, PyArguments arguments)
    {
        // threading.py asserts group away (assert group is None)
        if (arguments[0] is not PyNoneObject)
            return PyResult.RaiseException(PyAssertionErrorObjectType.Shared, PySR.Runtime_Threading_GroupMustBeNone);

        // args accepts tuple and list alike (threading.py stores it verbatim)
        IReadOnlyList<PyObject> args;
        if (arguments[3] is PyTupleObject tuple)
            args = [.. tuple];
        else if (arguments[3] is PyListObject list)
            args = [.. list];
        else
            return PyResult.TypeError(null);
        if (arguments[4] is not PyDictObject kwargs)
            return PyResult.TypeError(null);
        Dictionary<string, PyObject> dict = [];
        foreach (var pair in kwargs.Entries)
        {
            if (pair.Key is not PyStrObject str)
                return PyResult.TypeError(null);
            dict[str.Value] = pair.Value;
        }

        // an explicit truthy name wins str()-coerced (name=123 → "123"); a
        // falsy one falls back to Thread-<construction order>, suffixed
        // with the target's __name__ when it has one (threading.py
        // __init__ / _newname)
        string name;
        var explicitName = arguments[2];
        bool useExplicitName;
        if (explicitName is PyNoneObject)
        {
            useExplicitName = false;
        }
        else
        {
            // a __bool__ raising during the truthiness probe propagates,
            // exactly like threading.py's `if name:`
            var truthy = PySpecialMethods.Bool(context, explicitName);
            if (truthy.IsError)
                return truthy;
            useExplicitName = truthy.Value.BoolValue;
        }
        if (useExplicitName)
        {
            var coerced = PySpecialMethods.Str(context, explicitName);
            if (coerced.IsError)
                return coerced;
            name = coerced.Value.Value;
        }
        else
        {
            name = NextDefaultName(context, arguments[1]);
        }

        // daemon: explicit values are stored as-is; the default inherits
        // the creating thread's daemon setting (threading.py __init__)
        var explicitDaemon = arguments["daemon"];
        PyObject daemon;
        if (explicitDaemon is not PyNoneObject)
            daemon = explicitDaemon;
        else
            daemon = TryGetActiveThread(context.PyEnvironment)?._daemonic ?? PyBoolObject.False;

        return new PyThreadObject(arguments[1], args, dict, name)
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

    protected override PyResult Repr(PyCallContext context, PyThreadObject self)
    {
        return self.FormatRepr(context);
    }

    [PyMethod("start")]
    [PyFunctionParameters()]
    private static PyResult Start(PyCallContext context, PyThreadObject self, PyArguments arguments)
    {
        self.PyStart(context);
        return PyNoneObject.None;
    }

    [PyMethod("join")]
    [PyFunctionParameters("timeout=None")]
    private static PyResult Join(PyCallContext context, PyThreadObject self, PyArguments arguments)
    {
        if (arguments[0] is PyNoneObject)
        {
            self.PyJoin(context, -1);
            return PyNoneObject.None;
        }
        var result = PySpecialMethods.Float(context, arguments[0]);
        if (result.IsError)
            return result;
        var timeout = result.Value.Value;
        timeout = Math.Max(timeout, 0);
        self.PyJoin(context, timeout);
        return PyNoneObject.None;
    }

    [PyMethod("run")]
    [PyFunctionParameters()]
    private static PyResult Run(PyCallContext context, PyThreadObject self, PyArguments arguments)
    {
        return self.PyRun(context);
    }

    [PyMethod("is_alive")]
    [PyFunctionParameters()]
    private static PyResult IsAlive(PyCallContext context, PyThreadObject self, PyArguments arguments)
    {
        return PyBoolObject.FromBoolean(self.PyIsAlive());
    }

    [PyProperty("name")]
    private static PyResult Get_Name(PyCallContext context, PyThreadObject self)
    {
        return PyStrObject.FromString(self._name);
    }

    [PyProperty("name", Type = PyPropertyMethodType.Setter)]
    private static PyResult Set_Name(PyCallContext context, PyThreadObject self, PyObject value)
    {
        // threading.py coerces through str() on assignment
        var coerced = PySpecialMethods.Str(context, value);
        if (coerced.IsError)
            return coerced;
        self._name = coerced.Value.Value;
        return PyNoneObject.None;
    }

    [PyProperty("ident")]
    private static PyResult Get_Ident(PyCallContext context, PyThreadObject self)
    {
        return self._ident is { } ident ? PyIntObject.FromInteger(ident) : PyNoneObject.None;
    }

    // CPython on Windows reports the same kernel thread id for both fields
    [PyProperty("native_id")]
    private static PyResult Get_NativeId(PyCallContext context, PyThreadObject self)
    {
        return self._nativeId is { } nativeId ? PyIntObject.FromInteger(nativeId) : PyNoneObject.None;
    }

    [PyProperty("daemon")]
    private static PyResult Get_Daemon(PyCallContext context, PyThreadObject self)
    {
        return self._daemonic;
    }

    [PyProperty("daemon", Type = PyPropertyMethodType.Setter)]
    private static PyResult Set_Daemon(PyCallContext context, PyThreadObject self, PyObject value)
    {
        if (self._started)
            return PyResult.RaiseException(PyRuntimeErrorObjectType.Shared, PySR.Runtime_Threading_DaemonOfActiveThread);
        self._daemonic = value;
        return PyNoneObject.None;
    }

    [PyMethod("getName")]
    [PyFunctionParameters()]
    private static PyResult GetName(PyCallContext context, PyThreadObject self, PyArguments arguments)
    {
        var warnResult = context.Warn(PyDeprecationWarningObjectType.Shared, PySR.Runtime_Threading_DeprecatedGetName);
        if (warnResult.IsError)
            return warnResult;
        return PyStrObject.FromString(self._name);
    }

    [PyMethod("setName")]
    [PyFunctionParameters("name")]
    private static PyResult SetName(PyCallContext context, PyThreadObject self, PyArguments arguments)
    {
        var warnResult = context.Warn(PyDeprecationWarningObjectType.Shared, PySR.Runtime_Threading_DeprecatedSetName);
        if (warnResult.IsError)
            return warnResult;
        return Set_Name(context, self, arguments[0]);
    }

    [PyMethod("isDaemon")]
    [PyFunctionParameters()]
    private static PyResult IsDaemon(PyCallContext context, PyThreadObject self, PyArguments arguments)
    {
        var warnResult = context.Warn(PyDeprecationWarningObjectType.Shared, PySR.Runtime_Threading_DeprecatedIsDaemon);
        if (warnResult.IsError)
            return warnResult;
        return self._daemonic;
    }

    [PyMethod("setDaemon")]
    [PyFunctionParameters("daemonic")]
    private static PyResult SetDaemon(PyCallContext context, PyThreadObject self, PyArguments arguments)
    {
        var warnResult = context.Warn(PyDeprecationWarningObjectType.Shared, PySR.Runtime_Threading_DeprecatedSetDaemon);
        if (warnResult.IsError)
            return warnResult;
        return Set_Daemon(context, self, arguments[0]);
    }
}
