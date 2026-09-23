using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;

namespace PySharp.Modules.Builtins;

/// <summary>
/// Wraps an async generator for __anext__() / asend() results.
/// Equivalent to CPython's async_generator_asend type.
/// When awaited (via __await__), drives the underlying async generator
/// and returns the yielded value, or raises StopAsyncIteration when exhausted.
/// Like CPython's AWAITABLE_STATE_* machinery the awaitable is single use:
/// one step — a yield, an exhaustion or an error — leaves it closed.
/// </summary>
public sealed class PyAsyncGeneratorASendObject : PyObject
{
    // CPython AWAITABLE_STATE_*: the awaitable is created in Init, passes
    // through Iter while a step runs, and every produced step - a yield, an
    // exhaustion or an error - leaves it Closed.
    internal enum DriveState
    {
        Init,
        Iter,
        Closed,
    }

    private readonly PyGeneratorObject _generator;

    public PyAsyncGeneratorASendObject(PyGeneratorObject generator, PyObject? initialSendValue = null)
    {
        _generator = generator;
        InitialSendValue = initialSendValue;
    }

    public override PyTypeObject DefaultPyType => PyAsyncGeneratorASendObjectType.Shared;
    public PyGeneratorObject Generator => _generator;
    // CPython ags_sendval: the value captured by asend(), used only when the
    // drive itself carries no value
    internal PyObject? InitialSendValue { get; }
    internal DriveState State { get; set; }
}

[PyType("async_generator_asend")]
public sealed partial class PyAsyncGeneratorASendObjectType : PyTypeObject<PyAsyncGeneratorASendObject>
{
    protected override PyResult Repr(PyCallContext context, PyAsyncGeneratorASendObject self)
    {
        return PyStrObject.FromString($"<async_generator_asend object at 0x{self.PyId:X16}>");
    }

    /// <summary>
    /// __await__() returns self — the asend object is its own iterator.
    /// </summary>
    protected override PyResult Await(PyCallContext context, PyAsyncGeneratorASendObject self)
    {
        return self;
    }

    /// <summary>
    /// __iter__() returns self for the iterator protocol.
    /// </summary>
    protected override PyResult Iter(PyCallContext context, PyAsyncGeneratorASendObject self)
    {
        return self;
    }

    /// <summary>
    /// CPython async_gen_asend_send / async_gen_asend_throw: a closed
    /// awaitable reports the reuse error, an INIT drive rejects an async
    /// generator that is already running, the asend() value only applies to
    /// a drive that carries no value of its own, and every step closes the
    /// awaitable.
    /// </summary>
    private static PyResult Step(PyCallContext context, PyAsyncGeneratorASendObject self, PyObject? value, PyObject? throwValue)
    {
        if (self.State is PyAsyncGeneratorASendObject.DriveState.Closed)
            return PyResult.RuntimeError(PySR.Runtime_AsyncGen_ReusedASend);

        if (self.State is PyAsyncGeneratorASendObject.DriveState.Init)
        {
            if (self.Generator.IsRunning)
            {
                self.State = PyAsyncGeneratorASendObject.DriveState.Closed;
                return PyResult.RuntimeError(PySR.Runtime_AsyncGen_AlreadyRunningANext);
            }

            if (value is null || value is PyNoneObject)
                value = self.InitialSendValue;

            self.State = PyAsyncGeneratorASendObject.DriveState.Iter;
        }

        try
        {
            PyResult result;
            if (throwValue is not null)
                result = self.Generator.PyThrow(context, throwValue);
            else if (value is not null && value is not PyNoneObject)
                result = self.Generator.PySend(context, value);
            else
                result = self.Generator.PyNext(context);

            return WrapStep(self, result);
        }
        catch
        {
            // a step that unwinds leaves the awaitable closed, like
            // CPython's asend_send/asend_throw error paths
            self.State = PyAsyncGeneratorASendObject.DriveState.Closed;
            throw;
        }
    }

