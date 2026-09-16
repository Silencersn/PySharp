using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;
using PySharp.Runtime.VirtualMachine;
using System.Diagnostics;

namespace PySharp.Modules.Builtins;

public abstract class PyGeneratorObject : PyObject, IPyObjectName
{
    public string Name { get; }
    public override PyTypeObject DefaultPyType { get; }
    internal PyStrObject? _pyNameOverride;
    internal PyStrObject? _pyQualNameOverride;

    public PyGeneratorObject(PyTypeObject type, string name)
    {
        _pyType = type;
        DefaultPyType = type;
        Name = name;
    }

    // CPython gi_qualname: the creating code object's co_qualname
    internal virtual string QualName => Name;

    internal abstract PyResult PyNext(PyCallContext context);
    internal abstract PyResult PySend(PyCallContext context, PyObject pyObject);
    internal abstract PyResult PyThrow(PyCallContext context, PyObject pyObject);
    internal abstract PyResult PyClose(PyCallContext context);

    // CPython gen_throw's deprecated (type, value, tb) form: warns, then
    // normalizes — an exception-class type instantiates with the value
    // (no value or None → no-arg construction; an exception instance
    // value wins as-is), an exception-instance type rejects a separate
    // value, and the traceback argument must be None or a traceback.
    internal PyResult PyThrow(PyCallContext context, PyObject typeArg, PyObject valueArg, PyObject tbArg)
    {
        var warnResult = context.Warn(PyDeprecationWarningObjectType.Shared, PySR.Runtime_Generator_ThrowSignatureDeprecated);
        if (warnResult.IsError)
            return warnResult;

        if (tbArg is not PyNoneObject && tbArg is not PyTracebackObject)
            return PyResult.TypeError(PySR.Runtime_Generator_ThrowThirdArgTraceback);

        var resolved = ResolveThrow(context, typeArg, valueArg is PyNoneObject ? null : valueArg, out var exc);
        if (resolved.IsError)
            return resolved;

        return PyThrow(context, exc);
    }

    internal static PyResult ResolveThrow(PyCallContext context, PyObject typeArg, PyObject? valueArg, out PyExceptionObject exc)
    {
        if (typeArg is PyTypeObject type)
        {
            if (!type.IsSubclassOf(PyBaseExceptionObjectType.Shared))
                return ThrowArgTypeError(typeArg, out exc);

            if (valueArg is null)
            {
                var createdResult = type.Call(context);
                if (createdResult.IsError)
                {
                    exc = default!;
                    return createdResult;
                }
                exc = (PyExceptionObject)createdResult.Value;
                return default;
            }

            // an exception instance value is used as-is, any other
            // value instantiates the class with that value
            if (valueArg is PyExceptionObject instance)
            {
                exc = instance;
                return default;
            }

            var wrappedResult = type.Call(context, [valueArg]);
            if (wrappedResult.IsError)
            {
                exc = default!;
                return wrappedResult;
            }
            exc = (PyExceptionObject)wrappedResult.Value;
            return default;
        }

        if (typeArg is PyExceptionObject)
        {
            if (valueArg is not null)
            {
                exc = default!;
                return PyResult.TypeError(PySR.Runtime_Exception_InstanceSeparateValue);
            }
            exc = (PyExceptionObject)typeArg;
            return default;
        }

        return ThrowArgTypeError(typeArg, out exc);
    }

    private static PyResult ThrowArgTypeError(PyObject typeArg, out PyExceptionObject exc)
    {
        exc = default!;
        return PyResult.TypeError(PySR.Runtime_Exception_NonException, typeArg.PyType.FullName);
    }
}

public sealed class PyBytecodeGeneratorObject : PyGeneratorObject
{
    private bool IsGeneratorRunning;
    // CPython gen_send_ex2's gi_running: set while the frame is being
    // evaluated; a re-entrant resume raises ValueError instead of
    // corrupting the frame (the old unguarded path crashed the process).
    private bool IsExecuting;
    private PyInternalFrame _frame;
    private BytecodeVirtualMachineStates _vmStates;

    private bool IsCoroutine => _pyType is PyCoroutineObjectType;
    private bool IsAsyncGenerator => _pyType is PyAsyncGeneratorObjectType;

    internal PyBytecodeGeneratorObject(PyTypeObject type, string name, PyInternalFrame frame, BytecodeVirtualMachineStates states) : base(type, name)
    {
        _frame = frame;
        _vmStates = states;
    }

    internal override string QualName => _frame.CodeObject?.QualName ?? Name;

