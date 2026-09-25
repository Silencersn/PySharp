namespace PySharp.Compilation;

// Per-compile state. Speculative parses re-convert the same literal token,
// so a re-parse of the same source position must not warn twice; two
// compiles of the same source each warn, mirroring CPython (the parser
// suppresses its second pass via a flag, Parser/string_parser.c, and the
// warnings runtime has no cross-compile dedup — see Lib/codeop.py).
public sealed class CompileSession
{
    public HashSet<(int Line, int Offset, string Message)> WarnedSyntax { get; } = new();
}
