using PySharp.CodeAnalysis;

namespace PySharp.PyRuntime.Metadata;

public sealed class MetaInfo
{
    public string? SourceName;
    public string? FirstLine;
    public CodeTextPosition Start;
    public CodeTextPosition End;
    public CodeTextPosition CrucialStart;
    public CodeTextPosition CrucialEnd;
}



