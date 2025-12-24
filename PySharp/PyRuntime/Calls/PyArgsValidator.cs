using PySharp.PyModules.Builtins;
using System.Diagnostics.CodeAnalysis;

namespace PySharp.PyRuntime.Calls;

public static class PyArgsValidator
{
    public static bool ValidateArgs(IReadOnlyList<PyObject> args, int expectedCount, [NotNullWhen(false)] out PyResult? err)
    {
        if (args.Count > expectedCount)
        {
            err = PyResult.RaiseTypeError($"takes {expectedCount} positional arguments but {args.Count} was given");
            return false;
        }

        if (args.Count < expectedCount)
        {
            var missingCount = expectedCount - args.Count;
            err = PyResult.RaiseTypeError($"missing {missingCount} required positional {(missingCount is 1 ? "argument" : "arguments")}");
            return false;
        }

        err = null;
        return true;
    }

    public static bool ValidateEmptyKwargs(IReadOnlyDictionary<string, PyObject> kwargs, [NotNullWhen(false)] out PyResult? err)
    {
        if (kwargs.Count > 0)
        {
            err = PyResult.RaiseTypeError($"got an unexpected keyword argument '{kwargs.First().Key}'");
            return false;
        }

        err = null;
        return true;
    }

    public static bool ValidateSinglePositionalArg(IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs, [NotNullWhen(false)] out PyResult? err)
    {
        if (!ValidateArgs(args, 1, out err))
            return false;

        if (!ValidateEmptyKwargs(kwargs, out err))
            return false;

        return true;
    }

    public static bool ValidateEmpty(IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs, [NotNullWhen(false)] out PyResult? err)
    {
        if (!ValidateArgs(args, 0, out err))
            return false;

        if (!ValidateEmptyKwargs(kwargs, out err))
            return false;

        return true;
    }
}