    /// <summary>
    /// Maps one drive result onto the iterator protocol: an exhausted async
    /// generator surfaces as StopAsyncIteration, other errors pass through,
    /// and a yielded value ends the step with StopIteration. CPython's
    /// async_gen_unwrap_value returns NULL for all three outcomes, so each
    /// of them closes the awaitable.
    /// </summary>
    private static PyResult WrapStep(PyAsyncGeneratorASendObject self, PyResult result)
    {
        self.State = PyAsyncGeneratorASendObject.DriveState.Closed;

        if (result.IsStopIteration)
            return PyResult.FromException(PyStopAsyncIterationObjectType.Shared.Create());
        if (result.IsError)
            return result;
        return PyResult.StopIteration(result.Value);
    }

    /// <summary>
    /// __next__() drives the underlying async generator and returns the
    /// yielded value wrapped in StopIteration to signal completion to Send.
    /// When the async generator is exhausted, raises StopAsyncIteration.
    /// Non-StopIteration errors are propagated to the caller.
    /// </summary>
    protected override PyResult Next(PyCallContext context, PyAsyncGeneratorASendObject self)
    {
        return Step(context, self, null, null);
    }

    [PyMethod("send")]
    [PyFunctionParameters("value", "/")]
    private static PyResult Send(PyCallContext context, PyAsyncGeneratorASendObject self, PyArguments arguments)
    {
        return Step(context, self, arguments[0], null);
    }

    [PyMethod("throw")]
    [PyFunctionParameters("value", "/")]
    private static PyResult Throw(PyCallContext context, PyAsyncGeneratorASendObject self, PyArguments arguments)
    {
        return Step(context, self, null, arguments[0]);
    }

    /// <summary>
    /// CPython async_gen_asend_close: a closed awaitable is a no-op,
    /// otherwise GeneratorExit goes through the throw path, where
    /// StopIteration (the unwrapped yield value), StopAsyncIteration and
    /// GeneratorExit all count as success.
    /// </summary>
    [PyMethod("close")]
    [PyFunctionParameters()]
    private static PyResult Close(PyCallContext context, PyAsyncGeneratorASendObject self, PyArguments arguments)
    {
        if (self.State is PyAsyncGeneratorASendObject.DriveState.Closed)
            return PyNoneObject.None;

        try
        {
            var result = Step(context, self, null, PyGeneratorExitObjectType.Shared.Create());
            if (!result.IsError)
                return PyResult.RuntimeError(PySR.Runtime_AsyncGen_ASendIgnoredGeneratorExit);

            return IsCloseSuccess(result.Exception) ? PyNoneObject.None : result;
        }
        catch (PyRuntimeException error)
        {
            if (IsCloseSuccess(error.PyException))
                return PyNoneObject.None;
            throw;
        }
    }

    private static bool IsCloseSuccess(PyExceptionObject exception)
    {
        return PyStopIterationObjectType.Shared.IsInstance(exception)
            || PyStopAsyncIterationObjectType.Shared.IsInstance(exception)
            || PyGeneratorExitObjectType.Shared.IsInstance(exception);
    }
}

/// <summary>
/// Wraps an async generator for aclose() / athrow() results.
/// Equivalent to CPython's async_generator_athrow awaitable (PEP 525):
/// nothing runs until the object is driven, so the cleanup aclose() asks
/// for happens when the caller awaits it, and one object can be driven
/// only once (CPython AWAITABLE_STATE_INIT/ITER/CLOSED).
/// </summary>
public sealed class PyAsyncGeneratorAThrowObject : PyObject
{
    internal enum DriveState
    {
        Init,
        Iter,
        Closed,
    }

    private readonly PyGeneratorObject _generator;
    private readonly PyObject? _throwValue;

    private PyAsyncGeneratorAThrowObject(PyGeneratorObject generator, PyObject? throwValue)
    {
        _generator = generator;
        _throwValue = throwValue;
    }

    /// <summary>
    /// Creates the awaitable returned by aclose(): the first drive injects
    /// GeneratorExit into the generator.
    /// </summary>
    public static PyAsyncGeneratorAThrowObject CreateForClose(PyGeneratorObject generator)
    {
        return new PyAsyncGeneratorAThrowObject(generator, null);
    }

