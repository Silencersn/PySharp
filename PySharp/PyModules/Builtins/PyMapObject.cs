using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;
using PySharp.PyRuntime.PyAttributes;
using System.Collections.Frozen;

namespace PySharp.PyModules.Builtins;

public sealed class PyMapObject : PyObject
{
    private readonly PyObject _function;
    private readonly IEnumerator<PyObject?>[] _iters;
    private readonly bool _strict;

    public PyMapObject(PyObject function, IEnumerator<PyObject?>[] iters, bool strict)
    {
        _function = function;
        _iters = iters;
        _strict = strict;
    }

    public override PyObject? Iter()
    {
        return this;
    }

    public override PyObject? Next()
    {
        List<PyObject> args = [];
        foreach (var iter in _iters)
        {
            if (!iter.MoveNext())
                break;

            var arg = iter.Current;
            if (arg is null)
                return null;

            args.Add(arg);
        }

        if (args.Count != _iters.Length)
        {
            if (_strict)
                return PyVirtualMachine.RaiseValueError(null);

            return PyVirtualMachine.RaiseStopIteration();
        }

        return _function.Call(args, FrozenDictionary<string, PyObject>.Empty);
    }
}


public sealed class PyMapObjectType : PyPrimitiveTypeObject<PyMapObjectType, PyMapObject>
{
    public override string Name => "map";

    public PyMapObjectType()
    {
        AppendSpecialMethodsAsDescriptorsIfOverridden<PyMapObject>();
    }

    private static readonly PyBuiltinFunctionOrMethodObject _new = new(PySpecialNames.New, NewImpl);

    [PyFunctionArgsDef("function", "/", "*iterables", "strict=False")]
    private static PyObject? NewImpl(PyArguments arguments)
    {
        var function = arguments[0];
        if (!PyInteropService.TryGetBool(arguments["strict"], out var strict))
            return null;

        List<IEnumerator<PyObject?>> iters = [];
        foreach (var arg in arguments.ExtraArgs)
        {
            var iter = Utils.EnumerateIterable(arg);
            if (iter is null)
                return null;

            iters.Add(iter.GetEnumerator());
        }

        return new PyMapObject(function, [.. iters], strict);
    }

    public override PyObject? New(PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        return _new.Call(args, kwargs);
    }
}