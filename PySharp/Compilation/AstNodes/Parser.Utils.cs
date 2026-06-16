using PySharp.Compilation.Tokenization;

namespace PySharp.Compilation.AstNodes;

partial class Parser
{
    private delegate bool StopPredicate(Token token);

    private static class StopPredicates
    {
        public static StopPredicate UntilKeywordIn(Parser parser) => _ => parser.CurrentTokenType is TokenType.Name && parser.CurrentTokenStringAsSpan is "in";
        public static bool UntilRightSquareBracket(Token token) => token.Type is TokenType.RightSquareBracket;
        public static bool UntilRightParen(Token token) => token.Type is TokenType.RightParen;
        public static bool UntilRightParenOrNewLineOrSemicolon(Token token) => token.Type is TokenType.RightParen or TokenType.NewLine or TokenType.Semicolon;
        public static bool UntilRightParenOrDoubleStar(Token token) => token.Type is TokenType.RightParen or TokenType.DoubleStar;
        public static bool UntilRightBrace(Token token) => token.Type is TokenType.RightBrace;
        public static bool UntilRightBraceOrDoubleStar(Token token) => token.Type is TokenType.RightBrace or TokenType.DoubleStar;
        public static bool UntilRightBraceOrEqualOrExclamationOrColon(Token token) => token.Type is TokenType.RightBrace or TokenType.Equal or TokenType.Exclamation or TokenType.Colon;
        public static bool UntilNewLine(Token token) => token.Type is TokenType.NewLine;
        public static bool UntilNewLineOrEndMarker(Token token) => token.Type is TokenType.NewLine or TokenType.EndMarker;
        public static bool UntilNewLineOrSemicolon(Token token) => token.Type is TokenType.NewLine or TokenType.Semicolon;
        public static bool UntilNewLineOrSemicolonOrRightParen(Token token) => token.Type is TokenType.NewLine or TokenType.Semicolon or TokenType.RightParen;
        public static bool UntilNewLineOrSemicolonOrEqual(Token token) => token.Type is TokenType.NewLine or TokenType.Semicolon or TokenType.Equal;
        public static bool UntilColon(Token token) => token.Type is TokenType.Colon;
        public static bool UntilNonName(Token token) => token.Type is not TokenType.Name;
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
