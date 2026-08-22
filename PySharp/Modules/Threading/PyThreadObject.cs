using PySharp.Modules.Builtins;
using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;

namespace PySharp.Modules.Threading;

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

[PyType("Thread", Module = "threading")]
public sealed partial class PyThreadObjectType : PyTypeObject<PyThreadObject>
{
    [PyExport(PySpecialNames.New, nameof(NewImpl))]
    private static partial PyBuiltinFunctionOrMethodObject _new { get; }

    [PyFunctionParameters("group=None", "target=None", "name=None", "args=()", "kwargs={}", "*", "daemon=None", "context=None")]
    private static PyResult NewImpl(PyCallContext context, PyArguments arguments)
    {
        if (arguments[3] is not PyTupleObject args)
            return PyResult.TypeError(null);
        if (arguments[4] is not PyDictObject kwargs)
            return PyResult.TypeError(null);
        Dictionary<string, PyObject> dict = [];
        foreach (var pair in kwargs.Entries)
        {
            if (pair.Key is not PyStrObject str)
                return PyResult.TypeError(null);
            dict[str.Value] = pair.Value;
        }
        return new PyThreadObject(arguments[1], args, dict);
    }

    protected override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        var obj = _new.Call(context, args, kwargs);
        if (obj.IsError)
            return obj;
        obj.Value._pyType = cls;
        return obj;
    }

    [PyMethod("start")]
    [PyFunctionParameters()]
    private static PyResult Start(PyCallContext context, PyThreadObject self, PyArguments arguments)
    {
        self.PyStart(context);
        return PyNoneObject.None;
    }

    [PyMethod("join")]
    [PyFunctionParameters("timeout=None")]
    private static PyResult Join(PyCallContext context, PyThreadObject self, PyArguments arguments)
    {
        if (arguments[0] is PyNoneObject)
        {
            self.PyJoin(-1);
            return PyNoneObject.None;
        }
        var result = PySpecialMethods.Float(context, arguments[0]);
        if (result.IsError)
            return result;
        var timeout = result.Value.Value;
        timeout = Math.Max(timeout, 0);
        self.PyJoin(timeout);
        return PyNoneObject.None;
    }

    [PyMethod("is_alive")]
    [PyFunctionParameters()]
    private static PyResult IsAlive(PyCallContext context, PyThreadObject self, PyArguments arguments)
    {
        return PyBoolObject.FromBoolean(self.PyIsAlive());
    }
}
