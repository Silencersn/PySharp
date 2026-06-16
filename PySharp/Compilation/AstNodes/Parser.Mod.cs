using PySharp.Compilation.Tokenization;

namespace PySharp.Compilation.AstNodes;

partial class Parser
{
    [GrammarSyntaxRule("file")]
    internal ModuleNode ParseFile()
    {
        if (CurrentTokenType is TokenType.Encoding)
            EnsureTokenTypeThenMove(TokenType.Encoding);

        var metaInfo = CreateAstMetaInfo();

        IEnumerable<AstStmtNode> body = CurrentTokenType is not TokenType.EndMarker ? ParseStatements() : [];


        EnsureTokenTypeThenMove(TokenType.EndMarker);

        return Ast.Module(body).With(metaInfo);
    }

    [GrammarSyntaxRule("eval")]
    internal ExpressionNode ParseEval()
    {
        if (CurrentTokenType is TokenType.Encoding)
            EnsureTokenTypeThenMove(TokenType.Encoding);

        var metaInfo = CreateAstMetaInfo();

        var exprs = ParseExpressions(StopPredicates.UntilNewLineOrEndMarker, out var endsWithComma);
        var body = UnwrapOrMakeTuple(exprs, endsWithComma);

        while (CurrentTokenType is TokenType.NewLine)
            MoveNextToken();
        EnsureTokenTypeThenMove(TokenType.EndMarker);

        return Ast.Expression(body).With(metaInfo);
    }

    [GrammarSyntaxRule("interactive")]
    internal InteractiveNode ParseInteractive()
    {
        if (CurrentTokenType is TokenType.Encoding)
            EnsureTokenTypeThenMove(TokenType.Encoding);

        var metaInfo = CreateAstMetaInfo();
        var body = ParseStatementNewLine();
        return Ast.Interactive(body).With(metaInfo);
    }
}
