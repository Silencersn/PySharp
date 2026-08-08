using PySharp.Modules.Builtins;
using System.Collections.Frozen;

namespace PySharp.Runtime.Calls;

public readonly ref struct PyArguments
{
    public static PyArguments Empty => new(PyArgsDef.Empty, [], [], FrozenDictionary<string, PyObject>.Empty);

    private readonly PyObject[] _extraArgs;
    private readonly PyArgsDef _argsDef;
    private readonly ReadOnlySpan<PyObject> _argsAndKwargs;

    internal readonly ReadOnlySpan<PyObject> ArgsAndKwargs => _argsAndKwargs;

    public readonly ReadOnlySpan<PyObject> Args;
    public readonly IReadOnlyList<PyObject> ExtraArgs => _extraArgs;
    public IReadOnlyDictionary<string, PyObject> ExtraKwargs { get; }

    internal PyArguments(PyArgsDef argsDef, ReadOnlySpan<PyObject> argsAndKwargs, PyObject[] extraArgs, IReadOnlyDictionary<string, PyObject> extraKwargs)
    {
        _argsDef = argsDef;
        _argsAndKwargs = argsAndKwargs;
        Args = argsAndKwargs[..(argsDef.PosonlyArgs.Length + argsDef.Args.Length)];
        _extraArgs = extraArgs;
        ExtraKwargs = extraKwargs;
    }

    public PyObject this[int index]
    {
        get
        {
            if (index < Args.Length)
                return Args[index];

            return _extraArgs[index - Args.Length];
        }
    }

    public PyObject this[string key]
    {
        get
        {
            var index = _argsDef.KwonlyArgs.IndexOf(key);
            if (index is not -1)
                return _argsAndKwargs[Args.Length + index];

            if (ExtraKwargs.TryGetValue(key, out var value))
                return value;

            throw new KeyNotFoundException(key);
        }
    }
}