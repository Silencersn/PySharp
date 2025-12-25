using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace PySharp.PyModules.Builtins;

public sealed class PyGeneratorObject : PyObject
{
    internal readonly PyFrame _frame;
    internal readonly Task _task;

    internal PyGeneratorObject(PyFrame frame, Task task)
    {
        Debug.Assert(frame.Back is null);
        Debug.Assert(frame.FrameType is FrameType.YieldFunction or FrameType.YieldLambda);
        Debug.Assert(task.Status is TaskStatus.Created);

        _frame = frame;
        _task = task;
    }

    public override PyTypeObject DefaultPyType => PyGeneratorObjectType.Shared;

    internal PyResult PySend(PyCallContext context, PyObject pyObject)
    {
        if (_task.Status is TaskStatus.Created)
        {
            if (pyObject is not PyNoneObject)
                return PyResult.RaiseTypeError("can't send non-None value to a just-started generator");

            _frame._tcsWaitAtSend = new();
            _task.Start();
            return _frame._tcsWaitAtSend.Task.Result;
        }

        _frame.Back = context.CurrentFrame;
        context.EnterFrame(_frame);
        _frame._tcsWaitAtSend = new();
        Debug.Assert(_frame._tcsWaitAtStartOrYield is not null);
        _frame._tcsWaitAtStartOrYield.SetResult(pyObject);
        var result = _frame._tcsWaitAtSend.Task.Result;
        context.ExitFrame();
        _frame.Back = null;
        return result;
    }
}

public sealed class PyGeneratorObjectType : PyTypeObject<PyGeneratorObjectType, PyGeneratorObject>
{
    public override string Name => "generator";

    protected internal override PyResult Next(PyCallContext context, PyGeneratorObject self)
    {
        return self.PySend(context, PyNoneObject.None);
    }
}