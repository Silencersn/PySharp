using PySharp.Modules.Builtins;

namespace PySharp.Runtime.Calls;

public sealed class PyArguments
{
    public static PyArguments Empty { get; } = new([], [], [], []);

    public IReadOnlyList<PyObject> Args { get; }
    public IReadOnlyDictionary<string, PyObject> Kwargs { get; }
    public IReadOnlyList<PyObject> ExtraArgs { get; }
    public IReadOnlyDictionary<string, PyObject> ExtraKwargs { get; }

    internal PyArguments(IEnumerable<PyObject> args, IEnumerable<PyObject> extraArgs, IEnumerable<KeyValuePair<string, PyObject>> kwargs, IEnumerable<KeyValuePair<string, PyObject>> extraKwargs)
    {
        Args = [.. args];
        ExtraArgs = [.. extraArgs];
        Kwargs = kwargs.ToDictionary();
        ExtraKwargs = new OrderedDictionary<string, PyObject>(extraKwargs);
    }

    public PyObject this[int index]
    {
        get
        {
            ArgumentOutOfRangeException.ThrowIfNegative(index);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, Args.Count + ExtraArgs.Count);

            if (index < Args.Count)
                return Args[index];

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

    public (PyObject Target, PyArguments Arguments) ToMethodArguments()
    {
        var target = Args[0];
        var arguments = new PyArguments(
            Args.Skip(1),
            ExtraArgs,
            Kwargs,
            ExtraKwargs);
        return (target, arguments);
    }
}