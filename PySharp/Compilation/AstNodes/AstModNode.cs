using System.Collections.Immutable;

namespace PySharp.Compilation.AstNodes;

public abstract class AstModNode : AstNode;

public class ModuleNode : AstModNode
{
    internal ModuleNode(ImmutableArray<AstStmtNode> body)
    {
        Body = body;
    }

    public ImmutableArray<AstStmtNode> Body { get; }

}

public class ExpressionNode : AstModNode
{
    public AstExprNode Body { get; }

    internal ExpressionNode(AstExprNode body)
    {
        Body = body;
    }

}

public class InteractiveNode : AstModNode
{
    public ImmutableArray<AstStmtNode> Body { get; }

    internal InteractiveNode(ImmutableArray<AstStmtNode> body)
    {
        Body = body;
    }
}
