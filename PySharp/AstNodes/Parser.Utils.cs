using PySharp.Tokenization;

namespace PySharp.AstNodes;

partial class Parser
{
    private delegate bool StopPredicate(TokenInfo token);

    private static class StopPredicates
    {
        public static bool UntilKeywordIn(TokenInfo token) => token.Type is TokenType.Name && token.String is "in";
        public static bool UntilRightSquareBracket(TokenInfo token) => token.Type is TokenType.RightSquareBracket;
        public static bool UntilRightParen(TokenInfo token) => token.Type is TokenType.RightParen;
        public static bool UntilRightParenOrNewLineOrSemicolon(TokenInfo token) => token.Type is TokenType.RightParen or TokenType.NewLine or TokenType.Semicolon;
        public static bool UntilRightBrace(TokenInfo token) => token.Type is TokenType.RightBrace;
        public static bool UntilRightBraceOrEqual(TokenInfo token) => token.Type is TokenType.RightBrace or TokenType.Equal;
        public static bool UntilNewLine(TokenInfo token) => token.Type is TokenType.NewLine;
        public static bool UntilNewLineOrSemicolon(TokenInfo token) => token.Type is TokenType.NewLine or TokenType.Semicolon;
        public static bool UntilNewLineOrSemicolonOrEqual(TokenInfo token) => token.Type is TokenType.NewLine or TokenType.Semicolon or TokenType.Equal;
        public static bool UntilColon(TokenInfo token) => token.Type is TokenType.Colon;
    }

    private static bool IsValidAugtarget(AstExprNode node)
    {
        //return node is NameNode or SubscriptNode or AttributeNode;
        return node is ITargetNode;
    }

    private static bool IsValidTarget(AstExprNode node)
    {
        if (IsValidAugtarget(node))
            return true;

        if (node is TupleNode tupleNode)
            return tupleNode.Elts.All(IsValidTarget);

        if (node is ListNode listNode)
            return listNode.Elts.All(IsValidTarget);

        return false;
    }

    private static void TrySetTargetContext(AstExprNode node, ExprContext context)
    {
        if (node is NameNode nameNode)
        {
            nameNode.Ctx = context;
        }
        else if (node is TupleNode tupleNode)
        {
            tupleNode.Ctx = context;
            foreach (var elt in tupleNode.Elts)
                TrySetTargetContext(elt, context);
        }
        else if (node is ListNode listNode)
        {
            listNode.Ctx = context;
            foreach (var elt in listNode.Elts)
                TrySetTargetContext(elt, context);
        }
    }
}
