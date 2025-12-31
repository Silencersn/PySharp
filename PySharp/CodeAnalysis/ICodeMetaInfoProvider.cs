namespace PySharp.CodeAnalysis;

internal interface ICodeMetaInfoProvider
{
    public bool OnlyStartInfo { get; }
    public CodeMetaInfo? MetaInfo { get; }
}
