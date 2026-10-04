using PySharp.Modules.Builtins;
using PySharp.Runtime;
using PySharp.Runtime.Calls;
using System.Diagnostics;

namespace PySharp.Modules.Threading;

partial class PyThreadObject : PyObject
{
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
                PyThreadObjectType.RegisterActive(this);
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
                    PyThreadObjectType.UnregisterActive(_ident.Value, this);
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
        })
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
        if (ReferenceEquals(PyThreadObjectType.TryGetActiveThread(), this))
            throw context.RuntimeError(PySR.Runtime_Threading_JoinCurrentThread);

        if (timeout < 0)
            _thread!.Join();
        else
            _thread!.Join(TimeSpan.FromSeconds(timeout));
    }

    public bool PyIsAlive()
    {
        // CPython gates is_alive on the started event (threading.py:1177)
        // and un-alives it once the OS thread handle reports done; Join(0)
        // is the .NET "is the thread finished" probe.
        return _started && _thread is not null && !_thread.Join(0);
    }
}
