namespace PySharp.PyRuntime.Metadata;

internal interface IMetaInfoProvider
{
    public bool OnlyStartInfo { get; }
    public MetaInfo? MetaInfo { get; }
}
