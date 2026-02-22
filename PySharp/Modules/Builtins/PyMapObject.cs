using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.Calls.Extensions;
using PySharp.Runtime.PyAttributes;
using System.Collections.Frozen;

namespace PySharp.Modules.Builtins;

public sealed class PyMapObject : PyObject
{
    internal readonly PyObject _function;
    internal readonly IEnumerator<PyResult>[] _iters;
    internal readonly bool _strict;

    public PyMapObject(PyObject function, IEnumerator<PyResult>[] iters, bool strict)
    {
        _function = function;
        _iters = iters;
        _strict = strict;
    }
}

public sealed class PyMapObjectType : PyTypeObject<PyMapObjectType, PyMapObject>
{
    public override string Name => "map";

    private static readonly PyBuiltinFunctionOrMethodObject _new = PyBuiltinFunctionOrMethodObject.CreateFunction(PySpecialNames.New, NewImpl);

    [PyFunctionArgsDef("function", "/", "*iterables", "strict=False")]
    private static PyResult NewImpl(PyCallContext context, PyArguments arguments)
    {
        var function = arguments[0];
        var result = PySpecialMethods.Bool(context, arguments["strict"]);
        if (result.IsError)
            return result;

        List<IEnumerator<PyResult>> iters = [];
        foreach (var arg in arguments.ExtraArgs)
        {
            if (!Utils.TryEnumerateIterable(context, arg, out var iter, out var err))
                return err.Value;
            iters.Add(iter.GetEnumerator());
        }

        return new PyMapObject(function, [.. iters], result.Value.BoolValue);
    }

    protected override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        var obj = _new.Call(context, args, kwargs);
        if (obj.IsError)
            return obj;
        obj.Value._pyType = cls;
        return obj;
    }

    protected override PyResult Iter(PyCallContext context, PyMapObject self)
    {
        return self;
    }

    protected override PyResult Next(PyCallContext context, PyMapObject self)
    {
        List<PyObject> args = [];
        foreach (var iter in self._iters)
        {
            if (!iter.MoveNext())
                break;
            var arg = iter.Current;
            if (arg.IsError)
                return arg;
            args.Add(arg.Value);
        }
        if (args.Count != self._iters.Length)
        {
            if (self._strict)
                return PyResult.ValueError(null);
            return PyResult.StopIteration();
        }
        return self._function.Call(context, args, FrozenDictionary<string, PyObject>.Empty);
    }
}