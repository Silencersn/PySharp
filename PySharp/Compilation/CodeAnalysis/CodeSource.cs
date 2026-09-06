namespace PySharp.Compilation.CodeAnalysis;

public sealed class CodeSource
{
    public string Name { get; }
    public CodeText Code { get; }

    // Syntax warnings already emitted, keyed by source position so a
    // re-parse of the same token cannot warn twice; per compile, mirroring
    // CPython's one conversion per literal.
    public HashSet<(int Line, int Offset, string Message)> WarnedSyntax { get; } = new();

    public CodeSource(string name, string code)
    {
        Name = name;
        Code = new CodeText(code);
    }
}
