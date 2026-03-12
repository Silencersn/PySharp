using PySharp.Compilation.Bytecodes;
using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.Calls.Extensions;
using PySharp.Runtime.PyAttributes;

namespace PySharp.Modules.Builtins;

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

public sealed class PyBytecodeGeneratorObject : PyGeneratorObject
{
    private bool IsGeneratorRunning;
    private PyInternalFrame _frame;
    private BytecodeVirtualMachineStates _vmStates;

    internal PyBytecodeGeneratorObject(string name, PyInternalFrame frame, BytecodeVirtualMachineStates states) : base(name)
    {
        _frame = frame;
        _vmStates = states;
    }

    private PyResult Send(PyCallContext context, PyObject value)
    {
        if (_vmStates.RunToEnd)
            return PyResult.StopIteration();

        IsGeneratorRunning = true;
        _frame.BackFrameIndex = context.FrameState.CurrentFrameIndex;
        using var withFrame = context.WithFrame(ref _frame, dispose: false);
        _vmStates.SetYieldReceivedValue(value);
        var result = BytecodeVirtualMachine.Eval(ref _vmStates);
        _frame.InstructionIndex = context.FrameState.CurrentInternalFrame.InstructionIndex;
        if (result.IsError)
            return result;

        if (_vmStates.RunToEnd)
            return PyResult.StopIteration(result.Value);

        return result;
    }

    internal override PyResult PyClose(PyCallContext context)
    {
        if (_vmStates.RunToEnd)
            return PyNoneObject.None;

        _vmStates.ExceptionToRaise = PyGeneratorExitObjectType.Shared.Create();
        _frame.BackFrameIndex = context.FrameState.CurrentFrameIndex;
        using var withFrame = context.WithFrame(ref _frame, dispose: false);
        var result = BytecodeVirtualMachine.Eval(ref _vmStates);
        _frame.InstructionIndex = context.FrameState.CurrentInternalFrame.InstructionIndex;

        if (result.IsError)
        {
            if (PyGeneratorExitObjectType.Shared.IsInstance(result.Exception))
                return PyNoneObject.None;

            return result;
        }

        if (!_vmStates.RunToEnd)
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
        if (!IsGeneratorRunning && pyObject is not PyNoneObject)
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

        if (_vmStates.RunToEnd)
            return PyResult.FromException(exc);

        _vmStates.ExceptionToRaise = exc;
        _frame.BackFrameIndex = context.FrameState.CurrentFrameIndex;
        using var withFrame = context.WithFrame(ref _frame, dispose: false);
        var result = BytecodeVirtualMachine.Eval(ref _vmStates);
        _frame.InstructionIndex = context.FrameState.CurrentInternalFrame.InstructionIndex;
        if (result.IsError)
            return result;

        if (_vmStates.RunToEnd)
            // return value
            return PyResult.StopIteration(result.Value);

        // yield value
        return result;
    }
}

[PyType("generator")]
public sealed partial class PyGeneratorObjectType : PyTypeObject<PyGeneratorObjectType, PyGeneratorObject>
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
    [PyFunctionArgsDef("value")]
    private static PyResult Send(PyCallContext context, PyGeneratorObject self, PyArguments arguments)
    {
        if (arguments[0] is PyNoneObject)
            return self.PyNext(context);

        return self.PySend(context, arguments[0]);
    }

    [PyMethod("throw")]
    [PyFunctionArgsDef("value")]
    private static PyResult Throw(PyCallContext context, PyGeneratorObject self, PyArguments arguments)
    {
        return self.PyThrow(context, arguments[0]);
    }

    [PyMethod("close")]
    [PyFunctionArgsDef()]
    private static PyResult Close(PyCallContext context, PyGeneratorObject self, PyArguments arguments)
    {
        return self.PyClose(context);
    }
}