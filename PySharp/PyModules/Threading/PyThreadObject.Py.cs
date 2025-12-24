using PySharp.AstNodes;
using PySharp.PyModules.Builtins;
using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;
using System.Diagnostics;

namespace PySharp.PyModules.Threading;

partial class PyThreadObject : PyObject
{
    public void PyStart(PyCallContext context)
    {
        if (_thread is not null)
            throw new InvalidOperationException();

        var backFrame = PyVirtualMachine.CurrentFrame;

        _thread = new Thread(() =>
        {
            var frame = backFrame.CreateThreadRootFrame();
            PyVirtualMachine.EnterFrame(frame);
            frame.StmtMetaInfoProvider = backFrame.StmtMetaInfoProvider;
            try
            {
                PyInterpreter.PyTryCatch(context, () => PyRun(context));
            }
            catch (ThreadInterruptedException)
            {
                while (PyVirtualMachine.CurrentFrame != frame)
                    PyVirtualMachine.ExitFrame();
            }
            Debug.Assert(PyVirtualMachine.CurrentFrame.IsRoot);
            // no need to PyVirtualMachine.ExitFrame()
            Debug.Assert(_thread is not null);
            PyVirtualMachine.PyEnvironment.Threads.Remove(_thread);
        });
        PyVirtualMachine.PyEnvironment.Threads.Add(_thread);
        _thread.Start();
    }

    public void PyRun(PyCallContext context)
    {
        if (_target is not PyNoneObject)
        {
            var result = _target.Call(context, _args, _kwargs);
            if (result.IsError)
                throw new PyRuntimeException(result.Exception);
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
