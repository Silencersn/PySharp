using System.Diagnostics;
using System.Globalization;

namespace PySharp.Runtime.Calls;

// How CPython initialize_locals (Python/ceval.c) rejects a call: every way of
// failing raises a differently shaped TypeError.
internal enum PyArgBindingErrorKind
{
    None = 0,

    // "f() takes 2 positional arguments but 3 were given"
    TooManyPositional,

    // "f() got multiple values for argument 'b'"
    MultipleValues,

    // "f() got an unexpected keyword argument 'zz'"
    UnexpectedKeyword,

    // "f() got some positional-only arguments passed as keyword arguments: 'a'"
    PositionalOnlyAsKeyword,

    // "f() missing 1 required positional argument: 'a'"
    MissingPositional,

    // "f() missing 1 required keyword-only argument: 'c'"
    MissingKeywordOnly,
}

// What the binder could not do, as PyArgsDef.Describe reports it: CPython's
// messages name the parameters that failed rather than the callable (whose
// qualname it takes from the executing frame), so the binder hands back the
// parameter set and the call site supplies the name.
internal readonly struct PyArgBindingError
{
    internal PyArgBindingErrorKind Kind { get; init; }

    // the offending keyword (MultipleValues, UnexpectedKeyword)
    internal string? Name { get; init; }

    // the parameters to name, in declaration order (PositionalOnlyAsKeyword,
    // MissingPositional, MissingKeywordOnly)
    internal string[]? Names { get; init; }

    // positional arguments the caller passed
    internal int Given { get; init; }

    // the callable's positional arity (co_argcount)
    internal int Arity { get; init; }

    // how many of those positional parameters carry a default
    // (func_defaults): a non-zero count turns the arity into "from A to B"
    internal int DefaultsCount { get; init; }

    // keyword-only parameters the caller bound, reported as an aside when
    // positional arguments overflowed
    internal int KwonlyGiven { get; init; }

    // keyword names a misspelling is matched against (init_locals of
    // initialize_locals builds the same list from the non-positional-only
    // parameters)
    internal string[]? Candidates { get; init; }

    internal string Format(string qualname)
    {
        Debug.Assert(Kind is not PyArgBindingErrorKind.None);

        var call = qualname + "()";
        switch (Kind)
        {
            case PyArgBindingErrorKind.TooManyPositional:
                {
                    var arity = DefaultsCount is 0
                        ? Arity.ToString(CultureInfo.InvariantCulture)
                        : string.Create(CultureInfo.InvariantCulture, $"from {Arity - DefaultsCount} to {Arity}");
                    var plural = DefaultsCount is not 0 || Arity is not 1;
                    var keywordOnly = KwonlyGiven is 0
                        ? string.Empty
                        : $" positional argument{(Given is not 1 ? "s" : string.Empty)}"
                            + string.Create(CultureInfo.InvariantCulture, $" (and {KwonlyGiven} keyword-only argument{(KwonlyGiven is not 1 ? "s" : string.Empty)})");
                    var verb = Given is 1 && KwonlyGiven is 0 ? "was" : "were";
                    return string.Create(CultureInfo.InvariantCulture,
                        $"{call} takes {arity} positional argument{(plural ? "s" : string.Empty)} but {Given}{keywordOnly} {verb} given");
                }

            case PyArgBindingErrorKind.MultipleValues:
                return string.Create(CultureInfo.InvariantCulture,
                    $"{call} got multiple values for argument '{Name}'");

            case PyArgBindingErrorKind.UnexpectedKeyword:
                {
                    var suggestion = Candidates is null ? null : PyNameSuggestions.Calculate(Candidates, Name!);
                    var hint = suggestion is null ? string.Empty : $". Did you mean '{suggestion}'?";
                    return $"{call} got an unexpected keyword argument '{Name}'{hint}";
                }

            case PyArgBindingErrorKind.PositionalOnlyAsKeyword:
                return $"{call} got some positional-only arguments passed as keyword arguments: '{string.Join(", ", Names!)}'";

            case PyArgBindingErrorKind.MissingPositional:
                return Missing(call, "positional", Names!);

            case PyArgBindingErrorKind.MissingKeywordOnly:
                return Missing(call, "keyword-only", Names!);

            default:
                return $"{call} got an invalid combination of arguments";
        }
    }

    // format_missing (ceval.c) deals with the joys of natural language:
    // "'a'", "'a' and 'b'", "'a', 'b', and 'c'"
    private static string Missing(string call, string kind, string[] names)
    {
        var list = names.Length switch
        {
            1 => $"'{names[0]}'",
            2 => $"'{names[0]}' and '{names[1]}'",
            _ => string.Join(", ", Quote(names, names.Length - 1)) + $", and '{names[^1]}'",
        };

        return string.Create(CultureInfo.InvariantCulture,
            $"{call} missing {names.Length} required {kind} argument{(names.Length is 1 ? string.Empty : "s")}: {list}");
    }

    private static IEnumerable<string> Quote(string[] names, int count)
    {
        for (var i = 0; i < count; i++)
            yield return $"'{names[i]}'";
    }
}