    /// <summary>
    /// Creates the awaitable returned by athrow(): the first drive injects
    /// the given exception into the generator.
    /// </summary>
    public static PyAsyncGeneratorAThrowObject CreateForThrow(PyGeneratorObject generator, PyObject throwValue)
    {
        return new PyAsyncGeneratorAThrowObject(generator, throwValue);
    }

    public override PyTypeObject DefaultPyType => PyAsyncGeneratorAThrowObjectType.Shared;
    internal PyGeneratorObject Generator => _generator;
    internal bool IsCloseMode => _throwValue is null;
    internal DriveState State { get; set; }
    internal PyObject TakeThrowValue() => _throwValue!;
}

[PyType("async_generator_athrow")]
public sealed partial class PyAsyncGeneratorAThrowObjectType : PyTypeObject<PyAsyncGeneratorAThrowObject>
{
    protected override PyResult Repr(PyCallContext context, PyAsyncGeneratorAThrowObject self)
    {
        return PyStrObject.FromString($"<async_generator_athrow object at 0x{self.PyId:X16}>");
    }

    /// <summary>
    /// __await__() returns self — the athrow object is its own iterator.
    /// </summary>
    protected override PyResult Await(PyCallContext context, PyAsyncGeneratorAThrowObject self)
    {
        return self;
    }

    protected override PyResult Iter(PyCallContext context, PyAsyncGeneratorAThrowObject self)
    {
        return self;
    }

    protected override PyResult Next(PyCallContext context, PyAsyncGeneratorAThrowObject self)
    {
        return Drive(context, self, null);
    }

    [PyMethod("send")]
    [PyFunctionParameters("value", "/")]
    private static PyResult Send(PyCallContext context, PyAsyncGeneratorAThrowObject self, PyArguments arguments)
    {
        return Drive(context, self, arguments[0]);
    }

    [PyMethod("throw")]
    [PyFunctionParameters("value", "/")]
    private static PyResult Throw(PyCallContext context, PyAsyncGeneratorAThrowObject self, PyArguments arguments)
    {
        if (self.State is PyAsyncGeneratorAThrowObject.DriveState.Closed)
            return PyResult.RuntimeError(PySR.Runtime_AsyncGen_ReusedAThrow);

        try
        {
            return WrapStep(self, self.Generator.PyThrow(context, arguments[0]));
        }
        catch
        {
            self.State = PyAsyncGeneratorAThrowObject.DriveState.Closed;
            throw;
        }
    }

    [PyMethod("close")]
    [PyFunctionParameters()]
    private static PyResult Close(PyCallContext context, PyAsyncGeneratorAThrowObject self, PyArguments arguments)
    {
        if (self.State is PyAsyncGeneratorAThrowObject.DriveState.Closed)
            return PyNoneObject.None;

        self.State = PyAsyncGeneratorAThrowObject.DriveState.Closed;
        var result = self.Generator.PyClose(context);
        return result.IsError ? result : PyNoneObject.None;
    }

