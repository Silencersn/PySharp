using PySharp.Modules.Builtins;
using PySharp.Runtime;
using PySharp.Runtime.Calls;
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
public sealed partial class PyQueueObjectType : PyTypeObject<PyQueueObject>
{
    [PyExport(PySpecialNames.New, nameof(NewImpl))]
    private static partial PyBuiltinFunctionOrMethodObject _new { get; }

    [PyFunctionParameters("maxsize=0")]
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

    [PyMethod("qsize")]
    [PyFunctionParameters()]
    private static PyResult QSize(PyCallContext context, PyQueueObject self, PyArguments arguments)
    {
        return PyIntObject.FromInteger(self.PyQSize());
    }

    [PyMethod("empty")]
    [PyFunctionParameters()]
    private static PyResult Empty(PyCallContext context, PyQueueObject self, PyArguments arguments)
    {
        return PyBoolObject.FromBoolean(self.PyEmpty());
    }

    [PyMethod("full")]
    [PyFunctionParameters()]
    private static PyResult Full(PyCallContext context, PyQueueObject self, PyArguments arguments)
    {
        return PyBoolObject.FromBoolean(self.PyFull());
    }

    [PyMethod("put")]
    [PyFunctionParameters("item", "block=True", "timeout=None")]
    private static PyResult Put(PyCallContext context, PyQueueObject self, PyArguments arguments)
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

    [PyMethod("put_nowait")]
    [PyFunctionParameters("item")]
    private static PyResult PutNoWait(PyCallContext context, PyQueueObject self, PyArguments arguments)
    {
        var ex = self.PyPut(arguments[0], false, null);
        if (ex is not null)
            return PyResult.RaiseException(ex);
        return PyNoneObject.None;
    }

    [PyMethod("get")]
    [PyFunctionParameters("block=True", "timeout=None")]
    private static PyResult Get(PyCallContext context, PyQueueObject self, PyArguments arguments)
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

    [PyMethod("get_nowait")]
    [PyFunctionParameters()]
    private static PyResult GetNoWait(PyCallContext context, PyQueueObject self, PyArguments arguments)
    {
        var (item, ex) = self.PyGet(false, null);
        if (ex is not null)
            return PyResult.RaiseException(ex);
        Debug.Assert(item is not null);
        return item;
    }

    [PyMethod("task_done")]
    [PyFunctionParameters()]
    private static PyResult TaskDone(PyCallContext context, PyQueueObject self, PyArguments arguments)
    {
        if (self.PyTryTaskDone())
            return PyNoneObject.None;
        return PyResult.ValueError(PySR.Runtime_Queue_TaskDoneCalledTooMany);
    }

    [PyMethod("join")]
    [PyFunctionParameters()]
    private static PyResult Join(PyCallContext context, PyQueueObject self, PyArguments arguments)
    {
        self.PyJoin();
        return PyNoneObject.None;
    }
}

[PyException("Full", Module = "queue")]
public sealed partial class PyFullObjectType : PyExceptionType;

[PyException("Empty", Module = "queue")]
public sealed partial class PyEmptyObjectType : PyExceptionType;

[PyException("ShutDown", Module = "queue")]
public sealed partial class PyShutDownObjectType : PyExceptionType;
