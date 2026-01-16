using PySharp.CodeAnalysis;
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
        var guard = IsCurrentKeyword("if") ? ParseGuard() : null;
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
        EnsureKeywordThenMove("if");
        return ParseNamedExpression();
    }

    [GrammarSyntaxRule("block")]
    private List<AstStmtNode> ParseBlock(string keyword)
    {
        return ParseSuite(keyword);
    }

    [GrammarSyntaxRule("patterns")]
    private AstPatternNode ParsePatterns()
    {
        var list = ParseOpenSequencePattern(out var endsWithComma);
        var pattern = UnwrapOrMakeSomething(list, endsWithComma, Ast.MatchSequence);
        if (pattern is MatchStarNode)
            throw _context.ThrowableSyntaxError("invalid syntax");
        return pattern;
    }

    [GrammarSyntaxRule("open_sequence_pattern")]
    private List<AstPatternNode> ParseOpenSequencePattern(out TokenInfo? endsWithComma)
    {
        return ParseSomethingList(ParseMaybeStarPattern, StopPredicates.UntilColon, out endsWithComma);
    }

    [GrammarSyntaxRule("pattern")]
    private AstPatternNode ParsePattern()
    {
        var pos = TokenStreamPosition;
        _ = ParseOrPattern();
        var isAsPattern = IsCurrentKeyword("as");
        TokenStreamPosition = pos;

        if (isAsPattern)
            return ParseAsPattern();

        return ParseOrPattern();
    }

    [GrammarSyntaxRule("star_pattern")]
    private MatchStarNode ParseStarPattern()
    {
        EnsureTokenTypeThenMove(TokenType.Star);
        var name = IsCurrentKeyword("_") ? null : ParsePatternCaptureTarget();
        return Ast.MatchStar(name);
    }

    [GrammarSyntaxRule("maybe_star_pattern")]
    private AstPatternNode ParseMaybeStarPattern()
    {
        if (CurrentTokenType is TokenType.Star)
            return ParseStarPattern();

        return ParsePattern();
    }

    [GrammarSyntaxRule("closed_pattern")]
    private AstPatternNode ParseClosedPattern()
    {
        throw new NotImplementedException();
    }

    [GrammarSyntaxRule("or_pattern")]
    private AstPatternNode ParseOrPattern()
    {
        var list = ParseSomethingList(ParseClosedPattern, StopPredicates.UntilColon, out var endsWithComma, TokenType.Pipe);
        return UnwrapOrMakeSomething(list, endsWithComma, Ast.MatchOr);
    }

    [GrammarSyntaxRule("as_pattern")]
    private MatchAsNode ParseAsPattern()
    {
        var pattern = ParseOrPattern();
        EnsureKeywordThenMove("as");
        var name = ParsePatternCaptureTarget();
        return Ast.MatchAs(pattern, name);
    }

    [GrammarSyntaxRule("pattern_capture_target")]
    private string ParsePatternCaptureTarget()
    {
        var target = ParsePrimary();
        if (target is not NameNode nameNode)
            throw _context.ThrowableSyntaxError($"cannot use {AstUtils.GetExprNodeName(target)} as pattern target");

        var name = nameNode.Id;
        if (name is "_")
            throw _context.ThrowableSyntaxError($"cannot use '_' as a target");

        return name;
    }

    [GrammarSyntaxRule("wildcard_pattern")]
    private MatchAsNode ParseWildcardPattern()
    {
        EnsureKeywordThenMove("_");
        return Ast.MatchAs(pattern: null, name: null);
    }
}
