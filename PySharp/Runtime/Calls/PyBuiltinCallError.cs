namespace PySharp.Runtime.Calls;

using PySharp.Modules.Builtins;

// How CPython rejects a call to a C-implemented builtin function or method
// descriptor: the message family follows the calling convention the function
// is defined with, not the Python-level binder (Objects/methodobject.c,
// Objects/descrobject.c and the Argument Clinic generated parsers):
//
//   - vectorcall NOARGS/O and the descriptor equivalents reject every
//     overflow through "%U takes no arguments (N given)" / "%U takes exactly
//     one argument (N given)", and every keyword through "%U takes no
//     keyword arguments", with %U from _PyObject_FunctionStr;
//   - a METH_FASTCALL signature whose parameters are all positional-only
//     counts through _PyArg_CheckPositional, which names the callable by the
//     bare parser name without parentheses ("get expected at least 1
//     argument, got 0");
//   - a signature with keyword-capable parameters runs the clinic parser,
//     whose overflows are "%s() takes at least/at most N positional
//     argument(s) (M given)" and "%s() missing required argument 'x' (pos N)";
//   - signatures with *args or **kwargs keep the binder family, whose
//     keyword messages already mirror the parser's.
internal static class PyBuiltinCallError
{
    // What the CPython call machinery would report for a call the binder
    // rejected, or null when the failure is not covered by a C family and
    // the Python-level binder message stands (PyArgBindingError.Format).
    // `bareName` is the name CPython passes to its parser (no owner prefix,
    // no parentheses); `fullName` is the _PyObject_FunctionStr form the
    // vectorcall families spell out ("str.join").
    internal static string? Format(PyArgsDef def, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs, string bareName, string fullName)
    {
        var given = args.Count;

        if (def.VarArg is not null || def.KwArg is not null)
            return null;

        var positional = def.PosonlyArgs.Length + def.Args.Length;

        if (kwargs.Count is not 0)
        {
            // a signature with no keyword-capable parameter rejects every
            // keyword up front, by the full function name
            if (def.Args.Length is 0 && def.KwonlyArgs.Length is 0)
                return $"{fullName}() takes no keyword arguments";

            // keyword-shaped failures (unexpected keyword, positional-only
            // passed as keyword, multiple values) are parser messages: the
            // bare name with parentheses, which is what the binder formats
            return def.Describe(args, kwargs).Format(bareName);
        }

        if (positional is 0)
            return $"{fullName}() takes no arguments ({given} given)";

        if (positional is 1 && def.Defaults.Length is 0)
            return $"{fullName}() takes exactly one argument ({given} given)";

        var required = positional - def.Defaults.Length;

        // every parameter positional-only: the _PyArg_CheckPositional count
        if (def.Args.Length is 0)
        {
            if (given < required)
            {
                var atLeast = required != positional ? "at least " : string.Empty;
                return $"{bareName} expected {atLeast}{required} argument{(required is 1 ? string.Empty : "s")}, got {given}";
            }

            if (given > positional)
            {
                var atMost = required != positional ? "at most " : string.Empty;
                return $"{bareName} expected {atMost}{positional} argument{(positional is 1 ? string.Empty : "s")}, got {given}";
            }

            return null;
        }

        // keyword-capable parameters: the clinic parser count
        if (given < required)
        {
            // a missing positional-only parameter defers to the overflow
            // shape (Python/getargs.c skips the format walk to the `|`);
            // a missing keyword-capable one is named with its position
            if (given < def.PosonlyArgs.Length)
                return $"{bareName}() takes at least {required} positional argument{(required is 1 ? string.Empty : "s")} ({given} given)";

            var missingIndex = given;
            var missingName = def.Args[missingIndex - def.PosonlyArgs.Length];
            return $"{bareName}() missing required argument '{missingName}' (pos {missingIndex + 1})";
        }

        if (given > positional)
        {
            // the parser's first overflow check (nargs + nkwargs > len,
            // Python/getargs.c) spells "argument" without "positional"
            var bound = required == positional ? "exactly" : "at most";
            return $"{bareName}() takes {bound} {positional} argument{(positional is 1 ? string.Empty : "s")} ({given} given)";
        }

        return null;
    }
}
