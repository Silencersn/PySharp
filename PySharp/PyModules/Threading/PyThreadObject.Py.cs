using PySharp.AstNodes;
using PySharp.PyModules.Builtins;
using PySharp.PyRuntime;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

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
            Task.Run(() =>
            {
                var frame = backFrame.CreateThreadRootFrame();
                PyVirtualMachine.EnterFrame(frame);
                frame.Info.MetaInfo = backFrame.Info.MetaInfo;
                PyInterpreter.PyTryCatch(PyRun);
                PyVirtualMachine.ExitFrame();
                Debug.Assert(PyVirtualMachine.CurrentFrame.IsRoot);
            }, PyVirtualMachine.PyEnvironment.CancellationTokenSource.Token).Wait();
        });
        PyVirtualMachine.PyEnvironment.Threads.Add(_thread);
        _thread.Start();
    }

    public void PyRun()
    {
        if (_target is not PyNoneObject)
            _target.Call(_args, _kwargs).PyThrowIfNull();
    }

    public void PyJoin(double timeout)
    {
        if (_thread is null)
            throw new InvalidOperationException();

        _thread.Join(TimeSpan.FromSeconds(timeout));
    }

    public bool PyIsAlive()
    {
        if (_thread is null)
            throw new InvalidOperationException();

        return _thread.IsAlive;
    }
}
