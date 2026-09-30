using PySharp.Modules.Builtins;
using PySharp.Runtime.Comparison;

namespace PySharp.Compilation;

// Per-compile state. Speculative parses re-convert the same literal token,
// so a re-parse of the same source position must not warn twice; two
// compiles of the same source each warn, mirroring CPython (the parser
// suppresses its second pass via a flag, Parser/string_parser.c, and the
// warnings runtime has no cross-compile dedup — see Lib/codeop.py).
public sealed class CompileSession
{
    public HashSet<(int Line, int Offset, string Message)> WarnedSyntax { get; } = [];

    // The compile unit's canonical constants: the one object this compilation
    // uses for every constant equal to a given value. This is CPython's
    // c_const_cache (Python/compile.c compiler_setup, consulted by
    // _PyCompile_AddConst through merge_consts_recursive), which belongs to the
    // compilation rather than to a code object — the module body and every
    // nested function/lambda/class/generator body it builds resolve an equal
    // constant to this same object. A fresh session starts empty, which is why
    // two execs do not share.
    private readonly HashSet<PyObject> _canonicalConstants = new(PyObjectConstEqualityComparer.Shared);

    /// <summary>
    /// Returns the canonical object for a constant equal to
    /// <paramref name="value"/>, recording <paramref name="value"/> the first
    /// time it is seen.
    ///
    /// Like <c>merge_consts_recursive</c>, a constant tuple is canonicalized
    /// element by element and rebuilt when an element changed, so a bare
    /// constant and the same constant nested in a tuple are one object
    /// (<c>("a b",)[0] is "a b"</c>). Identity is decided by
    /// <see cref="PyObjectConstEqualityComparer"/>, CPython's
    /// <c>_PyCode_ConstantKey</c>: the type participates, so 1 / True / 1.0 stay
    /// three distinct constants.
    /// </summary>
    internal PyObject CanonicalizeConstant(PyObject value)
    {
        if (value is PyTupleObject tuple)
        {
            PyObject[]? canonicalItems = null;
            for (int i = 0; i < tuple.Count; i++)
            {
                var item = tuple[i];
                var canonicalItem = CanonicalizeConstant(item);

                if (canonicalItems is null)
                {
                    if (ReferenceEquals(canonicalItem, item))
                        continue;

                    // The first changed element: elements before i are already
                    // canonical, so copy them as they are and fill the rest in
                    // the remaining iterations.
                    canonicalItems = new PyObject[tuple.Count];
                    for (int j = 0; j < canonicalItems.Length; j++)
                        canonicalItems[j] = tuple[j];
                }

                canonicalItems[i] = canonicalItem;
            }

            // A fresh tuple per rebuild; its canonical object is what the table
            // hands out to every later equal display.
            if (canonicalItems is not null)
                value = PyTupleObject.CreateTuple(canonicalItems);
        }

        if (_canonicalConstants.TryGetValue(value, out var existing))
            return existing;

        _canonicalConstants.Add(value);
        return value;
    }
}
