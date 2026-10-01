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
    // the Thread-N default name or the explicit name= argument, settled at
    // construction — the excepthook report and the repr read this
    internal string _name;

    public override PyTypeObject DefaultPyType => PyThreadObjectType.Shared;

    internal PyThreadObject(PyObject target, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs, string name)
    {
        _target = target;
        _args = args;
        _kwargs = kwargs;
        _thread = null;
        _name = name;
    }
}

[PyType("Thread", Module = "threading")]
public sealed partial class PyThreadObjectType : PyTypeObject<PyThreadObject>
{
    // CPython names threads with a process-wide counter consumed at
    // construction time (threading.py _newname), not with the OS thread id
    private static int _nameCounter;

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

        // an explicit name wins as-is; the default is Thread-<construction
        // order>, suffixed with the target's __name__ when it has one
        // (threading.py __init__ / _newname)
        string name;
        if (arguments[2] is PyStrObject explicitName)
        {
            name = explicitName.Value;
        }
        else if (arguments[2] is not PyNoneObject)
        {
            return PyResult.TypeError(PySR.Runtime_Threading_NameMustBeString);
        }
        else
        {
            name = $"Thread-{Interlocked.Increment(ref _nameCounter)}";
            var target = arguments[1];
            if (target is not PyNoneObject)
            {
                var targetName = PyTypeObject.DefaultGetAttribute(context, target, PyStrObject.FromString("__name__"));
                if (!targetName.IsError && targetName.Value is PyStrObject targetNameString)
                    name += $" ({targetNameString.Value})";
            }
        }

        return new PyThreadObject(arguments[1], args, dict, name);
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
            self.PyJoin(context, -1);
            return PyNoneObject.None;
        }
        var result = PySpecialMethods.Float(context, arguments[0]);
        if (result.IsError)
            return result;
        var timeout = result.Value.Value;
        timeout = Math.Max(timeout, 0);
        self.PyJoin(context, timeout);
        return PyNoneObject.None;
    }

    [PyMethod("run")]
    [PyFunctionParameters()]
    private static PyResult Run(PyCallContext context, PyThreadObject self, PyArguments arguments)
    {
        return self.PyRun(context);
    }

    [PyMethod("is_alive")]
    [PyFunctionParameters()]
    private static PyResult IsAlive(PyCallContext context, PyThreadObject self, PyArguments arguments)
    {
        return PyBoolObject.FromBoolean(self.PyIsAlive());
    }
}
