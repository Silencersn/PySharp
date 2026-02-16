using PySharp.Compilation.Bytecodes;
using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.Calls.Extensions;
using PySharp.Runtime.PyAttributes;
using System.Diagnostics;

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

        IsGeneratorRunning = true;
        _frame.Back = context.CurrentFrame;
        using var withFrame = context.WithFrame(_frame);
        _vm.SetYieldReceivedValue(value);
        var result = _vm.Eval();
        if (result.IsError)
            return result;

        if (_vm.RunToEnd)
            return PyResult.StopIteration(result.Value);

        return result;
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