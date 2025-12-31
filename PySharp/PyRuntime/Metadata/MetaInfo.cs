using PySharp.CodeAnalysis;

namespace PySharp.PyRuntime.Metadata;

public sealed class MetaInfo
{
    public CodeSource? Source;
    public string? FirstLine;
    public CodeTextPosition Start;
    public CodeTextPosition End;
    public CodeTextPosition CrucialStart;
    public CodeTextPosition CrucialEnd;
}



