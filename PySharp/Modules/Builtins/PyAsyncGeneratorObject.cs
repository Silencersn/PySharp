using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;
using System.Diagnostics;

namespace PySharp.Modules.Builtins;

/// <summary>
/// Wraps an async generator for __anext__() / asend() results.
/// Equivalent to CPython's async_generator_asend type.
/// When awaited (via __await__), drives the underlying async generator
/// and returns the yielded value, or raises StopAsyncIteration when exhausted.
/// </summary>
public sealed class PyAsyncGeneratorASendObject : PyObject
{
    private readonly PyGeneratorObject _generator;
    private PyObject? _initialSendValue;
    private bool _initialSendUsed;
    private PyObject? _throwValue;

    private PyAsyncGeneratorASendObject(PyGeneratorObject generator, PyObject? initialSendValue, PyObject? throwValue)
        : this(generator, initialSendValue)
    {
        _throwValue = throwValue;
    }

    public PyAsyncGeneratorASendObject(PyGeneratorObject generator, PyObject? initialSendValue = null)
    {
        _generator = generator;
        _initialSendValue = initialSendValue;
    }

    /// <summary>
    /// Creates an async_generator_asend for athrow mode.
    /// When driven, throws the given exception into the generator.
    /// </summary>
    public static PyAsyncGeneratorASendObject CreateForThrow(PyGeneratorObject generator, PyObject throwValue)
    {
        return new PyAsyncGeneratorASendObject(generator, null, throwValue);
    }

    public override PyTypeObject DefaultPyType => PyAsyncGeneratorASendObjectType.Shared;
    public PyGeneratorObject Generator => _generator;
    internal bool HasInitialSend => _initialSendValue is not null && !_initialSendUsed;
    internal PyObject? TakeInitialSend()
    {
        _initialSendUsed = true;
        return _initialSendValue;
    }
    internal PyObject? ThrowValue => _throwValue;
    internal void ClearThrow() => _throwValue = null;
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

    private static PyResult DriveGenerator(PyAsyncGeneratorASendObject self, PyCallContext context, PyObject? value = null)
    {
        PyResult result;
        if (self.ThrowValue is not null)
        {
            // athrow mode: inject the exception on first drive
            var exc = self.ThrowValue;
            self.ClearThrow(); // clear so subsequent drives use PyNext
            result = self.Generator.PyThrow(context, exc);
        }
        else if (self.HasInitialSend)
        {
            var sendValue = self.TakeInitialSend()!;
            result = self.Generator.PySend(context, sendValue);
        }
        else if (value is not null)
        {
            result = self.Generator.PySend(context, value);
        }
        else
        {
            result = self.Generator.PyNext(context);
        }
        return WrapResult(result);
    }

    private static PyResult WrapResult(PyResult result)
    {
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
    /// If constructed with an initial send value (asend mode), the first
    /// drive uses PySend instead of PyNext.
    /// Non-StopIteration errors are propagated to the caller.
    /// </summary>
    protected override PyResult Next(PyCallContext context, PyAsyncGeneratorASendObject self)
    {
        return DriveGenerator(self, context);
    }

    [PyMethod("send")]
    [PyFunctionParameters("value")]
    private static PyResult Send(PyCallContext context, PyAsyncGeneratorASendObject self, PyArguments arguments)
    {
        return DriveGenerator(self, context, arguments[0]);
    }

    [PyMethod("throw")]
    [PyFunctionParameters("value")]
    private static PyResult Throw(PyCallContext context, PyAsyncGeneratorASendObject self, PyArguments arguments)
    {
        // throw() always calls PyThrow, never PySend.
        // _throwValue (from CreateForThrow) is consumed on first drive;
        // subsequent throw() calls use the argument directly.
        if (self.ThrowValue is not null)
        {
            var exc = self.ThrowValue;
            self.ClearThrow();
            return WrapResult(self.Generator.PyThrow(context, exc));
        }
        return WrapResult(self.Generator.PyThrow(context, arguments[0]));
    }

    [PyMethod("close")]
    [PyFunctionParameters()]
    private static PyResult Close(PyCallContext context, PyAsyncGeneratorASendObject self, PyArguments arguments)
    {
        return self.Generator.PyClose(context);
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
    [PyFunctionParameters("value")]
    private static PyResult ASend(PyCallContext context, PyGeneratorObject self, PyArguments arguments)
    {
        // asend() must return an awaitable. Wrap the async generator
        // in a PyAsyncGeneratorASendObject with the send value.
        return new PyAsyncGeneratorASendObject(self, arguments[0]);
    }

    [PyMethod("athrow")]
    [PyFunctionParameters("value")]
    private static PyResult AThrow(PyCallContext context, PyGeneratorObject self, PyArguments arguments)
    {
        // athrow() must return an awaitable. Wrap in PyAsyncGeneratorASendObject
        // that throws the exception into the generator when driven.
        return PyAsyncGeneratorASendObject.CreateForThrow(self, arguments[0]);
    }

    [PyMethod("aclose")]
    [PyFunctionParameters()]
    private static PyResult AClose(PyCallContext context, PyGeneratorObject self, PyArguments arguments)
    {
        return self.PyClose(context);
    }
}
