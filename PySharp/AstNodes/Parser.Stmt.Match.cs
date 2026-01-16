using PySharp.Tokenization;
using System.Diagnostics;

namespace PySharp.AstNodes;

partial class Parser
{
    [GrammarSyntaxRule("match_stmt")]
    private MatchNode ParseMatchStmt()
    {
        var metaInfo = CreateAstMetaInfo();
        var lineno = CurrentToken.Start.Line;
        EnsureKeywordThenMove("match");

        var subject = ParseSubjectExpr();

        EnsureTokenTypeThenMove(TokenType.Colon);
        EnsureTokenTypeThenMove(TokenType.NewLine);
        EnsureTokenTypeThenMove(TokenType.Indent, $"expected an indented block after 'match' on line {lineno}");

        List<AstMatchCaseNode> cases = [ParseCaseBlock()];
        while (IsCurrentKeyword("case"))
            cases.Add(ParseCaseBlock());

        EnsureTokenTypeThenMove(TokenType.Dedent);

        return Ast.Match(subject, cases).With(metaInfo);
    }

    [GrammarSyntaxRule("subject_expr")]
    private AstExprNode ParseSubjectExpr()
    {
        var list = ParseStarNamedExpressions(out var endsWithComma);
        var expr = UnwrapOrMakeTuple(list, endsWithComma);
        if (expr is StarredNode)
            throw _context.ThrowableSyntaxError("can't use starred expression here");
        return expr;
    }

    [GrammarSyntaxRule("case_block")]
    private AstMatchCaseNode ParseCaseBlock()
    {
        var metaInfo = CreateAstMetaInfo();
        EnsureKeywordThenMove("case");
        var patterns = ParsePatterns();
        var guard = CurrentTokenType is not TokenType.Colon ? ParseGuard() : null;
        EnsureTokenTypeThenMove(TokenType.Colon);
        var body = ParseBlock("case");
        return Ast.MatchCase(patterns, guard, body).With(metaInfo);
    }

    [GrammarSyntaxRule("named_expression")]
    private AstExprNode ParseNamedExpression()
    {
        return ParseAssignmentExpression();
    }

    [GrammarSyntaxRule("star_named_expression")]
    private AstExprNode ParseStarNamedExpression()
    {
        if (CurrentTokenType is TokenType.Star)
            return ParseStarredExpression();

        return ParseNamedExpression();
    }

    [GrammarSyntaxRule("star_named_expressions")]
    private List<AstExprNode> ParseStarNamedExpressions(out TokenInfo? endsWithComma)
    {
        return ParseSomethingList(ParseStarNamedExpression, StopPredicates.UntilColon, out endsWithComma);
    }

    [GrammarSyntaxRule("guard")]
    private AstExprNode ParseGuard()
    {
        throw new NotImplementedException();
    }

    [GrammarSyntaxRule("patterns")]
    private AstPatternNode ParsePatterns()
    {
        throw new NotImplementedException();
    }

    [GrammarSyntaxRule("block")]
    private List<AstStmtNode> ParseBlock(string keyword)
    {
        return ParseSuite(keyword);
    }
}
