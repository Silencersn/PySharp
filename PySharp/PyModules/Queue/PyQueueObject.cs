using PySharp.PyModules.Builtins;
using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;
using PySharp.PyRuntime.PyAttributes;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace PySharp.PyModules.Queue;

public sealed partial class PyQueueObject : PyObject
{
    private readonly ConcurrentQueue<PyObject> _queue;
    private readonly BlockingCollection<PyObject> _collection;
    private TaskCompletionSource? _source;
    private int _unfinished_tasks;

    public override PyTypeObject DefaultPyType => PyQueueObjectType.Shared;

    internal PyQueueObject(int maxSize)
    {
        _queue = [];
        if (maxSize <= 0)
            _collection = new BlockingCollection<PyObject>(_queue);
        else
            _collection = new BlockingCollection<PyObject>(_queue, maxSize);
        _source = null;
        _unfinished_tasks = 0;
    }

    [PyFunctionArgsDef()]
    internal PyObject? QSizeImpl(PyArguments arguments)
    {
        return PyIntObject.FromInteger(PyQSize());
    }

    [PyFunctionArgsDef()]
    internal PyObject? EmptyImpl(PyArguments arguments)
    {
        return PyBoolObject.FromBoolean(PyEmpty());
    }

    [PyFunctionArgsDef()]
    internal PyObject? FullImpl(PyArguments arguments)
    {
        return PyBoolObject.FromBoolean(PyFull());
    }

    [PyFunctionArgsDef("item", "block=True", "timeout=None")]
    internal PyObject? PutImpl(PyArguments arguments)
    {
        if (!PyInteropService.TryGetBool(arguments[1], out var block))
            return null;

        double? timeout;
        if (arguments[2] is PyNoneObject)
            timeout = null;
        else if (PyInteropService.TryGetFloat(arguments[2], out var value)) // TODO: negative value
            timeout = value;
        else
            return null;

        var ex = PyPut(arguments[0], block, timeout);
        if (ex is not null)
            return PyVirtualMachine.RaiseException(ex);
        return PyNoneObject.None;
    }

    [PyFunctionArgsDef("item")]
    internal PyObject? PutNoWaitImpl(PyArguments arguments)
    {
        var ex = PyPut(arguments[0], false, null);
        if (ex is not null)
            return PyVirtualMachine.RaiseException(ex);
        return PyNoneObject.None;
    }

    [PyFunctionArgsDef("block=True", "timeout=None")]
    internal PyObject? GetImpl(PyArguments arguments)
    {
        if (!PyInteropService.TryGetBool(arguments[0], out var block))
            return null;

        double? timeout;
        if (arguments[1] is PyNoneObject)
            timeout = null;
        else if (PyInteropService.TryGetFloat(arguments[1], out var value)) // TODO: negative value
            timeout = value;
        else
            return null;

        var (item, ex) = PyGet(block, timeout);
        if (ex is not null)
            return PyVirtualMachine.RaiseException(ex);

        Debug.Assert(item is not null);
        return item;
    }

    [PyFunctionArgsDef()]
    internal PyObject? GetNoWaitImpl(PyArguments arguments)
    {
        var (item, ex) = PyGet(false, null);
        if (ex is not null)
            return PyVirtualMachine.RaiseException(ex);

        Debug.Assert(item is not null);
        return item;
    }

    [PyFunctionArgsDef()]
    internal PyObject? TaskDoneImpl(PyArguments arguments)
    {
        if (PyTryTaskDone())
            return PyNoneObject.None;

        return PyVirtualMachine.RaiseValueError("task_done() called too many times");
    }

    [PyFunctionArgsDef()]
    internal PyObject? JoinImpl(PyArguments arguments)
    {
        PyJoin();
        return PyNoneObject.None;
    }
}

public sealed class PyQueueObjectType : PyPrimitiveTypeObject<PyQueueObjectType, PyQueueObject>
{
    public override string Name => "Queue";
    public override string FullName => $"queue.{Name}";

    public PyQueueObjectType()
    {
        AppendMethodDescriptor<PyQueueObject>("qsize", nameof(PyQueueObject.QSizeImpl));
        AppendMethodDescriptor<PyQueueObject>("empty", nameof(PyQueueObject.EmptyImpl));
        AppendMethodDescriptor<PyQueueObject>("full", nameof(PyQueueObject.FullImpl));
        AppendMethodDescriptor<PyQueueObject>("put", nameof(PyQueueObject.PutImpl));
        AppendMethodDescriptor<PyQueueObject>("put_nowait", nameof(PyQueueObject.PutNoWaitImpl));
        AppendMethodDescriptor<PyQueueObject>("get", nameof(PyQueueObject.GetImpl));
        AppendMethodDescriptor<PyQueueObject>("get_nowait", nameof(PyQueueObject.GetNoWaitImpl));
        AppendMethodDescriptor<PyQueueObject>("task_done", nameof(PyQueueObject.TaskDoneImpl));
        AppendMethodDescriptor<PyQueueObject>("join", nameof(PyQueueObject.JoinImpl));
    }

    private static readonly PyBuiltinFunctionOrMethodObject _new = new(PySpecialNames.New, NewImpl);

    [PyFunctionArgsDef("maxsize=0")]
    private static PyQueueObject? NewImpl(PyArguments arguments)
    {
        if (!PyInteropService.TryGetIndex(arguments[0], out int maxSize))
            return null;

        return new PyQueueObject(maxSize);
    }

    protected internal override PyObject? New(PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        return _new.Call(args, kwargs);
    }
}

public sealed class PyFullObjectType : PyExceptionType<PyFullObjectType, PyExceptionObjectType>
{
    public override string Name => "Full";
    public override string FullName => $"queue.{Name}";
}

public sealed class PyEmptyObjectType : PyExceptionType<PyEmptyObjectType, PyExceptionObjectType>
{
    public override string Name => "Empty";
    public override string FullName => $"queue.{Name}";
}

public sealed class PyShutDownObjectType : PyExceptionType<PyShutDownObjectType, PyExceptionObjectType>
{
    public override string Name => "ShutDown";
    public override string FullName => $"queue.{Name}";
}