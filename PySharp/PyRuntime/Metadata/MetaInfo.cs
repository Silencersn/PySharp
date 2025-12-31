using PySharp.CodeAnalysis;

namespace PySharp.PyRuntime.Metadata;

public sealed class MetaInfo
{
    public CodeSource? Source;
    public ReadOnlySpan<char> FirstLine => Source?.Code.TryGetLine(Start.Line, true, out var line) ?? false ? line : [];
    public CodeTextPosition Start;
    public CodeTextPosition End;
    public CodeTextPosition CrucialStart;
    public CodeTextPosition CrucialEnd;
}



