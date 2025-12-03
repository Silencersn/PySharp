namespace PySharp.AstNodes;

public class AstArgNode : AstNode
{
    public string Arg { get; }

    public AstArgNode(string arg)
    {
        Arg = arg;
    }
}
