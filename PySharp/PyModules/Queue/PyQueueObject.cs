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
}

public sealed class PyQueueObjectType : PyTypeObject<PyQueueObjectType, PyQueueObject>
{
    public override string Name => "Queue";
    public override string FullName => $"queue.{Name}";

    public PyQueueObjectType()
    {
        AppendMethodDescriptor("qsize", QSize);
        AppendMethodDescriptor("empty", Empty);
        AppendMethodDescriptor("full", Full);
        AppendMethodDescriptor("put", Put);
        AppendMethodDescriptor("put_nowait", PutNoWait);
        AppendMethodDescriptor("get", Get);
        AppendMethodDescriptor("get_nowait", GetNoWait);
        AppendMethodDescriptor("task_done", TaskDone);
        AppendMethodDescriptor("join", Join);
    }

    private static readonly PyBuiltinFunctionOrMethodObject _new = new(PySpecialNames.New, NewImpl);

    [PyFunctionArgsDef("maxsize=0")]
    private static PyResult NewImpl(PyCallContext context, PyArguments arguments)
    {
        if (!PySpecialMethods.TryGetIndex(context, arguments[0], out var maxSize, out var result))
            return PyResult.CaptureExceptionFromPVM();
        return new PyQueueObject(maxSize.Int32Value);
    }

    protected internal override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        var obj = _new.Call(context, args, kwargs);
        if (obj.IsError)
            return obj;
        obj.Value._pyType = cls;
        return obj;
    }

    [PyFunctionArgsDef()]
    internal PyResult QSize(PyCallContext context, PyQueueObject self, PyArguments arguments)
    {
        return PyIntObject.FromInteger(self.PyQSize());
    }

    [PyFunctionArgsDef()]
    internal PyResult Empty(PyCallContext context, PyQueueObject self, PyArguments arguments)
    {
        return PyBoolObject.FromBoolean(self.PyEmpty());
    }

    [PyFunctionArgsDef()]
    internal PyResult Full(PyCallContext context, PyQueueObject self, PyArguments arguments)
    {
        return PyBoolObject.FromBoolean(self.PyFull());
    }

    [PyFunctionArgsDef("item", "block=True", "timeout=None")]
    internal PyResult Put(PyCallContext context, PyQueueObject self, PyArguments arguments)
    {
        if (!PySpecialMethods.TryGetBool(context, arguments[1], out var block, out var result))
            return result;
        double? timeout;
        if (arguments[2] is PyNoneObject)
            timeout = null;
        else if (PySpecialMethods.TryGetFloat(context, arguments[2], out var value, out result))
            timeout = value.Value;
        else
            return result;
        var ex = self.PyPut(arguments[0], block.BoolValue, timeout);
        if (ex is not null)
            return PyResult.RaiseException(ex);
        return PyNoneObject.None;
    }

    [PyFunctionArgsDef("item")]
    internal PyResult PutNoWait(PyCallContext context, PyQueueObject self, PyArguments arguments)
    {
        var ex = self.PyPut(arguments[0], false, null);
        if (ex is not null)
            return PyResult.RaiseException(ex);
        return PyNoneObject.None;
    }

    [PyFunctionArgsDef("block=True", "timeout=None")]
    internal PyResult Get(PyCallContext context, PyQueueObject self, PyArguments arguments)
    {
        if (!PySpecialMethods.TryGetBool(context, arguments[0], out var block, out var result))
            return result;
        double? timeout;
        if (arguments[1] is PyNoneObject)
            timeout = null;
        else if (PySpecialMethods.TryGetFloat(context, arguments[1], out var value, out result))
            timeout = value.Value;
        else
            return result;
        var (item, ex) = self.PyGet(block.BoolValue, timeout);
        if (ex is not null)
            return PyResult.RaiseException(ex);
        Debug.Assert(item is not null);
        return item;
    }

    [PyFunctionArgsDef()]
    internal PyResult GetNoWait(PyCallContext context, PyQueueObject self, PyArguments arguments)
    {
        var (item, ex) = self.PyGet(false, null);
        if (ex is not null)
            return PyResult.RaiseException(ex);
        Debug.Assert(item is not null);
        return item;
    }

    [PyFunctionArgsDef()]
    internal PyResult TaskDone(PyCallContext context, PyQueueObject self, PyArguments arguments)
    {
        if (self.PyTryTaskDone())
            return PyNoneObject.None;
        return PyResult.RaiseValueError("task_done() called too many times");
    }

    [PyFunctionArgsDef()]
    internal PyResult Join(PyCallContext context, PyQueueObject self, PyArguments arguments)
    {
        self.PyJoin();
        return PyNoneObject.None;
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