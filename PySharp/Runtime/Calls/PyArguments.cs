using PySharp.Modules.Builtins;
using System.Collections.Frozen;

namespace PySharp.Runtime.Calls;

public readonly ref struct PyArguments
{
    public static PyArguments Empty => new([], FrozenDictionary<string, PyObject>.Empty, [], FrozenDictionary<string, PyObject>.Empty);

    private readonly PyObject[] _args;

    internal ReadOnlySpan<PyObject> InternalArgs => _args;

    public IReadOnlyList<PyObject> Args => _args;
    public IReadOnlyDictionary<string, PyObject> Kwargs { get; }
    public IReadOnlyList<PyObject> ExtraArgs { get; }
    public IReadOnlyDictionary<string, PyObject> ExtraKwargs { get; }

    internal PyArguments(PyObject[] args, IReadOnlyDictionary<string, PyObject> kwargs, IReadOnlyList<PyObject> extraArgs, IReadOnlyDictionary<string, PyObject> extraKwargs)
    {
        _args = args;
        Kwargs = kwargs;
        ExtraArgs = extraArgs;
        ExtraKwargs = extraKwargs;
    }

    public PyObject this[int index]
    {
        get
        {
            ArgumentOutOfRangeException.ThrowIfNegative(index);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, Args.Count + ExtraArgs.Count);

            if (index < Args.Count)
                return InternalArgs[index];

            return ExtraArgs[index - Args.Count];
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