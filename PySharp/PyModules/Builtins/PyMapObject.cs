using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;
using PySharp.PyRuntime.PyAttributes;
using System.Collections.Frozen;

namespace PySharp.PyModules.Builtins;

public sealed class PyMapObject : PyObject
{
    internal readonly PyObject _function;
    internal readonly IEnumerator<PyObject?>[] _iters;
    internal readonly bool _strict;

    public PyMapObject(PyObject function, IEnumerator<PyObject?>[] iters, bool strict)
    {
        _function = function;
        _iters = iters;
        _strict = strict;
    }
}

public sealed class PyMapObjectType : PyTypeObject<PyMapObjectType, PyMapObject>
{
    public override string Name => "map";

    private static readonly PyBuiltinFunctionOrMethodObject2 _new = new(PySpecialNames.New, NewImpl);

    [PyFunctionArgsDef("function", "/", "*iterables", "strict=False")]
    private static PyResult NewImpl(PyCallContext context, PyArguments arguments)
    {
        var function = arguments[0];
        if (!PyInteropService.TryGetBool(arguments["strict"], out var strict))
            return PyResult.CaptureExceptionFromPVM();

        List<IEnumerator<PyObject?>> iters = [];
        foreach (var arg in arguments.ExtraArgs)
        {
            var iter = Utils.EnumerateIterable(arg);
            if (iter is null)
                return PyResult.CaptureExceptionFromPVM();
            iters.Add(iter.GetEnumerator());
        }

        return new PyMapObject(function, [.. iters], strict);
    }

    protected internal override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        var obj = _new.Call(args, kwargs);
        if (obj is null)
            return PyResult.CaptureExceptionFromPVM();
        obj._pyType = cls;
        return obj;
    }

    protected internal override PyResult Iter(PyCallContext context, PyMapObject self)
    {
        return self;
    }

    protected internal override PyResult Next(PyCallContext context, PyMapObject self)
    {
        List<PyObject> args = [];
        foreach (var iter in self._iters)
        {
            if (!iter.MoveNext())
                break;
            var arg = iter.Current;
            if (arg is null)
                return PyResult.CaptureExceptionFromPVM();
            args.Add(arg);
        }
        if (args.Count != self._iters.Length)
        {
            if (self._strict)
                return PyResult.RaiseValueError(null);
            return PyResult.RaiseStopIteration();
        }
        var result = self._function.Call(args, FrozenDictionary<string, PyObject>.Empty);
        if (result is null)
            return PyResult.CaptureExceptionFromPVM();
        return result;
    }
}