using PySharp.Tokenization;

namespace PySharp.AstNodes;

partial class Parser
{
    private delegate bool StopPredicate(TokenInfo token);

    private static class StopPredicates
    {
        public static bool UntilKeywordIn(TokenInfo token) => token.Type is TokenType.Name && token.StringAsSpan is "in";
        public static bool UntilRightSquareBracket(TokenInfo token) => token.Type is TokenType.RightSquareBracket;
        public static bool UntilRightParen(TokenInfo token) => token.Type is TokenType.RightParen;
        public static bool UntilRightParenOrNewLineOrSemicolon(TokenInfo token) => token.Type is TokenType.RightParen or TokenType.NewLine or TokenType.Semicolon;
        public static bool UntilRightParenOrDoubleStar(TokenInfo token) => token.Type is TokenType.RightParen or TokenType.DoubleStar;
        public static bool UntilRightBrace(TokenInfo token) => token.Type is TokenType.RightBrace;
        public static bool UntilRightBraceOrDoubleStar(TokenInfo token) => token.Type is TokenType.RightBrace or TokenType.DoubleStar;
        public static bool UntilRightBraceOrEqual(TokenInfo token) => token.Type is TokenType.RightBrace or TokenType.Equal;
        public static bool UntilNewLine(TokenInfo token) => token.Type is TokenType.NewLine;
        public static bool UntilNewLineOrSemicolon(TokenInfo token) => token.Type is TokenType.NewLine or TokenType.Semicolon;
        public static bool UntilNewLineOrSemicolonOrEqual(TokenInfo token) => token.Type is TokenType.NewLine or TokenType.Semicolon or TokenType.Equal;
        public static bool UntilColon(TokenInfo token) => token.Type is TokenType.Colon;
        public static StopPredicate Until(TokenType tokenType) => tokenInfo => tokenInfo.Type == tokenType;
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    internal sealed class GrammarSyntaxRuleAttribute : Attribute
    {
        public GrammarSyntaxRuleAttribute(string ruleName)
        {
            RuleName = ruleName;
        }

        public string RuleName { get; }
    }
}
