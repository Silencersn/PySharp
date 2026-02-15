using PySharp.PyModules.Builtins;
using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;
using System.Collections.Immutable;

namespace PySharp.AstNodes;

public abstract class AstModNode : AstNode
{
}

public class ModuleNode : AstModNode
{
    internal ModuleNode(ImmutableArray<AstStmtNode> body)
    {
        Body = body;
    }

    public ImmutableArray<AstStmtNode> Body { get; }

    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        return Body;
    }
}

public class ExpressionNode : AstModNode
{
    public AstExprNode Body { get; }

    internal ExpressionNode(AstExprNode body)
    {
        Body = body;
    }

    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        yield return Body;
    }
}

public class InteractiveNode : AstModNode
{
    public ImmutableArray<AstStmtNode> Body { get; }

    internal InteractiveNode(ImmutableArray<AstStmtNode> body)
    {
        Body = body;
    }
    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        return Body;
    }
}