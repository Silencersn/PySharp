using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;
using PySharp.PyRuntime.PyAttributes;
using System.Diagnostics;

namespace PySharp.PyModules.Builtins;

public sealed class PyGeneratorObject : PyObject, IPyObjectName
{
    internal readonly PyFrame _frame;
    internal Task? _task;
    public string Name { get; }

    internal PyGeneratorObject(string name, PyFrame frame, Task task)
    {
        Debug.Assert(frame.Back is null);
        Debug.Assert(frame.FrameType is FrameType.YieldFunction or FrameType.YieldLambda);
        Debug.Assert(task.Status is TaskStatus.Created);

        Name = name;
        _frame = frame;
        _task = task;
    }

    public override PyTypeObject DefaultPyType => PyGeneratorObjectType.Shared;

    private PyResult StartTask()
    {
        Debug.Assert(_task is not null);
        Debug.Assert(!_frame._generatorCompleted);

        _frame._tcsWaitAtSend = new();
        _task.Start();
        return _frame._tcsWaitAtSend.Task.Result;
    }

    private PyResult ContinueTask(PyCallContext context, YieldCallerAction callerAction)
    {
        Debug.Assert(_task is not null);
        Debug.Assert(!_frame._generatorCompleted);

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

    private PyResult WrapIfReturn(PyResult result)
    {
        // raise
        if (result.IsError)
        {
            _task = null;
            return result;
        }

        // return
        if (_frame._generatorCompleted)
        {
            _task = null;
            return PyResult.RaiseStopIteration(result.Value);
        }

        // yield
        return result;
    }

    internal PyResult PyNext(PyCallContext context)
    {
        if (_task is null)
            return PyResult.RaiseStopIteration();

        if (_task.Status is TaskStatus.Created)
        {
            return StartTask();
        }

        var result = ContinueTask(context, YieldCallerAction.Next());
        return WrapIfReturn(result);
    }

    internal PyResult PySend(PyCallContext context, PyObject pyObject)
    {
        if (_task is null)
            return PyResult.RaiseStopIteration();

        if (_task.Status is TaskStatus.Created)
        {
            if (pyObject is not PyNoneObject)
                return PyResult.RaiseTypeError("can't send non-None value to a just-started generator");

            return StartTask();
        }

        var result = ContinueTask(context, YieldCallerAction.Send(pyObject));
        return WrapIfReturn(result);
    }

    internal PyResult PyThrow(PyCallContext context, PyObject pyObject)
    {
        if (_task is null)
            return PyResult.RaiseStopIteration();

        if (_task.Status is TaskStatus.Created)
        {
            return PyResult.RaiseExceptionFromTypeOrInstance(pyObject);
        }

        var result = ContinueTask(context, YieldCallerAction.Throw(pyObject));
        return WrapIfReturn(result);
    }

    internal PyResult PyClose(PyCallContext context)
    {
        if (_task is null)
            return PyNoneObject.None;

        if (_task.Status is TaskStatus.Created)
        {
            _task = null;
            return PyNoneObject.None;
        }

        var result = ContinueTask(context, YieldCallerAction.Close());
        _task = null;

        if (result.IsError)
        {
            if (result.Exception.PyType is PyGeneratorExitObjectType)
                return PyNoneObject.None;

            return result;
        }

        if (!_frame._generatorCompleted)
            return PyResult.RaiseRuntimeError("generator ignored GeneratorExit");

        return result;
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

    protected internal override PyResult Repr(PyCallContext context, PyGeneratorObject self)
    {
        return PyStrObject.FromString($"<generator object {self.Name} at 0x{self.PyId:X16}>");
    }

    protected internal override PyResult Iter(PyCallContext context, PyGeneratorObject self)
    {
        return self;
    }

    protected internal override PyResult Next(PyCallContext context, PyGeneratorObject self)
    {
        return self.PyNext(context);
    }

    [PyFunctionArgsDef("value")]
    internal PyResult Send(PyCallContext context, PyGeneratorObject self, PyArguments arguments)
    {
        if (arguments[0] is PyNoneObject)
            return self.PyNext(context);

        return self.PySend(context, arguments[0]);
    }

    [PyFunctionArgsDef("value")]
    internal PyResult Throw(PyCallContext context, PyGeneratorObject self, PyArguments arguments)
    {
        return self.PyThrow(context, arguments[0]);
    }

    [PyFunctionArgsDef()]
    internal PyResult Close(PyCallContext context, PyGeneratorObject self, PyArguments arguments)
    {
        return self.PyClose(context);
    }
}