using PySharp.Tokenization;

namespace PySharp.PyRuntime.Metadata;

public sealed class MetaInfo
{
    public string? SourceName;
    public string? FirstLine;
    public TokenPosition Start;
    public TokenPosition End;
    public TokenPosition CrucialStart;
    public TokenPosition CrucialEnd;
}



