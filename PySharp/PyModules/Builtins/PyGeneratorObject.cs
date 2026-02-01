using PySharp.Bytecodes;
using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;
using PySharp.PyRuntime.PyAttributes;
using System.Diagnostics;

namespace PySharp.PyModules.Builtins;

public abstract class PyGeneratorObject : PyObject, IPyObjectName
{
    public string Name { get; }
    public override PyTypeObject DefaultPyType => PyGeneratorObjectType.Shared;

    public PyGeneratorObject(string name)
    {
        Name = name;
    }

    internal abstract PyResult PyNext(PyCallContext context);
    internal abstract PyResult PySend(PyCallContext context, PyObject pyObject);
    internal abstract PyResult PyThrow(PyCallContext context, PyObject pyObject);
    internal abstract PyResult PyClose(PyCallContext context);
}

public sealed class PyGeneratorExpressionObject : PyGeneratorObject
{
    private readonly PyFrame _frame;
    private IEnumerator<PyObject>? _enumerator;
    private bool _first;

    public PyGeneratorExpressionObject(PyFrame frame, IEnumerator<PyObject> enumerator) : base("<genexpr>")
    {
        _frame = frame;
        _enumerator = enumerator;
        _first = true;
    }

    internal PyResult GetNext(PyCallContext context)
    {
        if (_enumerator is null)
            return PyResult.StopIteration();

        _first = false;

        _frame.Back = context.CurrentFrame;
        using var withFrame = context.WithFrame(_frame, () => _frame.Back = null);

        var result = _enumerator.MoveNext() ? _enumerator.Current : PyResult.StopIteration();
        if (result.IsError)
            _enumerator = null;
        return result;
    }

    internal override PyResult PyNext(PyCallContext context)
    {
        return GetNext(context);
    }

    internal override PyResult PySend(PyCallContext context, PyObject pyObject)
    {
        if (_first && pyObject is not PyNoneObject)
            return PyResult.TypeError(PySR.Runtime_Generator_SendNonNoneAtFirst);

        return GetNext(context);
    }

    internal override PyResult PyThrow(PyCallContext context, PyObject pyObject)
    {
        _enumerator = null;
        return PyResult.RaiseExceptionFromTypeOrInstance(pyObject); // TODO: do not raise inplace
    }

    internal override PyResult PyClose(PyCallContext context)
    {
        _enumerator = null;
        return PyNoneObject.None;
    }
}

public sealed class PyUserDefinedGeneratorObject : PyGeneratorObject
{
    internal readonly PyFrame _frame;
    internal Task? _task;

    internal PyUserDefinedGeneratorObject(string name, PyFrame frame, Task task) : base(name)
    {
        Debug.Assert(frame.Back is null);
        Debug.Assert(frame.FrameType is FrameType.YieldFunction or FrameType.YieldLambda);
        Debug.Assert(task.Status is TaskStatus.Created);

        _frame = frame;
        _task = task;
    }

    private PyResult StartTask(PyCallContext context)
    {
        Debug.Assert(_task is not null);
        Debug.Assert(!_frame._generatorCompleted);

        _frame._tcsWaitAtSend = new();

        _frame.Back = context.CurrentFrame;

        using var withFrame = context.WithFrame(_frame, () => _frame.Back = null);

        _task.Start();
        var result = _frame._tcsWaitAtSend.Task.Result;

        return result;
    }

    private PyResult ContinueTask(PyCallContext context, YieldCallerAction callerAction)
    {
        Debug.Assert(_task is not null);
        Debug.Assert(!_frame._generatorCompleted);

        _frame.Back = context.CurrentFrame;
        using var withFrame = context.WithFrame(_frame, () => _frame.Back = null);
        _frame._tcsWaitAtSend = new();
        Debug.Assert(_frame._tcsWaitAtStartOrYield is not null);
        _frame._tcsWaitAtStartOrYield.SetResult(callerAction);
        var result = _frame._tcsWaitAtSend.Task.Result;
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
            return PyResult.StopIteration(result.Value);
        }

