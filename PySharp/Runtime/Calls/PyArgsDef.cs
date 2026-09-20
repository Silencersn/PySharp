using PySharp.Compilation.AstNodes;
using PySharp.Modules.Builtins;
using PySharp.Utility;
using System.Buffers;
using System.Diagnostics;
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

    // replaced wholesale by a Python-level __defaults__ assignment
    // (func_set_defaults), which also moves the count of parameters that
    // lack a value
    internal PyObject[] Defaults { get; set; }
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

        void IDisposable.Dispose()
        {
            if (_poolArray is not null)
                ArrayPool<PyObject>.Shared.Return(_poolArray, clearArray: true);
            _poolArray = null;
        }

        internal static Span<PyObject> AsSpan(ref readonly Buffer buffer)
        {
            if (buffer._poolArray is not null)
                return buffer._poolArray;

            ref readonly var aptr = ref buffer._inlineArray;
            ref var ptr = ref Unsafe.As<InlinePyObjectArray, PyObject>(ref Unsafe.AsRef(in aptr));
            return MemoryMarshal.CreateSpan(ref ptr, InlinePyObjectArray.Length);
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
            if (parameters[i].StartsWith('*') && !parameters[i].StartsWith("**", StringComparison.Ordinal))
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
        if (parameters.Length > 0 && parameters[^1].StartsWith("**", StringComparison.Ordinal))
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

    // Binds a call the way CPython initialize_locals does (Python/ceval.c):
    // keywords are bound first, then the positional overflow is reported, and
    // only then the missing positional and keyword-only parameters are filled
    // in from the defaults or reported as missing.
    //
    // This is the hot path, so a failure only answers "no": what a message
    // would name is collected by Describe, on the exception path only.
    internal bool TryParse(IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs, in Buffer buffer, out PyArguments result)
    {
        return Bind(args, kwargs, Buffer.AsSpan(in buffer), describe: false, out result, out _);
    }

    // The message side of a failure: binds the same call a second time with the
    // detail switched on. The binding is pure, so the second pass reports what
    // the first one did; the fresh buffer matters because the failed attempt
    // left its own values in the caller's.
    internal PyArgBindingError Describe(IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        using var buffer = CreateBuffer();
        var bound = Bind(args, kwargs, Buffer.AsSpan(in buffer), describe: true, out _, out var error);
        Debug.Assert(!bound, "Describe reports a call the binding already rejected");

        return error;
    }

    // `describe` only decides whether a failure carries what its message needs;
    // it never decides whether the call binds.
    private bool Bind(IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs, Span<PyObject> span, bool describe, out PyArguments result, out PyArgBindingError error)
    {
        Debug.Assert(span.Length >= BufferLength);
        span = span[..BufferLength];

        if (ParametersType is PyArgsDefParametersType.NoAnyArgs)
            return TryParse_NoAnyArgs(args, kwargs, describe, out result, out error);

        if (ParametersType is PyArgsDefParametersType.OnlyArgs && kwargs.Count is 0)
            return TryParse_OnlyArgs(args, span, describe, out result, out error);

        if (kwargs.Count is 0)
            return TryParseGeneral(args, span, describe, out result, out error);

        return TryParseGeneral(args, kwargs, span, describe, out result, out error);
    }

    private bool TryParseGeneral(IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs, Span<PyObject> buffer, bool describe, out PyArguments result, out PyArgBindingError error)
    {
        result = default;
        error = default;

        var positionalCount = PosonlyArgs.Length + Args.Length;
        var resultArgs = buffer[..positionalCount];
        var resultKwargs = buffer[positionalCount..];

        CopyPositionalArgs(args, resultArgs, out var resultExtraArgs);

        List<KeyValuePair<string, PyObject>>? resultExtraKwargs = null;
        var positionalOnlyChecked = false;

        foreach (var pair in kwargs)
        {
            var index = KwonlyArgs.IndexOf(pair.Key);

            if (index is not -1)
            {
                // no duplication, guaranteed by the compiler
                resultKwargs[index] = pair.Value;
                continue;
            }

            if ((index = Args.IndexOf(pair.Key)) is not -1)
            {
                var offset = PosonlyArgs.Length + index;
                if (resultArgs[offset] is not null)
                {
                    if (describe)
                        error = new PyArgBindingError { Kind = PyArgBindingErrorKind.MultipleValues, Name = pair.Key };

                    return false;
                }

                resultArgs[offset] = pair.Value;
                continue;
            }

            if (KwArg is not null)
            {
                (resultExtraKwargs ??= []).Add(KeyValuePair.Create(pair.Key, pair.Value));
                continue;
            }

            // a positional-only name the call also passed by name is reported
            // before the keyword that found no parameter
            if (describe)
            {
                if (PosonlyArgs.Length is not 0 && !positionalOnlyChecked)
                {
                    positionalOnlyChecked = true;
                    if (CollectPositionalOnlyNames(kwargs, out var positionalOnlyNames))
                    {
                        error = new PyArgBindingError { Kind = PyArgBindingErrorKind.PositionalOnlyAsKeyword, Names = positionalOnlyNames };
                        return false;
                    }
                }

                error = new PyArgBindingError
                {
                    Kind = PyArgBindingErrorKind.UnexpectedKeyword,
                    Name = pair.Key,
                    Candidates = [.. Args, .. KwonlyArgs],
                };
            }

            return false;
        }

        if (!TryFillAndCheck(args.Count, resultArgs, resultKwargs, describe, out error))
            return false;

        result = new PyArguments(this, buffer, resultExtraArgs, resultExtraKwargs);
        return true;
    }

    private bool TryParseGeneral(IReadOnlyList<PyObject> args, Span<PyObject> buffer, bool describe, out PyArguments result, out PyArgBindingError error)
    {
        result = default;
        error = default;

        var positionalCount = PosonlyArgs.Length + Args.Length;
        var resultArgs = buffer[..positionalCount];
        var resultKwargs = buffer[positionalCount..];

        CopyPositionalArgs(args, resultArgs, out var resultExtraArgs);

        if (!TryFillAndCheck(args.Count, resultArgs, resultKwargs, describe, out error))
            return false;

        result = new PyArguments(this, buffer, resultExtraArgs, null);
        return true;
    }

    // The positional arguments fill the leading slots; whatever exceeds the
    // signature lands in *args when there is one
    private void CopyPositionalArgs(IReadOnlyList<PyObject> args, Span<PyObject> resultArgs, out PyObject[]? resultExtraArgs)
    {
        resultExtraArgs = null;

        var given = int.Min(args.Count, resultArgs.Length);
        for (int i = 0; i < given; i++)
            resultArgs[i] = args[i];

        if (VarArg is not null && args.Count > resultArgs.Length)
            resultExtraArgs = [.. args.Skip(resultArgs.Length)];
    }

    // The tail of the argument binding: an overflowing positional argument set
    // is reported before the missing ones, then the defaults fill the slots
    // the call left empty - the positional ones first, so that the message for
    // a missing keyword-only parameter only names parameters without a default
    private bool TryFillAndCheck(int given, Span<PyObject> resultArgs, Span<PyObject> resultKwargs, bool describe, out PyArgBindingError error)
    {
        error = default;

        if (VarArg is null && given > resultArgs.Length)
        {
            if (describe)
            {
                var kwonlyGiven = 0;
                foreach (var slot in resultKwargs)
                {
                    if (slot is not null)
                        kwonlyGiven++;
                }

                error = new PyArgBindingError
                {
                    Kind = PyArgBindingErrorKind.TooManyPositional,
                    Given = given,
                    Arity = resultArgs.Length,
                    DefaultsCount = Defaults.Length,
                    KwonlyGiven = kwonlyGiven,
                };
            }

            return false;
        }

        var required = resultArgs.Length - Defaults.Length;
        if (CollectMissing(resultArgs, required, PosonlyArgs, Args, describe, out var missingPositional))
        {
            if (describe)
                error = new PyArgBindingError { Kind = PyArgBindingErrorKind.MissingPositional, Names = missingPositional };

            return false;
        }

        for (int i = required; i < resultArgs.Length; i++)
        {
            if (resultArgs[i] is null)
                resultArgs[i] = Defaults[i - required];
        }

        // keyword-only defaults only fill the slots the call left empty, so a
        // parameter bound by keyword keeps its value
        for (int i = 0; i < resultKwargs.Length; i++)
        {
            if (resultKwargs[i] is null && KwDefaults[i] is { } kwDefault)
                resultKwargs[i] = kwDefault;
        }

        if (CollectMissing(resultKwargs, resultKwargs.Length, [], KwonlyArgs, describe, out var missingKwonly))
        {
            if (describe)
                error = new PyArgBindingError { Kind = PyArgBindingErrorKind.MissingKeywordOnly, Names = missingKwonly };

            return false;
        }

        return true;
    }

    // Every parameter left without a value, in declaration order - CPython
    // scans the whole positional block and names the empty slots. Without
    // detail the scan stops at the first empty slot instead of naming them.
    private static bool CollectMissing(Span<PyObject> slots, int required, ReadOnlySpan<string> leadingNames, ReadOnlySpan<string> trailingNames, bool describe, out string[] missing)
    {
        List<string>? collected = null;

        for (int i = 0; i < required; i++)
        {
            if (slots[i] is not null)
                continue;

            if (!describe)
            {
                missing = [];
                return true;
            }

            (collected ??= []).Add(i < leadingNames.Length ? leadingNames[i] : trailingNames[i - leadingNames.Length]);
        }

        missing = collected is null ? [] : [.. collected];
        return collected is not null;
    }

    // positional_only_passed_as_keyword (ceval.c): every parameter of the
    // positional-only block that the call also passed by name, in declaration
    // order - a clash anywhere in the keyword set is reported from the first
    // keyword that has no parameter
    private bool CollectPositionalOnlyNames(IReadOnlyDictionary<string, PyObject> kwargs, out string[] names)
    {
        List<string>? conflicts = null;

        foreach (var posonlyName in PosonlyArgs)
        {
            if (kwargs.ContainsKey(posonlyName))
                (conflicts ??= []).Add(posonlyName);
        }

        names = conflicts is null ? [] : [.. conflicts];
        return conflicts is not null;
    }

    private bool TryParse_NoAnyArgs(IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs, bool describe, out PyArguments result, out PyArgBindingError error)
    {
        Debug.Assert(ParametersType is PyArgsDefParametersType.NoAnyArgs);

        result = PyArguments.Empty;
        error = default;

        // "def f()" has no parameter to bind a keyword to, and keywords are
        // still reported before the positional overflow
        if (kwargs.Count is not 0)
        {
            if (describe)
            {
                // the message names the first keyword, in insertion order
                foreach (var pair in kwargs)
                {
                    error = new PyArgBindingError { Kind = PyArgBindingErrorKind.UnexpectedKeyword, Name = pair.Key, Candidates = [] };
                    break;
                }
            }

            return false;
        }

        if (args.Count is not 0)
        {
            if (describe)
                error = new PyArgBindingError { Kind = PyArgBindingErrorKind.TooManyPositional, Given = args.Count };

            return false;
        }

        return true;
    }

    private bool TryParse_OnlyArgs(IReadOnlyList<PyObject> args, Span<PyObject> buffer, bool describe, out PyArguments result, out PyArgBindingError error)
    {
        Debug.Assert(ParametersType is PyArgsDefParametersType.OnlyArgs);
        Debug.Assert(Args.Length == BufferLength);

        result = default;
        error = default;
        var argsCount = args.Count;

        if (argsCount > Args.Length)
        {
            if (describe)
            {
                error = new PyArgBindingError
                {
                    Kind = PyArgBindingErrorKind.TooManyPositional,
                    Given = argsCount,
                    Arity = Args.Length,
                    DefaultsCount = Defaults.Length,
                };
            }

            return false;
        }

        var required = Args.Length - Defaults.Length;
        if (argsCount < required)
        {
            if (describe)
            {
                error = new PyArgBindingError
                {
                    Kind = PyArgBindingErrorKind.MissingPositional,
                    Names = [.. Args.AsSpan()[argsCount..required]],
                };
            }

            return false;
        }

        for (int i = 0; i < argsCount; i++)
            buffer[i] = args[i];

        for (int i = argsCount; i < Args.Length; i++)
            buffer[i] = Defaults[i - required];

        result = new PyArguments(this, buffer, null, null);
        return true;
    }
}
