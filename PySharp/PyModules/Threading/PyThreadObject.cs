using PySharp.PyModules.Builtins;
using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;
using PySharp.PyRuntime.PyAttributes;

namespace PySharp.PyModules.Threading;

public partial class PyThreadObject : PyObject
{
    private readonly PyObject _target;
    private readonly IReadOnlyList<PyObject> _args;
    private readonly IReadOnlyDictionary<string, PyObject> _kwargs;
    private Thread? _thread;

    public override PyTypeObject DefaultPyType => PyThreadObjectType.Shared;

    internal PyThreadObject(PyObject target, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        _target = target;
        _args = args;
        _kwargs = kwargs;
        _thread = null;
    }

    [PyFunctionArgsDef()]
    internal PyObject? StartImpl(PyArguments arguments)
    {
        PyStart();
        return PyNoneObject.None;
    }

    [PyFunctionArgsDef("timeout=None")]
    internal PyObject? JoinImpl(PyArguments arguments)
    {
        if (arguments[0] is PyNoneObject)
        {
            PyJoin(-1);
            return PyNoneObject.None;
        }

        if (!PyInteropService.TryGetFloat(arguments[0], out var timeout))
            return null;

        timeout = Math.Max(timeout, 0);
        PyJoin(timeout);
        return PyNoneObject.None;
    }

    [PyFunctionArgsDef()]
    internal PyBoolObject IsAliveImpl(PyArguments arguments)
    {
        return PyBoolObject.FromBoolean(PyIsAlive());
    }
}

public sealed class PyThreadObjectType : PyPrimitiveTypeObject<PyThreadObjectType, PyThreadObject>
{
    public override string Name => "Thread";

    private static readonly PyBuiltinFunctionOrMethodObject _new = new(PySpecialNames.New, NewImpl);

    public PyThreadObjectType()
    {
        AppendMethodDescriptor<PyThreadObject>("start", nameof(PyThreadObject.StartImpl));
        AppendMethodDescriptor<PyThreadObject>("join", nameof(PyThreadObject.JoinImpl));
        AppendMethodDescriptor<PyThreadObject>("is_alive", nameof(PyThreadObject.IsAliveImpl));
    }

    [PyFunctionArgsDef("group=None", "target=None", "name=None", "args=()", "kwargs={}", "*", "daemon=None", "context=None")]
    private static PyObject? NewImpl(PyArguments arguments)
    {
        if (arguments[3] is not PyTupleObject args)
            return PyVirtualMachine.RaiseTypeError(null);

        if (arguments[4] is not PyDictObject kwargs)
            return PyVirtualMachine.RaiseTypeError(null);

        Dictionary<string, PyObject> dict = [];
        foreach (var pair in kwargs._dict)
        {
            if (pair.Key is not PyStrObject str)
                return PyVirtualMachine.RaiseTypeError(null);

            dict[str.Value] = pair.Value;
        }

        return new PyThreadObject(arguments[1], args._array, dict);
    }

    protected internal override PyObject? New(PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        return _new.Call(args, kwargs);
    }
}