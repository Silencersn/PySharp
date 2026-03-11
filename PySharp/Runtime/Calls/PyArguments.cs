using PySharp.Modules.Builtins;
using System.Collections.Frozen;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PySharp.Runtime.Calls;

public readonly ref struct PyArguments
{
    public static PyArguments Empty => new([], FrozenDictionary<string, PyObject>.Empty, [], FrozenDictionary<string, PyObject>.Empty);

    private readonly PyObject[] _extraArgs;

    public readonly ReadOnlySpan<PyObject> Args;
    public IReadOnlyDictionary<string, PyObject> Kwargs { get; }
    public readonly IReadOnlyList<PyObject> ExtraArgs => _extraArgs;
    public IReadOnlyDictionary<string, PyObject> ExtraKwargs { get; }

    internal PyArguments(ReadOnlySpan<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs, PyObject[] extraArgs, IReadOnlyDictionary<string, PyObject> extraKwargs)
    {
        Args = args;
        _extraArgs = extraArgs;
        Kwargs = kwargs;
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
            if (Kwargs.TryGetValue(key, out var value))
                return value;

            if (ExtraKwargs.TryGetValue(key, out value))
                return value;

            throw new KeyNotFoundException(key);
        }
    }
}