using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;

namespace PySharp.Modules.Builtins;

/// <summary>
/// Wraps a __anext__() result to catch StopAsyncIteration and return a default value.
/// Equivalent to CPython's anext_awaitable type.
/// </summary>
internal sealed class PyAnextAwaitableObject : PyObject
{
    private readonly PyObject _iterator;
    private readonly PyObject _defaultValue;
    private PyObject? _innerAwaitable;

    internal PyAnextAwaitableObject(PyObject iterator, PyObject defaultValue)
    {
        _iterator = iterator;
        _defaultValue = defaultValue;
    }

    public override PyTypeObject DefaultPyType => PyAnextAwaitableObjectType.Shared;
    public PyObject Iterator => _iterator;
    public PyObject DefaultValue => _defaultValue;
    public PyObject? InnerAwaitable => _innerAwaitable;
    public void SetInnerAwaitable(PyObject awaitable) => _innerAwaitable = awaitable;
}

[PyType("anext_awaitable")]
internal sealed partial class PyAnextAwaitableObjectType : PyTypeObject<PyAnextAwaitableObject>
{
    /// <summary>
    /// __await__() calls __anext__() on the iterator, stores the inner awaitable,
    /// then returns self as the iterator that will be driven by Send.
    /// </summary>
    protected override PyResult Await(PyCallContext context, PyAnextAwaitableObject self)
    {
        var slot = self.Iterator.PyType.Slots.ANext;
        if (slot is null)
            return PyResult.TypeError(PySR.Runtime_Builtin_ANext_NotAsyncIterator, self.Iterator.PyType.FullName);

        var result = slot(context, self.Iterator);
        if (result.IsError)
            return result;

        self.SetInnerAwaitable(result.Value);
        return self;
    }

    /// <summary>
    /// __iter__() returns self for the iterator protocol.
    /// </summary>
    protected override PyResult Iter(PyCallContext context, PyAnextAwaitableObject self)
    {
        return self;
    }

    /// <summary>
    /// __next__() drives the inner awaitable (the result of __anext__()).
    /// If StopAsyncIteration is raised, returns the default value (wrapped in StopIteration).
    /// </summary>
    protected override PyResult Next(PyCallContext context, PyAnextAwaitableObject self)
    {
        var inner = self.InnerAwaitable;
        if (inner is null)
            return PyResult.StopIteration(self.DefaultValue);

        PyResult result;
        if (inner is PyGeneratorObject gen)
            result = gen.PyNext(context);
        else
        {
            var nextSlot = inner.PyType.Slots.Next;
            if (nextSlot is null)
                return PyResult.TypeError(PySR.Runtime_Sequence_IterReturnsNonIterator, inner.PyType.FullName);
            result = nextSlot(context, inner);
        }

        if (result.IsStopAsyncIteration)
            return PyResult.StopIteration(self.DefaultValue);
        return result;
    }
}
