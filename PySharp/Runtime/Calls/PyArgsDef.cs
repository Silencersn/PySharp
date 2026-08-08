using PySharp.Compilation.AstNodes;
using PySharp.Modules.Builtins;
using PySharp.Utility;
using System.Buffers;
using System.Collections.Frozen;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PySharp.Runtime.Calls;

internal enum PyArgsDefParametersType
{
    Unknown = 0,

    // def foo():
    NoAnyArgs,

    // def foo(arg0, arg1=1, arg2=2)
    OnlyArgs,
}

public sealed class PyArgsDef
{
    public static PyArgsDef Empty { get; } = new([], [], [], [], [], null, null);

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
            ParametersType = PyArgsDefParametersType.NoAnyArgs;
        else if (PosonlyArgs.Length is 0 && KwonlyArgs.Length is 0 && VarArg is null && KwArg is null)
            ParametersType = PyArgsDefParametersType.OnlyArgs;
    }

    internal PyArgsDefParametersType ParametersType { get; }
    internal string[] PosonlyArgs { get; }
    internal string[] Args { get; }
    internal string[] KwonlyArgs { get; }
    internal PyObject?[] KwDefaults { get; }
    internal PyObject[] Defaults { get; }
    internal string? VarArg { get; }
    internal string? KwArg { get; }
    internal int BufferLength => PosonlyArgs.Length + Args.Length + KwonlyArgs.Length;

    internal ref struct Buffer : IDisposable
    {
        private InlinePyObjectArray _inlineArray;
        private PyObject[]? _poolArray;

        public Buffer(PyObject[]? poolArray = null)
        {
            _poolArray = poolArray;
        }

        internal Span<PyObject> Span
        {
            get
            {
                if (_poolArray is not null)
                    return _poolArray;
                ref var ptr = ref Unsafe.As<InlinePyObjectArray, PyObject>(ref _inlineArray);
                return MemoryMarshal.CreateSpan(ref ptr, InlinePyObjectArray.Length);
            }
        }

        void IDisposable.Dispose()
        {
            Span.Clear();
            if (_poolArray is not null)
                ArrayPool<PyObject>.Shared.Return(_poolArray);
            _poolArray = null;
        }

        public static implicit operator Span<PyObject>(Buffer buffer)
        {
            return buffer.Span;
        }
    }

    internal Buffer CreateBuffer()
    {
        if (BufferLength <= InlinePyObjectArray.Length)
            return new Buffer();

        var array = ArrayPool<PyObject>.Shared.Rent(BufferLength);
        Array.Clear(array);
        return new Buffer(array);
    }

    internal static PyArgsDef FromDef(params ReadOnlySpan<string> parameters)
    {
        // this is for internal use,
        // so it is assumed that all parameters conform to Python syntax

        scoped ReadOnlySpan<string> posonlyArgs, args, kwonlyArgs;
        string? varArg = null, kwArg = null;

        // possible situation here:
        // ...
        // ... /
        // ... *
        // ... **
        // ... / ... *
        // ... / ... **
        // ... * ... **
        // ... / ... * ... **
        //
        var indexOfSlash = parameters.IndexOf("/");
        if (indexOfSlash is not -1)
        {
            posonlyArgs = parameters[..indexOfSlash];
            parameters = parameters[(indexOfSlash + 1)..];
        }
        else
        {
            posonlyArgs = [];
        }

        // possible situation here:
        // ...
        // ... *
        // ... **
        // ... * ... **
        //
        var indexOfStar = -1;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].StartsWith('*') && !parameters[i].StartsWith("**"))
            {
                indexOfStar = i;
                varArg = parameters[i] is "*" ? null : parameters[i][1..];
                break;
            }
        }
        if (indexOfStar is not -1)
        {
            args = parameters[..indexOfStar];
            parameters = parameters[(indexOfStar + 1)..];
        }
        else
        {
            // this assignment here will be overwritten,
            // it's just to pass compilation.
            args = [];
        }


        // possible situation here:
        // ...
        // ... (<- args) **
        // ... (<- kwonlyArgs) **
        //
        if (parameters.Length > 0 && parameters[^1].StartsWith("**"))
        {
            kwArg = parameters[^1][..^2];
            parameters = parameters[..^1];
        }

        // possible situation here:
        // ... (<- args)
        // ... (<- kwonlyArgs) 
        //
        if (indexOfStar is not -1)
        {
            kwonlyArgs = parameters;
        }
        else
        {
            args = parameters;
            kwonlyArgs = [];
        }

        List<PyObject> defaults = [];
        string[] posonlyArgsResult = new string[posonlyArgs.Length];
        for (int i = 0; i < posonlyArgs.Length; i++)
        {
            var arg = posonlyArgs[i];
            var indexOfEqual = arg.IndexOf('=');
            if (indexOfEqual is not -1)
            {
                posonlyArgsResult[i] = arg[..indexOfEqual];
                var d = LiteralParser.LiteralEval(arg.AsSpan()[(indexOfEqual + 1)..]);
                defaults.Add(d);
            }
            else
            {
                posonlyArgsResult[i] = arg;
            }
        }

        string[] argsResult = new string[args.Length];
        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            var indexOfEqual = arg.IndexOf('=');
            if (indexOfEqual is not -1)
            {
                argsResult[i] = arg[..indexOfEqual];
                var d = LiteralParser.LiteralEval(arg.AsSpan()[(indexOfEqual + 1)..]);
                defaults.Add(d);
            }
            else
            {
                argsResult[i] = arg;
            }
        }

        PyObject?[] kwDefaults = new PyObject?[kwonlyArgs.Length];
        string[] kwonlyArgsResult = new string[kwonlyArgs.Length];
        for (int i = 0; i < kwonlyArgs.Length; i++)
        {
            var kwarg = kwonlyArgs[i];
            var indexOfEqual = kwarg.IndexOf('=');
            if (indexOfEqual is not -1)
            {
                kwonlyArgsResult[i] = kwarg[..indexOfEqual];
                kwDefaults[i] = LiteralParser.LiteralEval(kwarg.AsSpan()[(indexOfEqual + 1)..]);
            }
            else
            {
                kwonlyArgsResult[i] = kwarg;
            }
        }

        return new PyArgsDef(posonlyArgsResult, argsResult, kwonlyArgsResult, kwDefaults, [.. defaults], varArg, kwArg);
    }

    internal static PyArgsDef FromCodeObjectAndDefaults(PyCodeObject code, PyObject?[] kwDefaults, PyObject[] defaults)
    {
        var args = code.VarNames.AsSpan()[..(code.ArgCount + code.KwOnlyArgCount)];
        return new PyArgsDef(
            [.. args[..code.PosOnlyArgCount]],
            [.. args[code.PosOnlyArgCount..code.ArgCount]],
            [.. args[code.ArgCount..]],
            kwDefaults,
            defaults,
            code.VarArg,
            code.KwArg
            );
    }

    internal bool TryParse(IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs, Span<PyObject> buffer, out PyArguments result)
    {
        Debug.Assert(buffer.Length >= BufferLength);
        buffer = buffer[..BufferLength];

        if (ParametersType is PyArgsDefParametersType.NoAnyArgs)
            return TryParse_NoAnyArgs(args, kwargs, out result);

        if (ParametersType is PyArgsDefParametersType.OnlyArgs)
        {
            if (kwargs.Count is 0)
                return TryParse_OnlyArgs(args, buffer, out result);
        }

        if (kwargs.Count is 0)
            return TryParseGeneral(args, buffer, out result);

        return TryParseGeneral(args, kwargs, buffer, out result);
    }

    private bool TryParseArgsPart(IReadOnlyList<PyObject> args, Span<PyObject> resultArgs, [NotNullWhen(true)] out PyObject[]? resultExtraArgs)
    {
        resultExtraArgs = null;

        var defaultsCountForPosonly = int.Max(0, Defaults.Length - Args.Length);
        var leastPosonlyArgsCount = PosonlyArgs.Length - defaultsCountForPosonly;
        if (args.Count < leastPosonlyArgsCount)
            return false;

        if (args.Count > resultArgs.Length && VarArg is null)
            return false;

        for (int i = 0; i < PosonlyArgs.Length; i++)
        {
            if (i < args.Count)
                resultArgs[i] = args[i];
            else
                resultArgs[i] = Defaults[i - leastPosonlyArgsCount];
        }

        if (Defaults.Length > Args.Length)
            Defaults.AsSpan()[^Args.Length..].CopyTo(resultArgs[^Args.Length..]);
        else
            Defaults.CopyTo(resultArgs[^Defaults.Length..]);

        var maxLength = int.Min(resultArgs.Length, args.Count);
        for (int i = PosonlyArgs.Length; i < maxLength; i++)
            resultArgs[i] = args[i];

        resultExtraArgs = [];
        if (VarArg is not null && args.Count > resultArgs.Length)
            resultExtraArgs = [.. args.Skip(resultArgs.Length)];

        return true;
    }

    private bool TryParseGeneral(IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs, Span<PyObject> buffer, out PyArguments result)
    {
        result = default;

        var totalArgsCount = PosonlyArgs.Length + Args.Length;
        var resultArgs = buffer[..totalArgsCount];

        if (!TryParseArgsPart(args, resultArgs, out var resultExtraArgs))
            return false;

        var resultKwargs = buffer[totalArgsCount..];
        KwDefaults.CopyTo(resultKwargs!);

        Dictionary<string, PyObject>? resultExtraKwargs = null;

        int index;
        foreach (var pair in kwargs)
        {
            index = KwonlyArgs.IndexOf(pair.Key);
            
            if (index is not -1)
            {
                // no duplication, guaranteed by the compiler
                resultKwargs[index] = pair.Value;
            }
            else if ((index = Args.IndexOf(pair.Key)) is not -1)
            {
                var offset = PosonlyArgs.Length + index;
                ref var value = ref resultArgs[offset];
                if (value is not null && offset < args.Count)
                    return false;
                value = pair.Value;
            }
            else if (KwArg is not null)
            {
                (resultExtraKwargs ??= [])[pair.Key] = pair.Value;
            }
            else
            {
                return false;
            }
        }

        foreach (var arg in resultArgs[^Args.Length..])
        {
            if (arg is null)
                return false;
        }

        foreach (var value in resultKwargs)
        {
            if (value is null)
                return false;
        }

        result = new PyArguments(this, buffer, resultExtraArgs,
            (IReadOnlyDictionary<string, PyObject>)resultExtraKwargs! ?? FrozenDictionary<string, PyObject>.Empty);
        return true;
    }

    private bool TryParseGeneral(IReadOnlyList<PyObject> args, Span<PyObject> buffer, out PyArguments result)
    {
        result = default;

        if (KwonlyArgs.Length > 0 && KwDefaults.Any(static value => value is null))
            return false;

        var totalArgsCount = PosonlyArgs.Length + Args.Length;
        var resultArgs = buffer[..totalArgsCount];

        if (!TryParseArgsPart(args, resultArgs, out var resultExtraArgs))
            return false;

        foreach (var arg in resultArgs[^Args.Length..])
        {
            if (arg is null)
                return false;
        }

        KwDefaults.CopyTo(buffer[resultArgs.Length..]!);

        result = new PyArguments(this, buffer, resultExtraArgs, FrozenDictionary<string, PyObject>.Empty);
        return true;
    }

    private bool TryParse_NoAnyArgs(IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs, out PyArguments result)
    {
        Debug.Assert(ParametersType is PyArgsDefParametersType.NoAnyArgs);

        result = PyArguments.Empty;
        return args.Count is 0 && kwargs.Count is 0;
    }

    private bool TryParse_OnlyArgs(IReadOnlyList<PyObject> args, Span<PyObject> buffer, out PyArguments result)
    {
        Debug.Assert(ParametersType is PyArgsDefParametersType.OnlyArgs);
        Debug.Assert(Args.Length == BufferLength);

        result = default;
        var argsCount = args.Count;

        if (argsCount + Defaults.Length < Args.Length)
            return false;

        if (argsCount > Args.Length)
            return false;

        for (int i = 0; i < argsCount; i++)
            buffer[i] = args[i];

        if (argsCount < Args.Length)
        {
            var needDefaultsCount = Args.Length - argsCount;
            Defaults.AsSpan()[^needDefaultsCount..].CopyTo(buffer[^needDefaultsCount..]);
        }

        result = new PyArguments(this, buffer, [], FrozenDictionary<string, PyObject>.Empty);
        return true;
    }
}
