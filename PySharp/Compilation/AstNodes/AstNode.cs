using PySharp.Compilation.CodeAnalysis;

namespace PySharp.Compilation.AstNodes;

public abstract partial class AstNode : ICodeMetaInfoProvider
{
    public CodeMetaInfo? MetaInfo { get; internal set; }

    public abstract IEnumerable<AstNode> EnumerateSubNodes();
}
