namespace PySharp.Compilation.AstNodes;


partial class Ast
{
    public static ModuleNode Module(IEnumerable<AstStmtNode> body)
    {
        ArgumentNullException.ThrowIfNull(body);

        return new ModuleNode(body.ToImmutableArray(true));
    }

    public static ExpressionNode Expression(AstExprNode body)
    {
        ArgumentNullException.ThrowIfNull(body);

        return new ExpressionNode(body);
    }

    public static InteractiveNode Interactive(IEnumerable<AstStmtNode> body)
    {
        ArgumentNullException.ThrowIfNull(body);

        return new InteractiveNode(body.ToImmutableArray(true));
    }
}
