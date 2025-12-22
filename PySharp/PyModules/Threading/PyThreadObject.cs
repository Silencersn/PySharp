using PySharp.PyModules.Builtins;
using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;
using PySharp.PyRuntime.PyAttributes;

namespace PySharp.PyModules.Threading;

public partial class PyThreadObject : PyObject
{
    internal readonly PyObject _target;
    internal readonly IReadOnlyList<PyObject> _args;
    internal readonly IReadOnlyDictionary<string, PyObject> _kwargs;
    internal Thread? _thread;

    public override PyTypeObject DefaultPyType => PyThreadObjectType.Shared;

    internal PyThreadObject(PyObject target, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        _target = target;
        _args = args;
        _kwargs = kwargs;
        _thread = null;
    }
}

public sealed class PyThreadObjectType : PyTypeObject<PyThreadObjectType, PyThreadObject>
{
    public override string Name => "Thread";

    private static readonly PyBuiltinFunctionOrMethodObject _new = new(PySpecialNames.New, NewImpl);

    public PyThreadObjectType()
    {
        AppendMethodDescriptor("start", Start);
        AppendMethodDescriptor("join", Join);
        AppendMethodDescriptor("is_alive", IsAlive);
    }

    [PyFunctionArgsDef("group=None", "target=None", "name=None", "args=()", "kwargs={}", "*", "daemon=None", "context=None")]
    private static PyResult NewImpl(PyCallContext context, PyArguments arguments)
    {
        if (arguments[3] is not PyTupleObject args)
            return PyResult.RaiseTypeError(null);
        if (arguments[4] is not PyDictObject kwargs)
            return PyResult.RaiseTypeError(null);
        Dictionary<string, PyObject> dict = [];
        foreach (var pair in kwargs._dict)
        {
            if (pair.Key is not PyStrObject str)
                return PyResult.RaiseTypeError(null);
            dict[str.Value] = pair.Value;
        }
        return new PyThreadObject(arguments[1], args._array, dict);
    }

    protected internal override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        var obj = _new.Call(args, kwargs);
        if (obj is null)
            return PyResult.CaptureExceptionFromPVM();
        obj._pyType = cls;
        return obj;
    }

    [PyFunctionArgsDef()]
    internal PyResult Start(PyCallContext context, PyThreadObject self, PyArguments arguments)
    {
        self.PyStart();
        return PyNoneObject.None;
    }

    [PyFunctionArgsDef("timeout=None")]
    internal PyResult Join(PyCallContext context, PyThreadObject self, PyArguments arguments)
    {
        if (arguments[0] is PyNoneObject)
        {
            self.PyJoin(-1);
            return PyNoneObject.None;
        }
        if (!PyInteropService.TryGetFloat(arguments[0], out var timeout))
            return PyResult.CaptureExceptionFromPVM();
        timeout = Math.Max(timeout, 0);
        self.PyJoin(timeout);
        return PyNoneObject.None;
    }

    [PyFunctionArgsDef()]
    internal PyResult IsAlive(PyCallContext context, PyThreadObject self, PyArguments arguments)
    {
        return PyBoolObject.FromBoolean(self.PyIsAlive());
    }
}