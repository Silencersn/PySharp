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

    internal PyGeneratorObject(PyFrame frame)
    {
        Debug.Assert(frame.Back is null);
        Debug.Assert(frame.FrameType is FrameType.YieldFunction or FrameType.YieldLambda);
        _frame = frame;
    }

    public override PyTypeObject DefaultPyType => PyGeneratorObjectType.Shared;

    internal PyResult PySend(PyCallContext context, PyObject pyObject)
    {
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