    /// <summary>
    /// PEP 479: a StopIteration escaping the generator frame (raised by the
    /// body or left uncaught after a throw) must not be mistaken for normal
    /// exhaustion; replace it with RuntimeError and link the original
    /// StopIteration as __cause__.
    /// </summary>
    private PyResult ConvertStopIteration(PyResult result)
    {
        Debug.Assert(result.IsError);

        if (!result.IsStopIteration)
            return result;

        var message = IsCoroutine ? PySR.Runtime_Async_CoroutineRaisedStopIteration
            : IsAsyncGenerator ? PySR.Runtime_AsyncGen_RaisedStopIteration
            : PySR.Runtime_Generator_RaisedStopIteration;
        var error = PyResult.RuntimeError(message);
        error.Exception!.Cause = result.Exception;
        return error;
    }

    private PyResult AlreadyExecutingError()
    {
        var message = IsCoroutine ? PySR.Runtime_Generator_CoroutineAlreadyExecuting
            : IsAsyncGenerator ? PySR.Runtime_Generator_AsyncAlreadyExecuting
            : PySR.Runtime_Generator_AlreadyExecuting;
        return PyResult.ValueError(message);
    }

    private PyResult Send(PyCallContext context, PyObject value)
    {
        if (_vmStates.RunToEnd)
            return PyResult.StopIteration();

        if (IsExecuting)
            return AlreadyExecutingError();

        IsGeneratorRunning = true;
        IsExecuting = true;
        try
        {
            using var withFrame = context.WithFrame(ref _frame, dispose: false);
            _vmStates.SetYieldReceivedValue(value);
            var result = ResumeEval(context);
            _frame.InstructionIndex = context.CurrentInternalFrame.InstructionIndex;
            if (result.IsError)
                return ConvertStopIteration(result);

            if (_vmStates.RunToEnd)
                return PyResult.StopIteration(result.Value);

            return result;
        }
        finally
        {
            IsExecuting = false;
        }
    }

    internal override PyResult PyClose(PyCallContext context)
    {
        if (!IsGeneratorRunning)
            _vmStates.RunToEnd = true;

        if (_vmStates.RunToEnd)
            return PyNoneObject.None;

        if (IsExecuting)
            return AlreadyExecutingError();

        _vmStates.ExceptionToRaise = PyGeneratorExitObjectType.Shared.Create();
        IsExecuting = true;
        try
        {
            using var withFrame = context.WithFrame(ref _frame, dispose: false);
            var result = ResumeEval(context);
            _frame.InstructionIndex = context.CurrentInternalFrame.InstructionIndex;

            if (result.IsError)
            {
                if (PyGeneratorExitObjectType.Shared.IsInstance(result.Exception))
                    return PyNoneObject.None;

                return ConvertStopIteration(result);
            }

            if (!_vmStates.RunToEnd)
            {
                // still yield or await value
                return PyResult.RuntimeError(IsCoroutine ?
                    PySR.Runtime_Async_IgnoredGeneratorExit : PySR.Runtime_Generator_IgnoredGeneratorExit);
            }

            return result;
        }
        finally
        {
            IsExecuting = false;
        }
    }

    internal override PyResult PyNext(PyCallContext context)
    {
        return Send(context, PyNoneObject.None);
    }

    internal override PyResult PySend(PyCallContext context, PyObject pyObject)
    {
        // CPython gen_send_ex2: a closed/exhausted generator stops
        // iteration immediately; only a never-started generator
        // rejects a non-None send value
        if (_vmStates.RunToEnd)
            return PyResult.StopIteration();

        if (!IsGeneratorRunning && pyObject is not PyNoneObject)
        {
            return PyResult.TypeError(IsCoroutine ?
                PySR.Runtime_Async_SendNonNoneAtFirst : PySR.Runtime_Generator_SendNonNoneAtFirst);
        }

        return Send(context, pyObject);
    }

    internal override PyResult PyThrow(PyCallContext context, PyObject pyObject)
    {
        var resolved = ResolveThrow(context, pyObject, null, out var singleExc);
        if (resolved.IsError)
            return resolved;

        return ThrowResolved(context, singleExc);
    }

