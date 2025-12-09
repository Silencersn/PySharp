using PySharp.AstNodes;
using PySharp.PyModules.Builtins;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace PySharp.PyRuntime.Calls;

internal enum PyArgsDefParametersType
{
    Unknown = 0,

    NoArgsOrKwargs
}

public sealed class PyArgsDef
{
    private PyArgsDef(string[] posonlyArgs, string[] args, string[] kwonlyArgs, PyObject?[] kwDefaults, PyObject[] defaults, string? varArg, string? kwArg)
    {
        PosonlyArgs = posonlyArgs;
        Args = args;
        KwonlyArgs = kwonlyArgs;
        KwDefaults = kwDefaults;
        Defaults = defaults;
        VarArg = varArg;
        KwArg = kwArg;

        ParametersType = PyArgsDefParametersType.Unknown;
        if (PosonlyArgs.Length is 0 && Args.Length is 0 && KwonlyArgs.Length is 0 && VarArg is null && KwArg is null)
            ParametersType = PyArgsDefParametersType.NoArgsOrKwargs;
    }

    internal PyArgsDefParametersType ParametersType { get; }
    public string[] PosonlyArgs { get; }
    public string[] Args { get; }
    public string[] KwonlyArgs { get; }
    public PyObject?[] KwDefaults { get; }
    public PyObject[] Defaults { get; }
    public string? VarArg { get; }
    public string? KwArg { get; }

    private static PyObject ParseLiteral(string literal)
    {
        if (literal is "None")
            return PyNoneObject.None;

        if (literal is "True" or "False")
            return PyBoolObject.FromBoolean(literal is "True");

        var removedSpace = literal.Replace(" ", string.Empty);
        if (removedSpace is "()")
            return PyTupleObject.CreateTuple();

        if (removedSpace is "[]")
            return PyListObject.CreateList();

        if (removedSpace is "{}")
            return PyDictObject.CreateDict();

        if (literal[0] is '"' or '\'')
            return PyStrObject.FromLiteral(literal);

        if (PyIntObjectType.TryParse(literal, 0, out var resultInt))
            return PyIntObject.FromInteger(resultInt);

        if (double.TryParse(literal, out var resultDouble))
            return new PyFloatObject(resultDouble);

        throw new NotSupportedException();
    }

