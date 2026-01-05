namespace PySharp.AstNodes;


public static partial class AstNodeFactory
{
    public static AstComprehensionNode Comprehension(AstExprNode target, AstExprNode iter, IEnumerable<AstExprNode> ifs)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(iter);
        ArgumentNullException.ThrowIfNull(ifs);

        target.CheckValidTargetThenSetContext(ExprContextType.Store);

        return new AstComprehensionNode(target, iter, ifs.ToImmutableArray(true));
    }

    public static AstKeywordNode Keyword(string arg, AstExprNode value)
    {
        ArgumentNullException.ThrowIfNull(arg);
        ArgumentNullException.ThrowIfNull(value);

        return new AstKeywordNode(arg, value);
    }
}