    /// <summary>
    /// CPython async_gen_athrow_send: a finished generator completes the
    /// await immediately, the first drive injects GeneratorExit (aclose
    /// mode) or the pending exception (athrow mode), and later drives
    /// resume the generator.
    /// </summary>
    private static PyResult Drive(PyCallContext context, PyAsyncGeneratorAThrowObject self, PyObject? value)
    {
        if (self.State is PyAsyncGeneratorAThrowObject.DriveState.Closed)
            return PyResult.RuntimeError(PySR.Runtime_AsyncGen_ReusedAThrow);

        if (self.Generator.IsFinished)
        {
            self.State = PyAsyncGeneratorAThrowObject.DriveState.Closed;
            return PyResult.StopIteration();
        }

        try
        {
            if (self.State is PyAsyncGeneratorAThrowObject.DriveState.Init)
            {
                if (value is not null && value is not PyNoneObject)
                    return PyResult.RuntimeError(PySR.Runtime_Async_SendNonNoneAtFirst);

                if (self.Generator.IsRunning)
                {
                    self.State = PyAsyncGeneratorAThrowObject.DriveState.Closed;
                    return PyResult.RuntimeError(self.IsCloseMode
                        ? PySR.Runtime_AsyncGen_AlreadyRunningAClose
                        : PySR.Runtime_AsyncGen_AlreadyRunningAThrow);
                }

                if (self.IsCloseMode)
                {
                    // the awaited aclose() runs the cleanup here, and the
                    // await itself finishes once the generator exits
                    self.State = PyAsyncGeneratorAThrowObject.DriveState.Closed;
                    var closed = self.Generator.PyClose(context);
                    return closed.IsError ? closed : PyResult.StopIteration();
                }

                self.State = PyAsyncGeneratorAThrowObject.DriveState.Iter;
                return WrapStep(self, self.Generator.PyThrow(context, self.TakeThrowValue()));
            }

            var result = value is null || value is PyNoneObject
                ? self.Generator.PyNext(context)
                : self.Generator.PySend(context, value);
            return WrapStep(self, result);
        }
        catch
        {
            // a drive that unwinds leaves the awaitable closed, like
            // CPython's athrow_send error paths
            self.State = PyAsyncGeneratorAThrowObject.DriveState.Closed;
            throw;
        }
    }

    /// <summary>
    /// Maps one drive result onto the iterator protocol: a yielded value
    /// ends the step with StopIteration (leaving the object resumable), an
    /// exhausted generator surfaces as StopAsyncIteration, and any error
    /// closes the awaitable.
    /// </summary>
    private static PyResult WrapStep(PyAsyncGeneratorAThrowObject self, PyResult result)
    {
        if (result.IsStopIteration)
        {
            self.State = PyAsyncGeneratorAThrowObject.DriveState.Closed;
            return PyResult.FromException(PyStopAsyncIterationObjectType.Shared.Create());
        }
        if (result.IsError)
        {
            self.State = PyAsyncGeneratorAThrowObject.DriveState.Closed;
            return result;
        }

        self.State = PyAsyncGeneratorAThrowObject.DriveState.Iter;
        return PyResult.StopIteration(result.Value);
    }
}

[PyType("async_generator")]
public sealed partial class PyAsyncGeneratorObjectType : PyTypeObject<PyGeneratorObject>
{
    protected override PyResult Repr(PyCallContext context, PyGeneratorObject self)
    {
        return PyStrObject.FromString($"<async_generator object {self.Name} at 0x{self.PyId:X16}>");
    }

    protected override PyResult AIter(PyCallContext context, PyGeneratorObject self)
    {
        return self;
    }

    protected override PyResult ANext(PyCallContext context, PyGeneratorObject self)
    {
        return new PyAsyncGeneratorASendObject(self);
    }

    [PyMethod("asend")]
    [PyFunctionParameters("value", "/")]
    private static PyResult ASend(PyCallContext context, PyGeneratorObject self, PyArguments arguments)
    {
        // asend() must return an awaitable. Wrap the async generator
        // in a PyAsyncGeneratorASendObject with the send value.
        return new PyAsyncGeneratorASendObject(self, arguments[0]);
    }

    [PyMethod("athrow")]
    [PyFunctionParameters("value", "/")]
    private static PyResult AThrow(PyCallContext context, PyGeneratorObject self, PyArguments arguments)
    {
        // athrow() must return an awaitable. The async_generator_athrow
        // awaitable injects the exception only when the caller drives it.
        return PyAsyncGeneratorAThrowObject.CreateForThrow(self, arguments[0]);
    }

    [PyMethod("aclose")]
    [PyFunctionParameters()]
    private static PyResult AClose(PyCallContext context, PyGeneratorObject self, PyArguments arguments)
    {
        // aclose() must return an awaitable too: no cleanup runs until the
        // caller awaits it, matching CPython's deferred aclose()
        return PyAsyncGeneratorAThrowObject.CreateForClose(self);
    }

    // CPython gen_get_name/gen_set_name over the async generator: created
    // from the code object, writable to str only, deletion rejected
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
}
