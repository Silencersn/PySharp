namespace PySharp.AstNodes;

public static partial class AstNodeFactory
{
    public static AstComprehensionNode Comprehension(AstExprNode target, AstExprNode iter, params IEnumerable<AstExprNode> ifs)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(iter);
        ArgumentNullException.ThrowIfNull(ifs);

        if (target is not IExprContextNode node)
            throw new InvalidOperationException();

        node.Ctx = ExprContext.Store;

        return new AstComprehensionNode(target, iter, [.. ifs]);
    }

    public static AstKeywordNode Keyword(string arg, AstExprNode value)
    {
        ArgumentNullException.ThrowIfNull(arg);
        ArgumentNullException.ThrowIfNull(value);

        return new AstKeywordNode(arg, value);
    }
}
