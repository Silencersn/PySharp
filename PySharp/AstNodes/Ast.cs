using System.Collections.Immutable;

namespace PySharp.AstNodes;


public static partial class Ast
{
    public static AstComprehensionNode Comprehension(AstExprNode target, AstExprNode iter, IEnumerable<AstExprNode> ifs)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(iter);
        ArgumentNullException.ThrowIfNull(ifs);

        target.CheckValidTargetThenSetContext(ExprContextType.Store);

        return new AstComprehensionNode(target, iter, ifs.ToImmutableArray(true));
    }

    public static AstKeywordNode Keyword(string? arg, AstExprNode value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return new AstKeywordNode(arg, value);
    }

    public static ExceptHandlerNode ExceptHandler(AstExprNode? type, string? name, IEnumerable<AstStmtNode> body)
    {
        return new ExceptHandlerNode(type, name, body.ToImmutableArray(true));
    }
}
