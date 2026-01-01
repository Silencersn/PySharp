namespace PySharp.CodeAnalysis;

public sealed class CodeMetaInfo
{
    public required CodeSource Source { get; init; }
    public ReadOnlySpan<char> FirstLine => Source.Code.GetLineOrDefault(Start.Line, false);
    public CodeTextPosition Start;
    public CodeTextPosition End;
    public CodeTextPosition CrucialStart;
    public CodeTextPosition CrucialEnd;

    public bool HasStart => !Start.IsEmpty;
    public bool HasEnd => !End.IsEmpty;
    public bool HasCrucialStart => !CrucialStart.IsEmpty;
    public bool HasCrucialEnd => !CrucialEnd.IsEmpty;
    public bool HasRange => HasStart && HasEnd;
    public bool HasCrucialRange => HasCrucialStart && HasCrucialEnd;
}



