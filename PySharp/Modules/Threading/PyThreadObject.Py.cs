using PySharp.Modules.Builtins;
using PySharp.Runtime;
using PySharp.Runtime.Calls;
using System.Diagnostics;

namespace PySharp.Modules.Threading;

partial class PyThreadObject : PyObject
{
    // threading.py Thread.__repr__ — shared by the Thread, _MainThread and
    // _DummyThread types, so the status text carries the actual PyType name
    internal PyResult FormatRepr(PyCallContext context)
    {
        // initial/started/stopped, then the daemon suffix, then the ident
        // once the thread has one
        string status;
        if (!_started)
            status = "initial";
        else if (_thread is not null && _thread.Join(0))
            status = "stopped";
        else
            status = "started";

        var truthy = PySpecialMethods.Bool(context, _daemonic);
        if (truthy.IsError)
            return truthy;
        if (truthy.Value.BoolValue)
            status += " daemon";
        if (_ident is { } ident)
            status += $" {ident}";

        return PyStrObject.FromString($"<{PyType.Name}({_name}, {status})>");
    }

    public void PyStart(PyCallContext context)
    {
        if (_thread is not null)
            throw context.RuntimeError(PySR.Runtime_Threading_ThreadAlreadyStarted);

        // daemon threads must not hold the process open at exit, which is
        // exactly the CLR's background-thread rule, so the truthiness of the
        // stored daemon value maps onto IsBackground (threading.py passes
        // daemon=self.daemon to _start_joinable_thread)
        var daemon = PySpecialMethods.Bool(context, _daemonic);
        if (daemon.IsError)
            throw new PyRuntimeException(context, daemon.Exception);

        // stack_size() maps onto the CLR per-thread stack reservation; the
        // OS rounds values below the default reservation back up, so small
        // configured sizes stay harmless
        var configuredSize = PyThreadObjectType.ConfiguredStackSize(context.PyEnvironment);
        var maxStackSize = configuredSize > 0 ? (int)Math.Min(configuredSize, int.MaxValue) : 0;

        _thread = new Thread(() =>
        {
            using var threadContext = PyCallContext.FromCreatingThread(context, _name);
            ref var frame = ref threadContext.CurrentInternalFrame;
            try
            {
                // the identity fields and the active registration settle
                // before the started flag flips, so start() — which is
                // blocked on the gate — returns with ident/native_id and
                // is_alive already consistent (threading.py
                // _bootstrap_inner sets them before _started.set())
                _ident = Environment.CurrentManagedThreadId;
                _nativeId = _ident;
                PyThreadObjectType.RegisterActive(threadContext.PyEnvironment, this);
                lock (_startedGate)
                {
                    _started = true;
                    Monitor.Pulse(_startedGate);
                }
                // a worker thread's uncaught exception only reports through
                // the excepthook channel; the process exit code stays with
                // the main thread
                try
                {
                    PyInterpreter.PyThreadExceptionReport(threadContext, () => PyDispatchRun(threadContext));
                }
                finally
                {
                    PyThreadObjectType.UnregisterActive(threadContext.PyEnvironment, _ident.Value, this);
                }
            }
            catch (ThreadInterruptedException)
            {
                // TODO:
                //threadContext.EnsureFrameState(frame);
                lock (_startedGate)
                {
                    _started = true;
                    Monitor.Pulse(_startedGate);
                }
            }
            Debug.Assert(threadContext.FrameState.CurrentFrameCount is 1);
            // no need to context.ExitFrame()
            Debug.Assert(_thread is not null);
            context.PyEnvironment.Threads.Remove(_thread);
        }, maxStackSize)
        {
            IsBackground = daemon.Value.BoolValue,
        };
        context.PyEnvironment.Threads.Add(_thread);
        _thread.Start();
        // threading.py start() returns only once the worker has set its
        // started event: a just-started thread reports alive immediately
        lock (_startedGate)
        {
            while (!_started)
                Monitor.Wait(_startedGate);
        }
    }

    // CPython _bootstrap_inner invokes self.run() — an ordinary attribute
    // lookup (threading.py:1082), so a subclass override replaces the
    // default target call.
    private void PyDispatchRun(PyCallContext context)
    {
        var result = this.CallMethod(context, "run");
        if (result.IsError)
            throw new PyRuntimeException(context, result.Exception);
    }

    // CPython Thread.run default: invoke the constructor target, if any,
    // with the stored args/kwargs (threading.py:1013-1028).
    public PyResult PyRun(PyCallContext context)
    {
        if (_target is not PyNoneObject)
            return _target.Call(context, _args, _kwargs);

        return PyNoneObject.None;
    }

    public void PyJoin(PyCallContext context, double timeout = -1)
    {
        if (!_started)
            throw context.RuntimeError(PySR.Runtime_Threading_JoinBeforeStart);
        if (ReferenceEquals(PyThreadObjectType.TryGetActiveThread(context.PyEnvironment), this))
            throw context.RuntimeError(PySR.Runtime_Threading_JoinCurrentThread);

        // the timeout range check only runs on the waiting path: a
        // finished thread reports join success immediately no matter how
        // large the timeout (threading.py joins a set Event)
        if (timeout < 0)
        {
            _thread!.Join();
            return;
        }
        if (_thread!.Join(0))
            return;
        if (PyLockObjectType.ValidateTimeoutRange(context, timeout) is { } rangeError)
            throw new PyRuntimeException(context, rangeError.Exception!);
        _thread.Join(TimeSpan.FromSeconds(timeout));
    }

    public bool PyIsAlive()
    {
        // CPython gates is_alive on the started event (threading.py:1177)
        // and un-alives it once the OS thread handle reports done; Join(0)
        // is the .NET "is the thread finished" probe.
        return _started && _thread is not null && !_thread.Join(0);
    }
}
