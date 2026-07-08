using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;
using PySharp.Runtime.VirtualMachine;

namespace PySharp.Modules.Builtins;

public abstract class PyGeneratorObject : PyObject, IPyObjectName
{
    public string Name { get; }
    public override PyTypeObject DefaultPyType { get; }

    public PyGeneratorObject(PyTypeObject type, string name)
    {
        _pyType = type;
        DefaultPyType = type;
        Name = name;
    }

    internal abstract PyResult PyNext(PyCallContext context);
    internal abstract PyResult PySend(PyCallContext context, PyObject pyObject);
    internal abstract PyResult PyThrow(PyCallContext context, PyObject pyObject);
    internal abstract PyResult PyClose(PyCallContext context);
}

public sealed class PyBytecodeGeneratorObject : PyGeneratorObject
{
    private bool IsGeneratorRunning;
    private PyInternalFrame _frame;
    private BytecodeVirtualMachineStates _vmStates;

    private bool IsCoroutine => _pyType is PyCoroutineObjectType;

    internal PyBytecodeGeneratorObject(PyTypeObject type, string name, PyInternalFrame frame, BytecodeVirtualMachineStates states) : base(type, name)
    {
        _frame = frame;
        _vmStates = states;
    }

    private PyResult Send(PyCallContext context, PyObject value)
    {
        if (_vmStates.RunToEnd)
            return PyResult.StopIteration();

        IsGeneratorRunning = true;
        using var withFrame = context.WithFrame(ref _frame, dispose: false);
        _vmStates.SetYieldReceivedValue(value);
        var result = BytecodeVirtualMachine.Eval(context, ref _vmStates);
        _frame.InstructionIndex = context.CurrentInternalFrame.InstructionIndex;
        if (result.IsError)
            return result;

        if (_vmStates.RunToEnd)
            return PyResult.StopIteration(result.Value);

        return result;
    }

    internal override PyResult PyClose(PyCallContext context)
    {
        if (!IsGeneratorRunning)
            _vmStates.RunToEnd = true;

        if (_vmStates.RunToEnd)
            return PyNoneObject.None;

        _vmStates.ExceptionToRaise = PyGeneratorExitObjectType.Shared.Create();
        using var withFrame = context.WithFrame(ref _frame, dispose: false);
        var result = BytecodeVirtualMachine.Eval(context, ref _vmStates);
        _frame.InstructionIndex = context.CurrentInternalFrame.InstructionIndex;

        if (result.IsError)
        {
            if (PyGeneratorExitObjectType.Shared.IsInstance(result.Exception))
                return PyNoneObject.None;

            return result;
        }

        if (!_vmStates.RunToEnd)
        {
            // still yield or await value
            return PyResult.RuntimeError(IsCoroutine ?
                PySR.Runtime_Async_IgnoredGeneratorExit : PySR.Runtime_Generator_IgnoredGeneratorExit);
        }

        return result;
    }

    internal override PyResult PyNext(PyCallContext context)
    {
        return Send(context, PyNoneObject.None);
    }

    internal override PyResult PySend(PyCallContext context, PyObject pyObject)
    {
        if (!IsGeneratorRunning && pyObject is not PyNoneObject)
        {
            return PyResult.TypeError(IsCoroutine ?
                PySR.Runtime_Async_SendNonNoneAtFirst : PySR.Runtime_Generator_SendNonNoneAtFirst);
        }

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

        if (_vmStates.RunToEnd)
            return PyResult.FromException(exc);

        _vmStates.ExceptionToRaise = exc;
        using var withFrame = context.WithFrame(ref _frame, dispose: false);
        var result = BytecodeVirtualMachine.Eval(context, ref _vmStates);
        _frame.InstructionIndex = context.CurrentInternalFrame.InstructionIndex;
        if (result.IsError)
            return result;

        if (_vmStates.RunToEnd)
            // return value
            return PyResult.StopIteration(result.Value);

        // yield or await value
        return result;
    }
}

[PyType("generator")]
public sealed partial class PyGeneratorObjectType : PyTypeObject<PyGeneratorObject>
{
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

    [PyMethod("send")]
    [PyFunctionParameters("value")]
    private static PyResult Send(PyCallContext context, PyGeneratorObject self, PyArguments arguments)
    {
        if (arguments[0] is PyNoneObject)
            return self.PyNext(context);

        return self.PySend(context, arguments[0]);
    }

    [PyMethod("throw")]
    [PyFunctionParameters("value")]
    private static PyResult Throw(PyCallContext context, PyGeneratorObject self, PyArguments arguments)
    {
        return self.PyThrow(context, arguments[0]);
    }

    [PyMethod("close")]
    [PyFunctionParameters()]
    private static PyResult Close(PyCallContext context, PyGeneratorObject self, PyArguments arguments)
    {
        return self.PyClose(context);
    }
}