    public static PyArgsDef FromDef(params ReadOnlySpan<string> parameters)
    {
        string[] posonlyArgs, args, kwonlyArgs;
        PyObject?[] kwDefaults;
        string? varArg = null, kwArg = null;

        List<PyObject> defaults = [];
        int index = parameters.IndexOf("/");
        if (index is not -1)
        {
            posonlyArgs = parameters[..index].ToArray();
            parameters = parameters[(index + 1)..];
            for (int i = 0; i < posonlyArgs.Length; i++)
            {
                var arg = posonlyArgs[i];
                var indexOfEqual = arg.IndexOf('=');
                if (indexOfEqual is not -1)
                {
                    posonlyArgs[i] = arg[..indexOfEqual];
                    var d = ParseLiteral(arg[(indexOfEqual + 1)..]);
                    defaults.Add(d);
                }
            }
        }
        else
        {
            posonlyArgs = [];
        }

        index = -1;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].StartsWith('*') && !parameters[i].StartsWith("**"))
            {
                index = i;
                if (parameters[i] is not "*")
                    varArg = parameters[i][1..];
                break;
            }
        }
        if (index is not -1)
        {
            kwonlyArgs = parameters[(index + 1)..].ToArray();
            parameters = parameters[..index];
            if (kwonlyArgs.Length > 0 && kwonlyArgs[^1].StartsWith("**"))
            {
                kwArg = kwonlyArgs[^1][2..];
                kwonlyArgs = kwonlyArgs[..^1];
            }

            kwDefaults = new PyObject?[kwonlyArgs.Length];

            for (int i = 0; i < kwonlyArgs.Length; i++)
            {
                var kwarg = kwonlyArgs[i];
                var indexOfEqual = kwarg.IndexOf('=');
                if (indexOfEqual is not -1)
                {
                    kwonlyArgs[i] = kwarg[..indexOfEqual];
                    kwDefaults[i] = ParseLiteral(kwarg[(indexOfEqual + 1)..]);
                }
            }
        }
        else
        {
            kwonlyArgs = [];
            kwDefaults = [];
        }

        args = parameters.ToArray();
        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            var indexOfEqual = arg.IndexOf('=');
            if (indexOfEqual is not -1)
            {
                args[i] = arg[..indexOfEqual];
                var d = ParseLiteral(arg[(indexOfEqual + 1)..]);
                defaults.Add(d);
            }
        }
        return new PyArgsDef(posonlyArgs, args, kwonlyArgs, kwDefaults, [.. defaults], varArg, kwArg);
    }

    internal static PyArgsDef FromAst(AstArgumentsNode node, PyFrame frame)
    {
        return new PyArgsDef(
            [.. node.PosonlyArgs.Select(arg => arg.Arg)],
            [.. node.Args.Select(arg => arg.Arg)],
            [.. node.KwonlyArgs.Select(arg => arg.Arg)],
            [.. node.KwDefaults.Select(d => d?.GetExprValue(frame).PyThrowIfNull())],
            [.. node.Defaults.Select(d => d.GetExprValue(frame).PyThrowIfNull())],
            node.VarArg?.Arg,
            node.KwArg?.Arg
            );
    }

    public bool TryParse(IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs, [NotNullWhen(true)] out PyArguments? result)
    {
        if (ParametersType is PyArgsDefParametersType.NoArgsOrKwargs)
            return TryParseEmpty(args, kwargs, out result);

        return TryParseGeneral(args, kwargs, out result);
    }

    private bool TryParseGeneral(IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs, [NotNullWhen(true)] out PyArguments? result)
    {
        result = null;

        var defaultsForPosonly = int.Max(0, Defaults.Length - Args.Length);
        if (args.Count < PosonlyArgs.Length - defaultsForPosonly)
            return false;

        List<PyObject> resultPosonlyArgs;

        if (args.Count >= PosonlyArgs.Length)
        {
            resultPosonlyArgs = [.. args.Take(PosonlyArgs.Length)];
        }
        else
        {
            resultPosonlyArgs = [.. args, .. Defaults.Take(PosonlyArgs.Length - args.Count)];
        }

        var resultArgs = Args.ToDictionary(static arg => arg, static _ => (PyObject?)null);


        var defaultsForArgs = int.Min(Args.Length, Defaults.Length);
        for (int i = 1; i <= defaultsForArgs; i++)
        {
            resultArgs[Args[^i]] = Defaults[^i];
        }

        var minLength = int.Min(Args.Length, args.Count - PosonlyArgs.Length);
        for (int i = 0; i < minLength; i++)
        {
            resultArgs[Args[i]] = args[i + PosonlyArgs.Length];
        }

        var resultExtraArgs = args.Skip(PosonlyArgs.Length + Args.Length).ToList();
        if (VarArg is null && resultExtraArgs.Count > 0)
            return false;

        var resultKwonlyArgs = Enumerable.Range(0, KwonlyArgs.Length).ToDictionary(i => KwonlyArgs[i], i => KwDefaults[i]);
        var resultExtraKwargs = new List<KeyValuePair<string, PyObject>>();

        foreach (var kwarg in kwargs)
        {
            if (resultArgs.TryGetValue(kwarg.Key, out var value))
            {
                resultArgs[kwarg.Key] = kwarg.Value;
            }
            else if (resultKwonlyArgs.TryGetValue(kwarg.Key, out value))
            {
                resultKwonlyArgs[kwarg.Key] = kwarg.Value;
            }
            else
            {
                if (KwArg is null)
                    return false;
                resultExtraKwargs.Add(kwarg);
            }
        }

        foreach (var value in resultArgs.Values)
        {
            if (value is null)
                return false;
        }

        foreach (var value in resultKwonlyArgs.Values)
        {
            if (value is null)
                return false;
        }

        result = new PyArguments(
            resultPosonlyArgs.Concat(Args.Select(arg => resultArgs[arg]!)),
            resultExtraArgs,
            KwonlyArgs.Select(arg => KeyValuePair.Create(arg, resultKwonlyArgs[arg]!)),
            resultExtraKwargs);

        return true;
    }

    private bool TryParseEmpty(IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs, [NotNullWhen(true)] out PyArguments? result)
    {
        Debug.Assert(ParametersType is PyArgsDefParametersType.NoArgsOrKwargs);
        if (args.Count is 0 && kwargs.Count is 0)
        {
            result = PyArguments.Empty;
            return true;
        }

        result = null;
        return false;
    }
}
