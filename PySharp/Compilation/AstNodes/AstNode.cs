using PySharp.Compilation.CodeAnalysis;

namespace PySharp.Compilation.AstNodes;

public abstract partial class AstNode
{
    public ValueCodeMetaInfo MetaInfo { get; internal set; }
}
