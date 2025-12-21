using PySharp.PyModules.Builtins;
using System.Diagnostics.CodeAnalysis;

namespace PySharp.PyRuntime.Calls;

public delegate PyObject? PyOldFunction(PyArguments arguments);
public delegate PyObject? PyOldUncompoundedFunction(IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs);

public delegate PyResult PyFunction(PyCallContext context, PyArguments arguments);
public delegate PyResult PyMethod(PyCallContext context, PyObject self, PyArguments arguments);
public delegate PyResult PyMethod<TObject>(PyCallContext context, TObject self, PyArguments arguments) where TObject : PyObject;
public delegate PyResult PyStaticMethod(PyCallContext context, PyTypeObject cls, PyArguments arguments);
public delegate PyResult PyUncompoundedDelegate(PyCallContext context, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs);

[Obsolete("Using PyArguments")]
public sealed class PyArgsPack
{
    private readonly IReadOnlyList<PyObject> _args;
    private readonly IReadOnlyDictionary<string, PyObject> _kwargs;

    public PyArgsPack(IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(kwargs);

        _args = args;
        _kwargs = kwargs;
    }

    public T GetValueOrDefault<T>(string key, T defaultValue, Func<PyObject, T> selector)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(selector);

        if (_kwargs.TryGetValue(key, out var value))
            return selector(value);

        return defaultValue;
    }

    public bool TryGetValue(string key, [NotNullWhen(true)] out PyObject? value)
    {
        return _kwargs.TryGetValue(key, out value);
    }

    public int Count => _args.Count + _kwargs.Count;
    public int ArgsCount => _args.Count;
    public int KwargsCount => _kwargs.Count;
    public IReadOnlyList<PyObject> Args => _args;
    public IReadOnlyDictionary<string, PyObject> Kwargs => _kwargs;

    public PyObject this[int index] => _args[index];
    public PyObject this[string key] => _kwargs[key];

    public bool ValidateEmpty()
    {
        return ValidateCount(0);
    }

    public bool ValidateCount(int count)
    {
        return Count == count;
    }

    public bool ValidateCount(int argsCount, int kwargsCount)
    {
        return ValidateArgsCount(argsCount) && ValidateKwargsCount(kwargsCount);
    }

    public bool ValidateArgsCount(int count)
    {
        return ArgsCount == count;
    }

    public bool ValidateKwargsCount(int count)
    {
        return KwargsCount == count;
    }

    public bool ValidateKwargsKeysAllIn(params ReadOnlySpan<string> keys)
    {
        foreach (var key in _kwargs.Keys)
        {
            if (!keys.Contains(key))
                return false;
        }
        return true;
    }
    public bool ValidateKwargsKeysAllIn([NotNullWhen(false)] out string? invalid, params ReadOnlySpan<string> keys)
    {
        foreach (var key in _kwargs.Keys)
        {
            if (!keys.Contains(key))
            {
                invalid = key;
                return false;
            }
        }

        invalid = null;
        return true;
    }

    public bool TryParseOneArg([NotNullWhen(true)] out PyObject? arg)
    {
        if (ValidateCount(1, 0))
        {
            arg = _args[0];
            return true;
        }

        arg = null;
        return false;
    }
    public bool TryParseOneKwarg(string key, [NotNullWhen(true)] out PyObject? arg)
    {
        if (ValidateCount(0, 1) && _kwargs.TryGetValue(key, out arg))
            return true;

        arg = null;
        return false;
    }
    public bool TryParseOneArgOrOneKwarg(string key, [NotNullWhen(true)] out PyObject? arg)
    {
        if (TryParseOneArg(out arg))
            return true;

        return TryParseOneKwarg(key, out arg);
    }
}
