using PySharp.Modules.Builtins;
using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.Calls.Extensions;
using System.Diagnostics;

namespace PySharp.Modules.Threading;

partial class PyThreadObject : PyObject
{
    public void PyStart(PyCallContext context)
    {
        if (_thread is not null)
            throw context.RuntimeError(PySR.Runtime_Threading_ThreadAlreadyStarted);


        _thread = new Thread(() =>
        {
            using var threadContext = PyCallContext.FromCreatingThread(context);
            ref var frame = ref threadContext.CurrentInternalFrame;
            try
            {
                PyInterpreter.PyTryCatch(threadContext, () => PyDispatchRun(threadContext));
            }
            catch (ThreadInterruptedException)
            {
                // TODO:
                //threadContext.EnsureFrameState(frame);
            }
            Debug.Assert(threadContext.FrameState.CurrentFrameCount is 1);
            // no need to context.ExitFrame()
            Debug.Assert(_thread is not null);
            context.PyEnvironment.Threads.Remove(_thread);
        });
        context.PyEnvironment.Threads.Add(_thread);
        _thread.Start();
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
        if (_thread is null)
            throw context.RuntimeError(PySR.Runtime_Threading_JoinBeforeStart);

        if (timeout < 0)
            _thread.Join();
        else
            _thread.Join(TimeSpan.FromSeconds(timeout));
    }

    public bool PyIsAlive()
    {
        // CPython gates is_alive on the started event (threading.py:1177):
        // a never-started thread is simply not alive. The .NET Thread
        // state machine would reject the IsAlive query, so an unset
        // _thread maps to False instead.
        return _thread?.IsAlive ?? false;
    }
}
