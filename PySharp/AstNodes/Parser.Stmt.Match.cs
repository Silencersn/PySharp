using PySharp.PyModules.Builtins;
using PySharp.Tokenization;
using PySharp.Utility;

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
        var pos = TokenStreamPosition;

        if (CurrentTokenType is TokenType.Number or TokenType.String or TokenType.FStringStart ||
            CurrentTokenType is TokenType.Name && IsKeyword(CurrentToken.String))
            return ParseLiteralPattern();

        if (CurrentTokenType is TokenType.Name)
        {
            if (CurrentToken.String is "_")
                return ParseWildcardPattern();

            var nameOrAttr = ParseNameOrAttr();
            TokenStreamPosition = pos;

            if (CurrentTokenType is TokenType.LeftParen)
                return ParseClassPattern();

            if (nameOrAttr is NameNode)
                return ParseCapturePattern();

            return ParseValuePattern();
        }

        if (CurrentTokenType is TokenType.LeftParen)
            throw new NotImplementedException();

        if (CurrentTokenType is TokenType.LeftBrace)
            throw new NotImplementedException();

        throw _context.ThrowableSyntaxError("invalid syntax");
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

    [GrammarSyntaxRule("literal_pattern")]
    private AstPatternNode ParseLiteralPattern()
    {
        var expr = ParseLiteralExpr();
        if (expr is ConstantNode constantNode)
        {
            var obj = constantNode.Value;
            if (obj is PyBoolObject or PyNoneObject)
                return Ast.MatchSingleton(obj);
        }
        return Ast.MatchValue(expr);
    }

    [GrammarSyntaxRule("literal_expr")]
    private AstExprNode ParseLiteralExpr()
    {
        if (CurrentTokenType is TokenType.Number)
        {
            var pos = TokenStreamPosition;

            var signedNumber = ParseSignedNumber();
            if (CurrentTokenType is not (TokenType.Plus or TokenType.Minus))
                return signedNumber;

            TokenStreamPosition = pos;
            return ParseComplexNumber();
        }
        else if (CurrentTokenType is TokenType.String)
        {
            return ParseStrings();
        }
        else if (CurrentTokenType is TokenType.Name)
        {
            var metaInfo = CreateAstMetaInfo();
            var s = CurrentToken.String;
            return (s switch
            {
                "True" => Ast.Constant(PyBoolObject.True),
                "False" => Ast.Constant(PyBoolObject.False),
                "None" => Ast.Constant(PyNoneObject.None),
                _ => throw _context.ThrowableSyntaxError("invalid syntax")
            }).With(metaInfo);
        }

        throw _context.ThrowableSyntaxError("invalid syntax");
    }

    [GrammarSyntaxRule("complex_number")]
    private BinOpNode ParseComplexNumber()
    {
        var metaInfo = CreateAstMetaInfo();
        var real = ParseSignedRealNumber();
        if (CurrentTokenType is not (TokenType.Plus or TokenType.Minus))
            throw _context.ThrowableSyntaxError("invalid syntax");
        var sign = CurrentTokenType is TokenType.Plus;
        MoveNextToken();
        var imag = ParseImaginaryNumber();
        return (sign ? Ast.Add(real, imag) : Ast.Sub(real, imag)).With(metaInfo.WithPreviousEnd());
    }

    [GrammarSyntaxRule("signed_number")]
    private AstExprNode ParseSignedNumber()
    {
        if (CurrentTokenType is not TokenType.Minus)
            return ParseNumber();

        var metaInfo = CreateAstMetaInfo();
        MoveNextToken();
        var number = ParseNumber();
        return Ast.USub(number).With(metaInfo.WithPreviousEnd());
    }

    [GrammarSyntaxRule("signed_real_number")]
    private AstExprNode ParseSignedRealNumber()
    {
        if (CurrentTokenType is not TokenType.Minus)
            return ParseRealNumber();

        var metaInfo = CreateAstMetaInfo();
        MoveNextToken();
        var number = ParseRealNumber();
        return Ast.USub(number).With(metaInfo.WithPreviousEnd());
    }

    [GrammarSyntaxRule("real_number")]
    private ConstantNode ParseRealNumber()
    {
        var number = ParseNumber();
        if (number.Value is not (PyIntObject or PyFloatObject))
            throw _context.ThrowableSyntaxError("real number required in complex literal");
        return number;
    }

    [GrammarSyntaxRule("imaginary_number")]
    private ConstantNode ParseImaginaryNumber()
    {
        var number = ParseNumber();
        if (number.Value is not PyComplexObject)
            throw _context.ThrowableSyntaxError("imaginary number required in complex literal");
        return number;
    }

    [GrammarSyntaxRule("NUMBER")]
    private ConstantNode ParseNumber()
    {
        var metaInfo = CreateAstMetaInfo();
        EnsureTokenType(TokenType.Number);
        var value = CurrentToken.String;
        MoveNextToken();

        if (value.EndsWith('j'))
        {
            value = value.Replace("_", string.Empty);
            var imag = double.Parse(value.AsSpan()[..^1]);
            var complex = PyComplexObject.FromRealImag(0, imag);
            return Ast.Constant(complex).With(metaInfo);
        }

        if (BigIntegerHelper.TryParse(value, 0, out var integer))
            return Ast.Constant(integer).With(metaInfo);

        value = value.Replace("_", string.Empty);
        return Ast.Constant(double.Parse(value)).With(metaInfo);
    }

    [GrammarSyntaxRule("strings")]
    private AstExprNode ParseStrings()
    {
        return ParseString();
    }

    [GrammarSyntaxRule("capture_pattern")]
    private MatchAsNode ParseCapturePattern()
    {
        var metaInfo = CreateAstMetaInfo();
        var name = ParsePatternCaptureTarget();
        return Ast.MatchAs(pattern: null, name).With(metaInfo);
    }

    [GrammarSyntaxRule("value_pattern")]
    private MatchValueNode ParseValuePattern()
    {
        var attr = ParseAttr();
        if (CurrentTokenType is TokenType.Dot or TokenType.LeftParen or TokenType.Equal)
            throw _context.ThrowableSyntaxError("invalid syntax");
        return Ast.MatchValue(attr);
    }

    [GrammarSyntaxRule("attr")]
    private AttributeNode ParseAttr()
    {
        var metaInfo = CreateAstMetaInfo();
        var nameOrAttr = ParseNameOrAttr();
        EnsureTokenTypeThenMove(TokenType.Dot);
        var name = ParseIdentifier();
        return Ast.Attribute(nameOrAttr, name).With(metaInfo);
    }

    [GrammarSyntaxRule("name_or_attr")]
    private AstExprNode ParseNameOrAttr()
    {
        var pos = TokenStreamPosition;
        var metaInfo = CreateAstMetaInfo();
        var name = ParseIdentifier();
        if (CurrentTokenType is not TokenType.Dot)
            return Ast.Name(name).With(metaInfo);

        TokenStreamPosition = pos;
        return ParseAttr();
    }

    [GrammarSyntaxRule("class_pattern")]
    private MatchClassNode ParseClassPattern()
    {
        throw new NotImplementedException();
    }
}
