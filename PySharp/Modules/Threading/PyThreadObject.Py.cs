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
            throw new InvalidOperationException();


        _thread = new Thread(() =>
        {
            using var threadContext = PyCallContext.FromCreatingThread(context);
            ref var frame = ref threadContext.CurrentInternalFrame;
            try
            {
                PyInterpreter.PyTryCatch(threadContext, () => PyRun(threadContext));
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

    public void PyRun(PyCallContext context)
    {
        if (_target is not PyNoneObject)
        {
            var result = _target.Call(context, _args, _kwargs);
            if (result.IsError)
                throw new PyRuntimeException(context, result.Exception);
        }
    }

    public void PyJoin(double timeout = -1)
    {
        if (_thread is null)
            throw new InvalidOperationException();

        if (timeout < 0)
            _thread.Join();
        else
            _thread.Join(TimeSpan.FromSeconds(timeout));
    }

    public bool PyIsAlive()
    {
        if (_thread is null)
            throw new InvalidOperationException();

        return _thread.IsAlive;
    }
}
