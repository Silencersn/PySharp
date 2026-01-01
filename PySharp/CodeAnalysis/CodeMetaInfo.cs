namespace PySharp.CodeAnalysis;

public sealed class CodeMetaInfo
{
    public CodeSource? Source;
    public ReadOnlySpan<char> FirstLine => Source?.Code.TryGetLine(Start.Line, false, out var line) ?? false ? line : [];
    public CodeTextPosition Start;
    public CodeTextPosition End;
    public CodeTextPosition CrucialStart;
    public CodeTextPosition CrucialEnd;

    public bool HasStart => Start != CodeTextPosition.Empty;
    public bool HasEnd => End != CodeTextPosition.Empty;
    public bool HasCrucialStart => CrucialStart != CodeTextPosition.Empty;
    public bool HasCrucialEnd => CrucialEnd != CodeTextPosition.Empty;
    public bool HasRange => HasStart && HasEnd;
    public bool HasCrucialRange => HasCrucialStart && HasCrucialEnd;
}



