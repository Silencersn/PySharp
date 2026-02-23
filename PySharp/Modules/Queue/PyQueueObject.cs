using PySharp.Modules.Builtins;
using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.Calls.Extensions;
using PySharp.Runtime.PyAttributes;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace PySharp.Modules.Queue;

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

[PyType("Queue", Module = "queue")]
public sealed partial class PyQueueObjectType : PyTypeObject<PyQueueObjectType, PyQueueObject>
{
    public override string DefaultModule => "queue";
    public override string Name => "Queue";

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

    private static readonly PyBuiltinFunctionOrMethodObject _new = PyBuiltinFunctionOrMethodObject.CreateFunction(PySpecialNames.New, NewImpl);

    [PyFunctionArgsDef("maxsize=0")]
    private static PyResult NewImpl(PyCallContext context, PyArguments arguments)
    {
        var result = PySpecialMethods.Index(context, arguments[0]);
        if (result.IsError)
            return result;
        return new PyQueueObject(result.Value.Int32Value);
    }

    protected override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
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
        var blockResult = PySpecialMethods.Bool(context, arguments[1]);
        if (blockResult.IsError)
            return blockResult;
        double? timeout;
        if (arguments[2] is PyNoneObject)
        {
            timeout = null;
        }
        else
        {
            var result = PySpecialMethods.Float(context, arguments[2]);
            if (result.IsError)
                return result;

            timeout = result.Value.Value;
        }
        var ex = self.PyPut(arguments[0], blockResult.Value.BoolValue, timeout);
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
        var blockResult = PySpecialMethods.Bool(context, arguments[0]);
        if (blockResult.IsError)
            return blockResult;
        double? timeout;
        if (arguments[1] is PyNoneObject)
        {
            timeout = null;
        }
        else
        {
            var result = PySpecialMethods.Float(context, arguments[1]);
            if (result.IsError)
                return result;

            timeout = result.Value.Value;
        }
        var (item, ex) = self.PyGet(blockResult.Value.BoolValue, timeout);
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
        return PyResult.ValueError(PySR.Runtime_Queue_TaskDoneCalledTooMany);
    }

    [PyFunctionArgsDef()]
    internal PyResult Join(PyCallContext context, PyQueueObject self, PyArguments arguments)
    {
        self.PyJoin();
        return PyNoneObject.None;
    }
}

[PyType("Full", Module = "queue")]
public sealed class PyFullObjectType : PyExceptionType<PyFullObjectType, PyExceptionObjectType>
{
    public override string DefaultModule => "queue";
    public override string Name => "Full";
}

[PyType("Empty", Module = "queue")]
public sealed class PyEmptyObjectType : PyExceptionType<PyEmptyObjectType, PyExceptionObjectType>
{
    public override string DefaultModule => "queue";
    public override string Name => "Empty";
}

[PyType("ShutDown", Module = "queue")]
public sealed class PyShutDownObjectType : PyExceptionType<PyShutDownObjectType, PyExceptionObjectType>
{
    public override string DefaultModule => "queue";
    public override string Name => "ShutDown";
}