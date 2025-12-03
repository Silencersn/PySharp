namespace PySharp.AstNodes;

public class AstArgumentsNode : AstNode
{
    public AstArgumentsNode()
    {
        PosonlyArgs = [];
        Args = [];
        KwonlyArgs = [];
        KwDefaults = [];
        Defaults = [];
    }

    public List<AstArgNode> PosonlyArgs { get; }
    public List<AstArgNode> Args { get; }
    public AstArgNode? VarArg { get; set; }
    public List<AstArgNode> KwonlyArgs { get; }
    public AstArgNode? KwArg { get; set; }
    public List<AstExprNode?> KwDefaults { get; }
    public List<AstExprNode> Defaults { get; }
}
