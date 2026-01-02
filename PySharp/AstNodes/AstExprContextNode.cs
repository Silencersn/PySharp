namespace PySharp.AstNodes;

public abstract class AstExprContextNode : AstNode
{
    public sealed override IEnumerable<AstNode> EnumerateSubNodes()
    {
        return [];
    }
}

public class LoadNode : AstExprContextNode;
public class StoreNode : AstExprContextNode;
public class DelNode : AstExprContextNode;