        // yield
        return result;
    }

    internal override PyResult PyNext(PyCallContext context)
    {
        if (_task is null)
            return PyResult.StopIteration();

        if (_task.Status is TaskStatus.Created)
        {
            return StartTask(context);
        }

        var result = ContinueTask(context, YieldCallerAction.Next());
        return WrapIfReturn(result);
    }

    internal override PyResult PySend(PyCallContext context, PyObject pyObject)
    {
        if (_task is null)
            return PyResult.StopIteration();

        if (_task.Status is TaskStatus.Created)
        {
            if (pyObject is not PyNoneObject)
                return PyResult.TypeError(PySR.Runtime_Generator_SendNonNoneAtFirst);

            return StartTask(context);
        }

        var result = ContinueTask(context, YieldCallerAction.Send(pyObject));
        return WrapIfReturn(result);
    }

    internal override PyResult PyThrow(PyCallContext context, PyObject pyObject)
    {
        if (_task is null)
            return PyResult.StopIteration();

        if (_task.Status is TaskStatus.Created)
        {
            return PyResult.RaiseExceptionFromTypeOrInstance(pyObject); // TODO: do not raise inplace
        }

        var result = ContinueTask(context, YieldCallerAction.Throw(pyObject));
        return WrapIfReturn(result);
    }

    internal override PyResult PyClose(PyCallContext context)
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
            if (PyGeneratorExitObjectType.Shared.IsInstance(result.Exception))
                return PyNoneObject.None;

            return result;
        }

        if (!_frame._generatorCompleted)
            return PyResult.RuntimeError(PySR.Runtime_Generator_IgnoredGeneratorExit);

        return result;
    }
}

public sealed class PyBytecodeGeneratorObject : PyGeneratorObject
{
    private readonly PyFrame _frame;
    private readonly BytecodeVirtualMachine _vm;

    internal PyBytecodeGeneratorObject(string name, PyFrame frame, BytecodeVirtualMachine vm) : base(name)
    {
        _frame = frame;
        _vm = vm;
    }

    private PyResult Send(PyCallContext context, PyObject value)
    {
        if (_vm.RunToEnd)
            return PyResult.StopIteration();

        _frame.Back = context.CurrentFrame;
        using var withFrame = context.WithFrame(_frame);
        _vm.SetYieldReceivedValue(value);
        return _vm.Eval();
    }

    internal override PyResult PyClose(PyCallContext context)
    {
        if (_vm.RunToEnd)
            return PyNoneObject.None;

        _vm.ExceptionToRaise = PyGeneratorExitObjectType.Shared.Create();
        _frame.Back = context.CurrentFrame;
        using var withFrame = context.WithFrame(_frame);
        var result = _vm.Eval();

        if (result.IsError)
        {
            if (PyGeneratorExitObjectType.Shared.IsInstance(result.Exception))
                return PyNoneObject.None;

            return result;
        }

        if (!_vm.RunToEnd)
            // still yield value
            return PyResult.RuntimeError(PySR.Runtime_Generator_IgnoredGeneratorExit);

        return result;
    }

    internal override PyResult PyNext(PyCallContext context)
    {
        return Send(context, PyNoneObject.None);
    }

    internal override PyResult PySend(PyCallContext context, PyObject pyObject)
    {
        if (_vm.InstructionIndex is 1 /* first send */ && pyObject is not PyNoneObject)
            return PyResult.TypeError(PySR.Runtime_Generator_SendNonNoneAtFirst);

        return Send(context, pyObject);
    }

    internal override PyResult PyThrow(PyCallContext context, PyObject pyObject)
    {
        if (pyObject is PyTypeObject type)
        {
            if (!type.IsSubclassOf(PyBaseExceptionObjectType.Shared))
                return PyResult.TypeError(PySR.Runtime_Exception_NonException, pyObject.PyType.FullName);

            var excResult = type.Call(context);
            if (excResult.IsError)
                return excResult;

            pyObject = excResult.Value;
        }

        if (pyObject is not PyExceptionObject exc)
            return PyResult.TypeError(PySR.Runtime_Exception_NonException, pyObject.PyType.FullName);

        if (_vm.RunToEnd)
            return PyResult.FromException(exc);

        _vm.ExceptionToRaise = exc;
        _frame.Back = context.CurrentFrame;
        using var withFrame = context.WithFrame(_frame);
        var result = _vm.Eval();
        if (result.IsError)
            return result;

        if (_vm.RunToEnd)
            // return value
            return PyResult.StopIteration(result.Value);

        // yield value
        return result;
    }
}

public sealed class PyGeneratorObjectType : PyTypeObject<PyGeneratorObjectType, PyGeneratorObject>
{
    public override string Module => "builtins";
    public override string Name => "generator";

    public PyGeneratorObjectType()
    {
        AppendMethodDescriptor("send", Send);
        AppendMethodDescriptor("throw", Throw);
        AppendMethodDescriptor("close", Close);
    }

    protected override PyResult Repr(PyCallContext context, PyGeneratorObject self)
    {
        return PyStrObject.FromString($"<generator object {self.Name} at 0x{self.PyId:X16}>");
    }

    protected override PyResult Iter(PyCallContext context, PyGeneratorObject self)
    {
        return self;
    }

    protected override PyResult Next(PyCallContext context, PyGeneratorObject self)
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