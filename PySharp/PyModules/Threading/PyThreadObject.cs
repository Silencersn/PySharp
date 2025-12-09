using PySharp.PyModules.Builtins;
using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;
using PySharp.PyRuntime.PyAttributes;
using System;
using System.Collections.Generic;
using System.Text;

namespace PySharp.PyModules.Threading;

public partial class PyThreadObject : PyObject
{
    private readonly PyObject _target;
    private readonly IReadOnlyList<PyObject> _args;
    private readonly IReadOnlyDictionary<string, PyObject> _kwargs;
    private Thread? _thread;

    public override PyTypeObject PyType => PyThreadObjectType.Shared;

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
}

public sealed class PyThreadObjectType : PyPrimitiveTypeObject<PyThreadObjectType, PyThreadObject>
{
    public override string Name => "Thread";

    private static readonly PyBuiltinFunctionOrMethodObject _new = new(PySpecialNames.New, NewImpl);

    public PyThreadObjectType()
    {
        AppendMethodDescriptor<PyThreadObject>("start", nameof(PyThreadObject.StartImpl));
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

    public override PyObject? New(IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        return _new.Call(args, kwargs);
    }
}