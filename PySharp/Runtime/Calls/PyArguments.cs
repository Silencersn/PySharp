using PySharp.Modules.Builtins;
using System.Collections.Frozen;

namespace PySharp.Runtime.Calls;

public readonly ref struct PyArguments
{
    public static PyArguments Empty => new([], FrozenDictionary<string, PyObject>.Empty, [], FrozenDictionary<string, PyObject>.Empty);

    private readonly PyObject[] _args;
    private readonly PyObject[] _extraArgs;

    internal ReadOnlySpan<PyObject> InternalArgs => _args;

    public IReadOnlyList<PyObject> Args => _args;
    public IReadOnlyDictionary<string, PyObject> Kwargs { get; }
    public IReadOnlyList<PyObject> ExtraArgs => _extraArgs;
    public IReadOnlyDictionary<string, PyObject> ExtraKwargs { get; }

    internal PyArguments(PyObject[] args, IReadOnlyDictionary<string, PyObject> kwargs, PyObject[] extraArgs, IReadOnlyDictionary<string, PyObject> extraKwargs)
    {
        _args = args;
        _extraArgs = extraArgs;
        Kwargs = kwargs;
        ExtraKwargs = extraKwargs;
    }

    public PyObject this[int index]
    {
        get
        {
            ArgumentOutOfRangeException.ThrowIfNegative(index);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, _args.Length + ExtraArgs.Count);

            if (index < _args.Length)
                return _args[index];

            return _extraArgs[index - _args.Length];
        }
    }

    public PyObject this[string key]
    {
        get
        {
            ArgumentNullException.ThrowIfNull(key);

            if (Kwargs.TryGetValue(key, out var value))
                return value;

            if (ExtraKwargs.TryGetValue(key, out value))
                return value;

            throw new KeyNotFoundException(key);
        }
    }
}