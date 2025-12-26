using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;
using PySharp.PyRuntime.PyAttributes;
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

    private PyResult StartTask()
    {
        _frame._tcsWaitAtSend = new();
        _task.Start();
        return _frame._tcsWaitAtSend.Task.Result;
    }

    private PyResult ContinueTask(PyCallContext context, YieldCallerAction callerAction)
    {
        _frame.Back = context.CurrentFrame;
        context.EnterFrame(_frame);
        _frame._tcsWaitAtSend = new();
        Debug.Assert(_frame._tcsWaitAtStartOrYield is not null);
        _frame._tcsWaitAtStartOrYield.SetResult(callerAction);
        var result = _frame._tcsWaitAtSend.Task.Result;
        context.ExitFrame();
        _frame.Back = null;
        return result;
    }

    internal PyResult PyNext(PyCallContext context)
    {
        if (_task.Status is TaskStatus.Created)
        {
            return StartTask();
        }

        return ContinueTask(context, YieldCallerAction.Next());
    }

    internal PyResult PySend(PyCallContext context, PyObject pyObject)
    {
        if (_task.Status is TaskStatus.Created)
        {
            if (pyObject is not PyNoneObject)
                return PyResult.RaiseTypeError("can't send non-None value to a just-started generator");

            return StartTask();
        }

        return ContinueTask(context, YieldCallerAction.Send(pyObject));
    }

    internal PyResult PyThrow(PyCallContext context, PyObject pyObject)
    {
        if (_task.Status is TaskStatus.Created)
        {
            return PyResult.RaiseExceptionFromTypeOrInstance(pyObject);
        }

        return ContinueTask(context, YieldCallerAction.Throw(pyObject));
    }

    internal PyResult PyClose(PyCallContext context)
    {
        throw new NotImplementedException();
    }
}

public sealed class PyGeneratorObjectType : PyTypeObject<PyGeneratorObjectType, PyGeneratorObject>
{
    public override string Name => "generator";

    public PyGeneratorObjectType()
    {
        AppendMethodDescriptor("send", Send);
        AppendMethodDescriptor("throw", Throw);
        AppendMethodDescriptor("close", Close);
    }

    protected internal override PyResult Next(PyCallContext context, PyGeneratorObject self)
    {
        return self.PyNext(context);
    }

    [PyFunctionArgsDef("value")]
    internal PyResult Send(PyCallContext context, PyGeneratorObject self, PyArguments arguments)
    {
        return self.PySend(context, arguments[0]);
    }

    [PyFunctionArgsDef("value")]
    internal PyResult Throw(PyCallContext context, PyGeneratorObject self, PyArguments arguments)
    {
        return self.PyThrow(context, arguments[0]);
    }

    [PyFunctionArgsDef("")]
    internal PyResult Close(PyCallContext context, PyGeneratorObject self, PyArguments arguments)
    {
        return self.PyClose(context);
    }
}