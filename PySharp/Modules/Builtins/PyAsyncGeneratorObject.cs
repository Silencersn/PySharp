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

    public PyAsyncGeneratorASendObject(PyGeneratorObject generator)
    {
        _generator = generator;
    }

    public override PyTypeObject DefaultPyType => PyAsyncGeneratorASendObjectType.Shared;
    public PyGeneratorObject Generator => _generator;
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
    /// __next__() drives the underlying async generator and returns the
    /// yielded value wrapped in StopIteration to signal completion to Send.
    /// When the async generator is exhausted, raises StopAsyncIteration.
    /// Non-StopIteration errors (e.g. exceptions inside the generator)
    /// are propagated to the caller, matching CPython behavior.
    /// </summary>
    protected override PyResult Next(PyCallContext context, PyAsyncGeneratorASendObject self)
    {
        var result = self.Generator.PyNext(context);
        if (result.IsStopIteration)
            // Async generator exhausted → the awaitable raises StopAsyncIteration
            return PyResult.FromException(PyStopAsyncIterationObjectType.Shared.Create());
        if (result.IsError)
            // Propagate non-StopIteration errors (e.g. TypeError, ValueError)
            return result;
        // Wrap the yield value in StopIteration — this signals to the Send opcode
        // that the "await" completed with a value, jumping to afterAwaitLabel.
        return PyResult.StopIteration(result.Value);
    }

    [PyMethod("send")]
    [PyFunctionParameters("value")]
    private static PyResult Send(PyCallContext context, PyAsyncGeneratorASendObject self, PyArguments arguments)
    {
        var result = self.Generator.PySend(context, arguments[0]);
        if (result.IsStopIteration)
            return PyResult.FromException(PyStopAsyncIterationObjectType.Shared.Create());
        if (result.IsError)
            return result;
        return PyResult.StopIteration(result.Value);
    }

    [PyMethod("throw")]
    [PyFunctionParameters("value")]
    private static PyResult Throw(PyCallContext context, PyAsyncGeneratorASendObject self, PyArguments arguments)
    {
        var result = self.Generator.PyThrow(context, arguments[0]);
        if (result.IsStopIteration)
            return PyResult.FromException(PyStopAsyncIterationObjectType.Shared.Create());
        if (result.IsError)
            return result;
        return PyResult.StopIteration(result.Value);
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
        return self.PySend(context, arguments[0]);
    }

    [PyMethod("athrow")]
    [PyFunctionParameters("value")]
    private static PyResult AThrow(PyCallContext context, PyGeneratorObject self, PyArguments arguments)
    {
        return self.PyThrow(context, arguments[0]);
    }

    [PyMethod("aclose")]
    [PyFunctionParameters()]
    private static PyResult AClose(PyCallContext context, PyGeneratorObject self, PyArguments arguments)
    {
        return self.PyClose(context);
    }
}
