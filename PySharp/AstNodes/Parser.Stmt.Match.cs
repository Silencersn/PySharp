using PySharp.PyModules.Builtins;
using PySharp.Resources;
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
        EnsureTokenTypeThenMove(TokenType.Indent, PySR.Format(PySR.InvalidSyntax_Indentation_ExpectedForBlock, "'match'", lineno));

        List<AstMatchCaseNode> cases = [ParseCaseBlock()];
        while (IsCurrentKeyword("case"))
            cases.Add(ParseCaseBlock());

        EnsureTokenTypeThenMove(TokenType.Dedent);

        return Ast.Match(subject, cases).With(metaInfo);
    }

    [GrammarSyntaxRule("subject_expr")]
    private AstExprNode ParseSubjectExpr()
    {
        var list = ParseStarNamedExpressions(StopPredicates.UntilColon, out var endsWithComma);
        var expr = UnwrapOrMakeTuple(list, endsWithComma);
        if (expr is StarredNode)
            throw SyntaxError(PySR.InvalidSyntax_StarredExpression_CannotUseHere);
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

    [GrammarSyntaxRule("guard")]
    private AstExprNode ParseGuard()
    {
        EnsureKeywordThenMove("if");
        return ParseNamedExpression();
    }

    [GrammarSyntaxRule("patterns")]
    private AstPatternNode ParsePatterns()
    {
        if (CurrentTokenType is TokenType.Star)
            return ParseOpenSequencePattern(StopPredicates.UntilColon);

        var pos = TokenStreamPosition;
        var pattern = ParsePattern();
        if (CurrentTokenType is not TokenType.Comma)
            return pattern;

        TokenStreamPosition = pos;
        return ParseOpenSequencePattern(StopPredicates.UntilColon);
    }

    [GrammarSyntaxRule("open_sequence_pattern")]
    private MatchSequenceNode ParseOpenSequencePattern(StopPredicate predicate)
    {
        var pattern = ParseMaybeStarPattern();
        var endsWithComma = CurrentToken;
        EnsureTokenTypeThenMove(TokenType.Comma);
        if (predicate(CurrentToken))
            return PackSomething([pattern], endsWithComma, Ast.MatchSequence);

        var list = ParseMaybeSequencePattern(predicate, out endsWithComma);
        return PackSomething([pattern, .. list], endsWithComma, Ast.MatchSequence);
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
            var isClass = CurrentTokenType is TokenType.LeftParen;
            TokenStreamPosition = pos;

            if (isClass)
                return ParseClassPattern();

            if (nameOrAttr is NameNode)
                return ParseCapturePattern();

            return ParseValuePattern();
        }

        if (CurrentTokenType is TokenType.LeftParen)
        {
            MoveNextToken();

            if (CurrentTokenType is TokenType.Star or TokenType.RightParen)
            {
                TokenStreamPosition = pos;
                return ParseSequencePattern();
            }

            var pattern = ParsePattern();
            if (CurrentTokenType is TokenType.RightParen)
            {
                MoveNextToken();
                return pattern;
            }

            TokenStreamPosition = pos;
            return ParseSequencePattern();
        }

        if (CurrentTokenType is TokenType.LeftSquareBracket)
            return ParseSequencePattern();

        if (CurrentTokenType is TokenType.LeftBrace)
            return ParseMappingPattern();

        throw SyntaxError();
    }

    [GrammarSyntaxRule("or_pattern")]
    private AstPatternNode ParseOrPattern()
    {
        var list = ParseSomethingList(ParseClosedPattern, StopPredicates.UntilColon, out var endsWithComma, TokenType.Pipe);
        return UnwrapOrPackSomething(list, endsWithComma, Ast.MatchOr);
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
            throw SyntaxError(PySR.InvalidSyntax_Pattern_InvalidPatternTarget, AstUtils.GetExprNodeName(target));

        var name = nameNode.Id;
        if (name is "_")
            throw SyntaxError(PySR.InvalidSyntax_Pattern_UnderscoreAsTarget);

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
                _ => throw SyntaxError()
            }).With(metaInfo);
        }

        throw SyntaxError();
    }

    [GrammarSyntaxRule("complex_number")]
    private BinOpNode ParseComplexNumber()
    {
        var metaInfo = CreateAstMetaInfo();
        var real = ParseSignedRealNumber();
        if (CurrentTokenType is not (TokenType.Plus or TokenType.Minus))
            throw SyntaxError();
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
            throw SyntaxError(PySR.InvalidSyntax_Pattern_RealNumberRequired);
        return number;
    }

    [GrammarSyntaxRule("imaginary_number")]
    private ConstantNode ParseImaginaryNumber()
    {
        var number = ParseNumber();
        if (number.Value is not PyComplexObject)
            throw SyntaxError(PySR.InvalidSyntax_Pattern_ImaginaryNumberRequired);
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
            throw SyntaxError();
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

    [GrammarSyntaxRule("maybe_sequence_pattern")]
    private List<AstPatternNode> ParseMaybeSequencePattern(StopPredicate predicate, out TokenInfo? endsWithComma)
    {
        return ParseSomethingList(ParseMaybeStarPattern, predicate, out endsWithComma);
    }

    [GrammarSyntaxRule("sequence_pattern")]
    private MatchSequenceNode ParseSequencePattern()
    {
        var metaInfo = CreateAstMetaInfo();
        if (CurrentTokenType is TokenType.LeftSquareBracket)
        {
            MoveNextToken();
            if (CurrentTokenType is TokenType.RightSquareBracket)
            {
                var pattern = Ast.MatchSequence([]);
                MoveNextToken();
                return pattern.With(metaInfo.WithPreviousEnd());
            }
            else
            {
                var list = ParseMaybeSequencePattern(StopPredicates.UntilRightSquareBracket, out var endsWithComma);
                var pattern = PackSomething(list, endsWithComma, Ast.MatchSequence);
                EnsureTokenTypeThenMove(TokenType.RightSquareBracket);
                return pattern.With(metaInfo.WithPreviousEnd());
            }
        }
        else if (CurrentTokenType is TokenType.LeftParen)
        {
            MoveNextToken();
            if (CurrentTokenType is TokenType.RightParen)
            {
                var pattern = Ast.MatchSequence([]);
                MoveNextToken();
                return pattern.With(metaInfo.WithPreviousEnd());
            }
            else
            {
                var pattern = ParseOpenSequencePattern(StopPredicates.UntilRightParen);
                EnsureTokenTypeThenMove(TokenType.RightParen);
                return pattern.With(metaInfo.WithPreviousEnd());
            }
        }

        throw SyntaxError();
    }

    [GrammarSyntaxRule("mapping_pattern")]
    private MatchMappingNode ParseMappingPattern()
    {
        var metaInfo = CreateAstMetaInfo();
        EnsureTokenTypeThenMove(TokenType.LeftBrace);

        if (CurrentTokenType is TokenType.RightBrace)
        {
            var pattern = Ast.MatchMapping(keys: [], patterns: [], rest: null);
            MoveNextToken();
            return pattern.With(metaInfo.WithPreviousEnd());
        }

        if (CurrentTokenType is TokenType.DoubleStar)
        {
            var rest = ParseDoubleStarPattern();
            var pattern = Ast.MatchMapping(keys: [], patterns: [], rest);
            EnsureTokenTypeThenMove(TokenType.RightBrace);
            return pattern.With(metaInfo.WithPreviousEnd());
        }

        var items = ParseItemsPattern(out var endsWithComma);
        if (CurrentTokenType is TokenType.DoubleStar)
        {
            if (endsWithComma is null)
                throw SyntaxError();

            var rest = ParseDoubleStarPattern();
            var pattern = Ast.MatchMapping(items.Select(static item => item.Key), items.Select(static item => item.Value), rest);
            EnsureTokenTypeThenMove(TokenType.RightBrace);
            return pattern.With(metaInfo.WithPreviousEnd());
        }
        else
        {
            var pattern = Ast.MatchMapping(items.Select(static item => item.Key), items.Select(static item => item.Value), rest: null);
            EnsureTokenTypeThenMove(TokenType.RightBrace);
            return pattern.With(metaInfo.WithPreviousEnd());
        }
    }

    [GrammarSyntaxRule("key_value_pattern")]
    private KeyValuePair<AstExprNode, AstPatternNode> ParseKeyValuePattern()
    {
        var key = IsCurrentIdentifier ? ParseAttr() : ParseLiteralExpr();
        EnsureTokenTypeThenMove(TokenType.Colon);
        var value = ParsePattern();
        return KeyValuePair.Create(key, value);
    }

    [GrammarSyntaxRule("items_pattern")]
    private List<KeyValuePair<AstExprNode, AstPatternNode>> ParseItemsPattern(out TokenInfo? endsWithComma)
    {
        return ParseSomethingList(ParseKeyValuePattern, StopPredicates.UntilRightBraceOrDoubleStar, out endsWithComma);
    }

    [GrammarSyntaxRule("double_star_pattern")]
    private string ParseDoubleStarPattern()
    {
        EnsureTokenTypeThenMove(TokenType.DoubleStar);
        return ParsePatternCaptureTarget();
    }

    [GrammarSyntaxRule("class_pattern")]
    private MatchClassNode ParseClassPattern()
    {
        var metaInfo = CreateAstMetaInfo();
        var cls = ParseNameOrAttr();
        EnsureTokenTypeThenMove(TokenType.LeftParen);

        if (CurrentTokenType is TokenType.RightParen)
        {
            var pattern = Ast.MatchClass(cls, patterns: [], kwdAttrs: [], kwdPatterns: []);
            MoveNextToken();
            return pattern.With(metaInfo.WithPreviousEnd());
        }

        if (TestIsKeywordPattern())
        {
            var kwds = ParseKeywordPatterns();
            var pattern = Ast.MatchClass(cls, patterns: [], kwds.Select(static kwd => kwd.Key), kwds.Select(static kwd => kwd.Value));
            EnsureTokenTypeThenMove(TokenType.RightParen);
            return pattern.With(metaInfo.WithPreviousEnd());
        }

        var patterns = ParsePositionalPatterns();

        if (CurrentTokenType is TokenType.RightParen)
        {
            var pattern = Ast.MatchClass(cls, patterns, kwdAttrs: [], kwdPatterns: []);
            EnsureTokenTypeThenMove(TokenType.RightParen);
            return pattern.With(metaInfo.WithPreviousEnd());
        }
        else
        {
            var kwds = ParseKeywordPatterns();
            var pattern = Ast.MatchClass(cls, patterns, kwds.Select(static kwd => kwd.Key), kwds.Select(static kwd => kwd.Value));
            EnsureTokenTypeThenMove(TokenType.RightParen);
            return pattern.With(metaInfo.WithPreviousEnd());
        }
    }

    [GrammarSyntaxRule("positional_patterns")]
    private List<AstPatternNode> ParsePositionalPatterns()
    {
        return ParseSomethingList(ParsePattern, _ => CurrentTokenType is TokenType.RightParen || TestIsKeywordPattern(), out _);
    }

    private bool TestIsKeywordPattern()
    {
        if (!IsCurrentIdentifier)
            return false;

        var pos = TokenStreamPosition;
        MoveNextToken();
        var result = CurrentTokenType is TokenType.Equal;
        TokenStreamPosition = pos;
        return result;
    }

    [GrammarSyntaxRule("keyword_pattern")]
    private KeyValuePair<string, AstPatternNode> ParseKeywordPattern()
    {
        var name = ParseIdentifier();
        EnsureTokenTypeThenMove(TokenType.Equal);
        var pattern = ParsePattern();
        return KeyValuePair.Create(name, pattern);
    }

    [GrammarSyntaxRule("keyword_patterns")]
    private List<KeyValuePair<string, AstPatternNode>> ParseKeywordPatterns()
    {
        return ParseSomethingList(ParseKeywordPattern, StopPredicates.UntilRightParen, out _);
    }
}
