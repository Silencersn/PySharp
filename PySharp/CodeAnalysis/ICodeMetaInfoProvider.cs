namespace PySharp.CodeAnalysis;

internal interface ICodeMetaInfoProvider
{
    public static ICodeMetaInfoProvider Empty => EmptyCodeMetaInfoProvider.Shared;
    public CodeMetaInfo? MetaInfo { get; }

    private sealed class EmptyCodeMetaInfoProvider : ICodeMetaInfoProvider
    {
        internal static EmptyCodeMetaInfoProvider Shared { get; } = new();
        private EmptyCodeMetaInfoProvider() { }
        CodeMetaInfo? ICodeMetaInfoProvider.MetaInfo => null;
    }
}
