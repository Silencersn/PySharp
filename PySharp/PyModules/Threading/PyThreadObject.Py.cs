using PySharp.AstNodes;
using PySharp.PyModules.Builtins;
using PySharp.PyRuntime;
using System.Diagnostics;

namespace PySharp.PyModules.Threading;

partial class PyThreadObject : PyObject
{
    public void PyStart()
    {
        if (_thread is not null)
            throw new InvalidOperationException();

        var backFrame = PyVirtualMachine.CurrentFrame;

        _thread = new Thread(() =>
        {
            var frame = backFrame.CreateThreadRootFrame();
            PyVirtualMachine.EnterFrame(frame);
            frame.StmtMetaInfoProvider = backFrame.StmtMetaInfoProvider;
            PyInterpreter.PyTryCatch(PyRun);
            Debug.Assert(PyVirtualMachine.CurrentFrame.IsRoot);
            // no need to PyVirtualMachine.ExitFrame()
            Debug.Assert(_thread is not null);
            PyVirtualMachine.PyEnvironment.Threads.Remove(_thread);
        });
        PyVirtualMachine.PyEnvironment.Threads.Add(_thread);
        _thread.Start();
    }

    public void PyRun()
    {
        if (_target is not PyNoneObject)
            _target.Call(_args, _kwargs).PyThrowIfNull();
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