    private PyResult ThrowResolved(PyCallContext context, PyExceptionObject exc)
    {
        if (_vmStates.RunToEnd)
            return PyResult.FromException(exc);

        // CPython gen_throw: an exception thrown into a never-started
        // generator propagates straight to the caller (no frame is
        // running that could catch it) and the generator is closed.
        if (!IsGeneratorRunning)
        {
            _vmStates.RunToEnd = true;
            return PyResult.FromException(exc);
        }

        if (IsExecuting)
            return AlreadyExecutingError();

        _vmStates.ExceptionToRaise = exc;
        IsExecuting = true;
        try
        {
            using var withFrame = context.WithFrame(ref _frame, dispose: false);
            var result = ResumeEval(context);
            _frame.InstructionIndex = context.CurrentInternalFrame.InstructionIndex;
            if (result.IsError)
                return ConvertStopIteration(result);

            if (_vmStates.RunToEnd)
                // return value
                return PyResult.StopIteration(result.Value);

            // yield or await value
            return result;
        }
        finally
        {
            IsExecuting = false;
        }
    }

    // Generator resume shares the caller's context: restore the handled
    // exception observed on entry when the generator suspends or exits, so
    // its handler state never leaks onto the caller's chain (a bare raise
    // after resuming must see the caller's active exception, not a dead one).
    private PyResult ResumeEval(PyCallContext context)
    {
        var savedHandledException = context.HandledException;
        try
        {
            return BytecodeVirtualMachine.Eval(context, ref _vmStates);
        }
        finally
        {
            context.HandledException = savedHandledException;
        }
    }
}

[PyType("generator")]
public sealed partial class PyGeneratorObjectType : PyTypeObject<PyGeneratorObject>
{
    protected override PyResult Repr(PyCallContext context, PyGeneratorObject self)
    {
        return PyStrObject.FromString($"<generator object {self.Name} at 0x{self.PyId:X16}>");
    }

    // CPython gen_get_name/gen_set_name: created from the code object,
    // writable to str only, deletion rejected
    [PyProperty(PySpecialNames.Name)]
    private static PyResult Get_Name(PyCallContext context, PyGeneratorObject self)
    {
        return self._pyNameOverride ?? PyStrObject.FromString(self.Name);
    }

    [PyProperty(PySpecialNames.Name, Type = PyPropertyMethodType.Setter)]
    private static PyResult Set_Name(PyCallContext context, PyGeneratorObject self, PyObject value)
    {
        if (value is not PyStrObject str)
            return PyResult.TypeError("__name__ must be set to a string object");

        self._pyNameOverride = str;
        return PyNoneObject.None;
    }

    [PyProperty(PySpecialNames.Name, Type = PyPropertyMethodType.Deleter)]
    private static PyResult Delete_Name(PyCallContext context, PyGeneratorObject self)
    {
        return PyResult.TypeError("__name__ must be set to a string object");
    }

    [PyProperty(PySpecialNames.QualName)]
    private static PyResult Get_QualName(PyCallContext context, PyGeneratorObject self)
    {
        return self._pyQualNameOverride ?? PyStrObject.FromString(self.QualName);
    }

    [PyProperty(PySpecialNames.QualName, Type = PyPropertyMethodType.Setter)]
    private static PyResult Set_QualName(PyCallContext context, PyGeneratorObject self, PyObject value)
    {
        if (value is not PyStrObject str)
            return PyResult.TypeError("__qualname__ must be set to a string object");

        self._pyQualNameOverride = str;
        return PyNoneObject.None;
    }

    [PyProperty(PySpecialNames.QualName, Type = PyPropertyMethodType.Deleter)]
    private static PyResult Delete_QualName(PyCallContext context, PyGeneratorObject self)
    {
        return PyResult.TypeError("__qualname__ must be set to a string object");
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

    [PyMethod("throw", Order = 1)]
    [PyFunctionParameters("value", "/")]
    private static PyResult Throw(PyCallContext context, PyGeneratorObject self, PyArguments arguments)
    {
        return self.PyThrow(context, arguments[0]);
    }

    [PyMethod("throw", Order = 2)]
    [PyFunctionParameters("type", "value", "/")]
    private static PyResult Throw2(PyCallContext context, PyGeneratorObject self, PyArguments arguments)
    {
        return self.PyThrow(context, arguments[0], arguments[1], PyNoneObject.None);
    }

    [PyMethod("throw", Order = 3)]
    [PyFunctionParameters("type", "value", "tb", "/")]
    private static PyResult Throw3(PyCallContext context, PyGeneratorObject self, PyArguments arguments)
    {
        return self.PyThrow(context, arguments[0], arguments[1], arguments[2]);
    }

    [PyMethod("close")]
    [PyFunctionParameters()]
    private static PyResult Close(PyCallContext context, PyGeneratorObject self, PyArguments arguments)
    {
        return self.PyClose(context);
    }
}
