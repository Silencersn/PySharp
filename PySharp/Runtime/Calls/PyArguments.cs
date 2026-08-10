using PySharp.Modules.Builtins;
using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;

namespace PySharp.Runtime.Calls;

public readonly ref struct PyArguments
{
    public static PyArguments Empty => new(PyArgsDef.Empty, [], null, null);

    private readonly PyObject[] _extraArgs;
    private readonly List<KeyValuePair<string, PyObject>>? _extraKwargs;
    private readonly PyArgsDef _argsDef;
    private readonly ReadOnlySpan<PyObject> _argsAndKwargs;

    internal readonly ReadOnlySpan<PyObject> ArgsAndKwargs => _argsAndKwargs;
    internal PyObject[] InternalExtraArgs => _extraArgs;

    public readonly ReadOnlySpan<PyObject> Args;
    public readonly IReadOnlyList<PyObject> ExtraArgs => _extraArgs;
    public IReadOnlyList<KeyValuePair<string, PyObject>> ExtraKwargs => _extraKwargs ?? [];

    internal PyArguments(PyArgsDef argsDef, ReadOnlySpan<PyObject> argsAndKwargs, PyObject[]? extraArgs, List<KeyValuePair<string, PyObject>>? extraKwargs)
    {
        _argsDef = argsDef;
        _argsAndKwargs = argsAndKwargs;
        Args = argsAndKwargs[..(argsDef.PosonlyArgs.Length + argsDef.Args.Length)];
        _extraArgs = extraArgs ?? [];
        _extraKwargs = extraKwargs;
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

            if (TryGetExtraKwarg(key, out var kwarg))
                return kwarg;

            throw new KeyNotFoundException(key);
        }
    }

    public bool TryGetExtraKwarg(string key, [NotNullWhen(true)] out PyObject? extraKwarg)
    {
        if (_extraKwargs is null)
        {
            extraKwarg = null;
            return false;
        }

        foreach (var arg in _extraKwargs)
        {
            if (!string.Equals(arg.Key, key, StringComparison.Ordinal))
                continue;

            extraKwarg = arg.Value;
            return true;
        }

        extraKwarg = null;
        return false;
    }

    internal PyObject GetKwargByIndex(int index)
    {
        return _argsAndKwargs[Args.Length + index];
    }
}