using PySharp.CodeAnalysis;
using PySharp.PyModules.Builtins;
using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;
using PySharp.Tokenization;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Text;

namespace PySharp.AstNodes;

partial class Parser
{
    /// <summary>
    /// only for token string, being never used by multiple methods at hre same time
    /// </summary>
    private readonly StringBuilder _builderForTokenString = new();

    /// <summary>
    /// identifier: &lt;NAME, except keywords&gt;
    /// </summary>
    /// <returns></returns>
    /// <exception cref="PyRuntimeException"></exception>
    private string ParseIdentifier()
    {
        EnsureTokenType(TokenType.Name);
        if (IsKeyword(CurrentToken.String))
            throw _context.ThrowableSyntaxError("invalid syntax");
        var ret = CurrentToken.String;
        MoveNextToken();
        return ret;
    }

    /// <summary>
    /// enclosure: <see cref="ParseParenthForm">parenth_form</see> | <see cref="ParseListDisplay">list_display</see> |
    ///            <see cref="ParseDictDisplay">dict_display</see> | <see cref="ParseSetDisplay">set_display</see> |
    ///            <see cref="ParseGeneratorExpression">generator_expression</see> | <see cref="ParseYieldAtom">yield_atom</see>
    /// </summary>
    /// <returns></returns>
    private AstExprNode ParseEnclosure()
    {
        if (CurrentTokenType is TokenType.LeftSquareBracket)
        {
            return ParseListDisplay();
        }
        else if (CurrentTokenType is TokenType.LeftParen)
        {
            var index = TokenStreamPosition;
            MoveNextToken();

            if (IsCurrentKeyword("yield"))
            {
                var expr = ParseYieldExpr();
                EnsureTokenTypeThenMove(TokenType.RightParen);
                return expr;
            }

            // () is an empty tuple
            if (CurrentTokenType is TokenType.RightParen)
            {
                TokenStreamPosition = index;
                return ParseParenthForm();
            }

            // generator_expression
            if (TestIsComprehension())
            {
                TokenStreamPosition = index;
                return ParseGeneratorExpression();
            }

            TokenStreamPosition = index;
            return ParseParenthForm();
        }
        else if (CurrentTokenType is TokenType.LeftBrace)
        {
            var index = TokenStreamPosition;

            bool isDict;

            MoveNextToken();
            if (CurrentTokenType is TokenType.RightBrace)
            {
                // {} is an empty dict instead of an empty set

                isDict = true;
            }
            else
            {
                _ = ParseExpression();
                isDict = CurrentTokenType is TokenType.Colon;
            }

            TokenStreamPosition = index;

            if (isDict)
                return ParseDictDisplay();

            return ParseSetDisplay();
        }

        throw _context.ThrowableSyntaxError("invalid syntax");
    }

    private AstExprNode ParseFExpression()
    {
        if (IsCurrentKeyword("yield"))
            return ParseYieldExpr();

        var list = ParseFlexibleExpressionList(StopPredicates.UntilRightBraceOrEqual, out var endsWithComma);
        return UnwrapOrMakeTuple(list, endsWithComma);
    }

    private JoinedStrNode ParseFStringFullFormatSpec()
    {
        EnsureTokenTypeThenMove(TokenType.Colon);
        List<AstExprNode> nodes = [];
        while (CurrentTokenType is not TokenType.RightBrace)
        {
            if (CurrentTokenType is TokenType.FStringMiddle)
            {
                var str = FromLiteralToString(_context, CurrentToken.StringAsSpan, true);
                var node = Ast.Constant(str).With(CreateAstMetaInfo());
                nodes.Add(node);
            }
            else
            {
                EnsureTokenTypeThenMove(TokenType.LeftBrace);
                var node = ParseFStringReplacementFieldWithoutBraces(out var debugSpecifier);
                if (debugSpecifier is not null)
                    // TODO: it seems that cpython do not support this here
                    nodes.Add(debugSpecifier);
                nodes.Add(node);
            }
            Debug.Assert(CurrentTokenType is TokenType.FStringMiddle or TokenType.RightBrace);
            MoveNextToken();
        }
        return Ast.JoinedStr(nodes); // TODO: need MetaInfo?
    }

    private FormattedValueNode ParseFStringReplacementFieldWithoutBraces(out ConstantNode? debugSpecifier)
    {
        if (CurrentTokenType is TokenType.RightBrace)
            throw _context.ThrowableSyntaxError("f-string: valid expression required before '}'");

        var start = CurrentToken.Start;
        var metaInfo = CreateAstMetaInfo();
        var fexpr = ParseFExpression();

        if (CurrentTokenType is TokenType.Equal)
        {
            MoveNextToken();
            var end = CurrentToken.Start;

            if (!_codeSource.Code.TryGetRange(start, end, out var range))
                throw _context.ThrowablePySharpException("incorrect code text position");

            debugSpecifier = Ast.Constant(range.ToString()).With(metaInfo.WithEnd());
        }
        else
        {
            debugSpecifier = null;
        }

        int conversion = -1;
        if (CurrentTokenType is TokenType.Exclamation)
        {
            MoveNextToken();

            if (CurrentTokenType is not TokenType.Name)
                throw _context.ThrowableSyntaxError("f-string: missing conversion character");

            if (CurrentToken.StringAsSpan is not ("s" or "r" or "a"))
                throw _context.ThrowableSyntaxError($"f-string: invalid conversion character '{CurrentToken.StringAsSpan}': expected 's', 'r', or 'a'");

            conversion = CurrentToken.StringAsSpan[0];
            MoveNextToken();
        }

        JoinedStrNode? format_spec = null;
        if (CurrentTokenType is TokenType.Colon)
            format_spec = ParseFStringFullFormatSpec();

        return Ast.FormattedValue(fexpr, conversion, format_spec).With(fexpr.MetaInfo); // TODO: MetaInfo
    }

    private AstExprNode ParseString()
    {
        Debug.Assert(CurrentTokenType is TokenType.String or TokenType.FStringStart);

        // ConstantNode or FormattedValueNode
        List<AstExprNode> nodes = [];
        bool hasFString = false;
        var metaInfo = CreateAstMetaInfo();

        while (CurrentTokenType is TokenType.String or TokenType.FStringStart)
        {
            if (CurrentTokenType is TokenType.String)
            {
                var str = FromLiteralToString(_context, CurrentToken.StringAsSpan, false);
                var node = Ast.Constant(str).With(CreateAstMetaInfo());
                nodes.Add(node);
            }
            else
            {
                EnsureTokenTypeThenMove(TokenType.FStringStart);
                hasFString = true;

                while (CurrentTokenType is not TokenType.FStringEnd)
                {
                    if (CurrentTokenType is TokenType.FStringMiddle)
                    {
                        var str = FromLiteralToString(_context, CurrentToken.StringAsSpan, true);
                        var node = Ast.Constant(str).With(CreateAstMetaInfo());
                        nodes.Add(node);
                    }
                    else
                    {
                        EnsureTokenTypeThenMove(TokenType.LeftBrace);
                        var node = ParseFStringReplacementFieldWithoutBraces(out var debugSpecifier);
                        if (debugSpecifier is not null)
                            nodes.Add(debugSpecifier);
                        nodes.Add(node);
                    }
                    MoveNextToken();
                }

            }

            metaInfo = metaInfo.WithEnd();
            Debug.Assert(CurrentTokenType is TokenType.String or TokenType.FStringEnd);
            MoveNextToken();
        }

        List<AstExprNode> combinedNodes = [];
        _builderForTokenString.Clear();
        foreach (var node in nodes)
        {
            if (node is ConstantNode constantNode)
            {
                Debug.Assert(constantNode.Value is PyStrObject);
                _builderForTokenString.Append(((PyStrObject)constantNode.Value).Value);
            }
            else if (node is FormattedValueNode formattedValueNode)
            {
                TryAppendCombinedConstantNode();
                combinedNodes.Add(formattedValueNode);
            }
            else
            {
                throw new UnreachableException();
            }
        }
        TryAppendCombinedConstantNode();

        if (!hasFString)
        {
            Debug.Assert(combinedNodes.Count is 0 or 1);

            if (combinedNodes.Count is 0)
                return Ast.Constant(string.Empty).With(metaInfo);

            var node = combinedNodes[0];
            node.MetaInfo = metaInfo;
            return node;
        }

        return Ast.JoinedStr(combinedNodes).With(metaInfo);

        void TryAppendCombinedConstantNode()
        {
            if (_builderForTokenString.Length is 0)
                return;

            var combinedNode = Ast.Constant(_builderForTokenString.ToString()); // MetaInfo will be added after the combining is complete
            combinedNodes.Add(combinedNode);
            _builderForTokenString.Clear();
        }


    }
    static string FromLiteralToString(PyCallContext context, ReadOnlySpan<char> literal, bool nonWrapper)
    {
        // TODO: prefix 'b'

        bool successful;
        string? str;
        PyStrConverter.ConvertErrorInfo info;
        if (nonWrapper)
            successful = PyStrConverter.TryFromTextToString(literal, out str, out info);
        else
            successful = PyStrConverter.TryFromLiteralToString(literal, out str, out info);

        if (successful)
        {
            if (info.Error is PyStrConverter.ConvertError.InvalidEscapeSequence)
            {
                if (!context.TryWarn<PySyntaxWarningObjectType>($"invalid escape sequence '\\{info.Char}'"))
                    throw new NotImplementedException();
            }

            Debug.Assert(str is not null);
            return str;
        }
        else
        {
            // correctness is ensured by the lexer
            Debug.Assert(info.Error is not (
                PyStrConverter.ConvertError.EndsWithEscape or
                PyStrConverter.ConvertError.DestinationNotEnough or
                PyStrConverter.ConvertError.WrongFormat or
                PyStrConverter.ConvertError.InvalidEscapeSequence));

            throw info.Error switch
            {
                PyStrConverter.ConvertError.LowerXSequence => context.ThrowableSyntaxError(MakeUnicodeErrorInfo("truncated \\xXX escape")),
                PyStrConverter.ConvertError.LowerUSequence => context.ThrowableSyntaxError(MakeUnicodeErrorInfo("truncated \\uXXXX escape")),
                PyStrConverter.ConvertError.UpperUSequence => context.ThrowableSyntaxError(MakeUnicodeErrorInfo("truncated \\UXXXXXXXX escape")),
                PyStrConverter.ConvertError.SurrogatesNotAllowed => context.ThrowableSyntaxError($"'utf-8' codec can't encode character '\\u{(uint)info.Char:x4}' in position {info.Position}: surrogates not allowed"),
                PyStrConverter.ConvertError.IllegalUnicodeCharacter => context.ThrowableSyntaxError(MakeUnicodeErrorInfo("illegal Unicode character")),
                _ => new UnreachableException(),
            };
            string MakeUnicodeErrorInfo(string message)
            {
                return $"(unicode error) 'unicodeescape' codec can't decode bytes in position {info.Position}-{info.Position + info.Length - 1}: {message}";
            }
        }

    }

    /// <summary>
    /// atom: <see cref="ParseIdentifier">identifier</see> | literal | <see cref="ParseEnclosure">enclosure</see>
    /// </summary>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    /// <exception cref="PyRuntimeException"></exception>
    /// <exception cref="NotSupportedException"></exception>
    private AstExprNode ParseAtom()
    {
        var metaInfo = CreateAstMetaInfo();
        if (CurrentTokenType is TokenType.Name)
        {
            if (IsKeyword(CurrentToken.String))
            {
                // keyword literal

                if (CurrentToken.String is "True" or "False")
                {
                    var value = CurrentToken.String;
                    MoveNextToken();
                    return Ast.Constant(bool.Parse(value)).With(metaInfo);
                }
                else if (CurrentToken.String is "None")
                {
                    MoveNextToken();
                    return Ast.Constant(PyNoneObject.None).With(metaInfo);
                }

                throw _context.ThrowableSyntaxError("invalid syntax");
            }
            else if (CurrentToken.String is PySpecialNames.Debug)
            {
                // __debug__

                MoveNextToken();
                return Ast.Constant(_options.Debug).With(metaInfo);
            }
            else
            {
                // identifier

                var nameNode = Ast.Name(ParseIdentifier()).With(metaInfo);
                return nameNode;
            }
        }
        else if (CurrentTokenType is TokenType.String or TokenType.FStringStart)
        {
            return ParseString();
        }
        else if (CurrentTokenType is TokenType.Number)
        {
            return ParseNumber();
        }
        else if (CurrentTokenType is TokenType.Ellipsis)
        {
            MoveNextToken();
            return Ast.Constant(PyEllipsisObject.Ellipsis).With(metaInfo);
        }
        else if (CurrentTokenType is TokenType.LeftParen or TokenType.LeftSquareBracket or TokenType.LeftBrace)
        {
            return ParseEnclosure();
        }
        else
        {
            if (CurrentTokenType is TokenType.Indent)
                throw _context.ThrowableIndentationError("unexpected indent");

            throw _context.ThrowableSyntaxError("invalid syntax");
        }

    }

    [GrammarSyntaxRule("slice")]
    private AstExprNode ParseSlice()
    {
        if (TestIsAssignmentExpression())
            return ParseAssignmentExpression();

        AstExprNode? lowerBound, upperBound, stride;
        if (CurrentTokenType is TokenType.Colon)
        {
            lowerBound = null;
        }
        else
        {
            lowerBound = ParseExpression();

            // the slice item is an expression instead of a proper_slice
            if (CurrentTokenType is TokenType.Comma or TokenType.RightSquareBracket)
                return lowerBound;
        }

        // [lower_bound] ":" [upper_bound] [ ":" [stride] ]
        //                ^ we are here
        EnsureTokenTypeThenMove(TokenType.Colon);

        // the slice item has 1 colon, optional lowerBound, no upperBound, no stride
        if (CurrentTokenType is TokenType.Comma or TokenType.RightSquareBracket)
            return Ast.Slice(lowerBound, null, null);

        if (CurrentTokenType is TokenType.Colon)
        {
            upperBound = null;
        }
        else
        {
            upperBound = ParseExpression();

            // the slice item has 1 colon, optional lowerBound, upperBound, no stride
            if (CurrentTokenType is TokenType.Comma or TokenType.RightSquareBracket)
                return Ast.Slice(lowerBound, upperBound, null);
        }

        // [lower_bound] ":" [upper_bound] ":" [stride]
        //                                  ^ we are here
        EnsureTokenTypeThenMove(TokenType.Colon);

        // the slice item has 2 colon, optional lowerBound, optional upperBound, no stride
        if (CurrentTokenType is TokenType.Comma or TokenType.RightSquareBracket)
            return Ast.Slice(lowerBound, upperBound, null);

        // the slice item has 2 colon, optional lowerBound, optional upperBound, stride
        stride = ParseExpression();
        return Ast.Slice(lowerBound, upperBound, stride);

    }

    [GrammarSyntaxRule("slices")]
    private AstExprNode ParseSlices()
    {
        var list = ParseSomethingList(ParseSliceOrStarredExpression, StopPredicates.UntilRightSquareBracket, out var endsWithComma);

        // return directly if it is single slice without comma
        if (list.Count is 1 && endsWithComma is null && list[0] is not StarredNode)
            return list[0];

        // single StarredNode is allowed
        return PackSomething(list, endsWithComma, Ast.Tuple);

        AstExprNode ParseSliceOrStarredExpression()
        {
            if (CurrentTokenType is TokenType.Star)
                return ParseStarredExpression();

            return ParseSlice();
        }
    }

    [GrammarSyntaxRule("primary")]
    private AstExprNode ParsePrimary()
    {
        var startMetaInfo = CreateAstMetaInfo();
        var primary = ParseAtom();

        while (CurrentTokenType is TokenType.Dot or TokenType.LeftParen or TokenType.LeftSquareBracket)
        {
            if (CurrentTokenType is TokenType.Dot)
            {
                MoveNextToken();
                var name = ParseIdentifier();
                primary = Ast.Attribute(primary, name).With(startMetaInfo.WithPreviousEnd());
            }
            else if (CurrentTokenType is TokenType.LeftParen)
            {
                MoveNextToken();

                var pos = TokenStreamPosition;
                var isGenExp = CurrentTokenType is not (TokenType.Star or TokenType.DoubleStar or TokenType.RightParen);
                if (isGenExp)
                {
                    _ = ParseNamedExpression();
                    isGenExp = IsCurrentKeyword("for");
                }
                TokenStreamPosition = pos;

                if (isGenExp)
                {
                    var metaInfo = CreateAstMetaInfo();
                    var (elts, generators) = ParseComprehension();
                    var genExp = Ast.GeneratorExp(elts, generators).With(metaInfo.WithPreviousEnd());
                    primary = Ast.Call(primary, [genExp], []).With(startMetaInfo.WithEnd());
                }
                else
                {
                    var (args, kwargs) = ParseArgumentList();
                    primary = Ast.Call(primary, args, kwargs).With(startMetaInfo.WithEnd());
                }

                EnsureTokenTypeThenMove(TokenType.RightParen);
            }
            else if (CurrentTokenType is TokenType.LeftSquareBracket)
            {
                var currentMetaInfo = startMetaInfo.WithCrucial();
                MoveNextToken();

                if (CurrentTokenType is TokenType.RightSquareBracket)
                    throw _context.ThrowableSyntaxError("invalid syntax. Perhaps you forgot a comma?");

                var slices = ParseSlices();
                EnsureTokenTypeThenMove(TokenType.RightSquareBracket);

                primary = Ast.Subscript(primary, slices).With(currentMetaInfo.WithAllEnd());
            }
            else
            {
                throw new UnreachableException();
            }
        }

        return primary;
    }

    [GrammarSyntaxRule("await_primary")]
    private AstExprNode ParseAwaitPrimary()
    {
        if (!IsCurrentKeyword("await"))
            return ParsePrimary();

        //var metaInfo = CreateAstMetaInfo();
        //MoveNextToken();
        //var expr = ParsePrimary();
        throw new NotSupportedException();
    }

    [GrammarSyntaxRule("power")]
    private AstExprNode ParsePower()
    {
        var metaInfo = CreateAstMetaInfo();
        var expr = ParseAwaitPrimary();

        if (CurrentTokenType is not TokenType.DoubleStar)
            return expr;

        metaInfo = metaInfo.WithCrucial();
        MoveNextToken();
        var factor = ParseFactor();
        return Ast.Pow(expr, factor).With(metaInfo.WithPreviousEnd());
    }

    [GrammarSyntaxRule("factor")]
    private AstExprNode ParseFactor()
    {
        return ParseUnaryOp(ParsePower,
            [TokenType.Plus, TokenType.Minus, TokenType.Tilde],
            [Ast.UAdd, Ast.USub, Ast.Invert]);
    }

    [GrammarSyntaxRule("term")]
    private AstExprNode ParseTerm()
    {
        return ParseBinOp(ParseFactor,
            [TokenType.Star, TokenType.Slash, TokenType.DoubleSlash, TokenType.Percent, TokenType.At],
            [Ast.Mult, Ast.Div, Ast.FloorDiv, Ast.Mod, Ast.MatMult]);
    }

    [GrammarSyntaxRule("sum")]
    private AstExprNode ParseSum()
    {
        return ParseBinOp(ParseTerm, [TokenType.Plus, TokenType.Minus], [Ast.Add, Ast.Sub]);
    }

    [GrammarSyntaxRule("shift_expr")]
    private AstExprNode ParseShiftExpr()
    {
        return ParseBinOp(ParseSum, [TokenType.LeftShift, TokenType.RightShift], [Ast.LShift, Ast.RShift]);
    }

    [GrammarSyntaxRule("bitwise_and")]
    private AstExprNode ParseBitwiseAnd()
    {
        return ParseBinOp(ParseShiftExpr, TokenType.Ampersand, Ast.BitAnd);
    }

    [GrammarSyntaxRule("bitwise_xor")]
    private AstExprNode ParseBitwiseXor()
    {
        return ParseBinOp(ParseBitwiseAnd, TokenType.Caret, Ast.BitXor);
    }

    [GrammarSyntaxRule("bitwise_or")]
    private AstExprNode ParseBitwiseOr()
    {
        return ParseBinOp(ParseBitwiseXor, TokenType.Pipe, Ast.BitOr);
    }

    private AstExprNode ParseUnaryOp(Func<AstExprNode> parse, ReadOnlySpan<TokenType> ops, ReadOnlySpan<Func<AstExprNode, AstExprNode>> wrappers)
    {
        Debug.Assert(ops.Length == wrappers.Length);

        var metaInfo = CreateAstMetaInfo();

        var index = ops.IndexOf(CurrentTokenType);
        if (index is -1)
            return parse();

        MoveNextToken();
        var innerValue = ParseUnaryOp(parse, ops, wrappers);
        var value = wrappers[index](innerValue).With(metaInfo.WithPreviousEnd());
        return value;
    }

    private AstExprNode ParseBinOp(Func<AstExprNode> parse, ReadOnlySpan<TokenType> ops, ReadOnlySpan<Func<AstExprNode, AstExprNode, AstExprNode>> combines)
    {
        Debug.Assert(ops.Length == combines.Length);

        var startMetaInfo = CreateAstMetaInfo();
        var leftExpr = parse();

        while (true)
        {
            var index = ops.IndexOf(CurrentTokenType);
            if (index is -1)
                break;

            var currentMetaInfo = startMetaInfo.WithCrucial();
            MoveNextToken();
            var rightExpr = parse();
            leftExpr = combines[index](leftExpr, rightExpr).With(currentMetaInfo.WithPreviousEnd());
        }

        return leftExpr;
    }

    private AstExprNode ParseBinOp(Func<AstExprNode> parse, TokenType op, Func<AstExprNode, AstExprNode, AstExprNode> combine)
    {
        return ParseBinOp(parse, [op], [combine]);
    }

    [GrammarSyntaxRule("compare_op_bitwise_or_pair")]
    private bool TryParseCompareOpBitwiseOrPair(out (CmpopType Op, AstExprNode Comparator) pair)
    {
        CmpopType op;
        if (IsCurrentKeyword("is"))
        {
            MoveNextToken();
            if (IsCurrentKeyword("not"))
            {
                MoveNextToken();
                op = CmpopType.IsNot;
            }
            else
            {
                op = CmpopType.Is;
            }
        }
        else if (IsCurrentKeyword("in"))
        {
            MoveNextToken();
            op = CmpopType.In;

        }
        else if (IsCurrentKeyword("not"))
        {
            MoveNextToken();
            EnsureKeywordThenMove("in");
            op = CmpopType.NotIn;
        }
        else if (CurrentTokenType is TokenType.Less)
        {
            MoveNextToken();
            op = CmpopType.Lt;
        }
        else if (CurrentTokenType is TokenType.LessEqual)
        {
            MoveNextToken();
            op = CmpopType.LtE;
        }
        else if (CurrentTokenType is TokenType.Greater)
        {
            MoveNextToken();
            op = CmpopType.Gt;
        }
        else if (CurrentTokenType is TokenType.GreaterEqual)
        {
            MoveNextToken();
            op = CmpopType.GtE;
        }
        else if (CurrentTokenType is TokenType.DoubleEqual)
        {
            MoveNextToken();
            op = CmpopType.Eq;
        }
        else if (CurrentTokenType is TokenType.NotEqual)
        {
            MoveNextToken();
            op = CmpopType.NotEq;
        }
        else
        {
            pair = default;
            return false;
        }

        var comparator = ParseBitwiseOr();

        pair = (op, comparator);
        return true;
    }

    [GrammarSyntaxRule("comparison")]
    private AstExprNode ParseComparison()
    {
        var metaInfo = CreateAstMetaInfo();

        var expr = ParseBitwiseOr();
        if (!TryParseCompareOpBitwiseOrPair(out var pair))
            return expr;

        List<(CmpopType, AstExprNode)> pairs = [pair];

        while (TryParseCompareOpBitwiseOrPair(out pair))
            pairs.Add(pair);

        return Ast.Compare(expr, pairs).With(metaInfo.WithPreviousEnd());
    }

    [GrammarSyntaxRule("inversion")]
    private AstExprNode ParseInversion()
    {
        if (!IsCurrentKeyword("not"))
            return ParseComparison();

        var metaInfo = CreateAstMetaInfo();
        MoveNextToken();
        return Ast.Not(ParseInversion()).With(metaInfo.WithPreviousEnd());
    }

    [GrammarSyntaxRule("conjunction")]
    private AstExprNode ParseConjunction()
    {
        var inversion = ParseInversion();
        if (!IsCurrentKeyword("and"))
            return inversion;

        var metaInfo = CreateAstMetaInfo();

        List<AstExprNode> values = [inversion];
        while (IsCurrentKeyword("and"))
        {
            MoveNextToken();
            values.Add(ParseInversion());
        }

        return Ast.And(values).With(metaInfo.WithPreviousEnd());
    }

    [GrammarSyntaxRule("disjunction")]
    private AstExprNode ParseDisjunction()
    {
        var conjunction = ParseConjunction();
        if (!IsCurrentKeyword("or"))
            return conjunction;

        var metaInfo = CreateAstMetaInfo();

        List<AstExprNode> values = [conjunction];
        while (IsCurrentKeyword("or"))
        {
            MoveNextToken();
            values.Add(ParseConjunction());
        }

        return Ast.Or(values).With(metaInfo.WithPreviousEnd());
    }

    /// <summary>
    /// lambda_expr: "lambda" [<see cref="ParseParameterList(StopPredicate)">parameter_list</see>] ":" <see cref="ParseExpression">expression</see>
    /// </summary>
    /// <returns></returns>
    private LambdaNode ParseLambdaExpr()
    {
        var metaInfo = CreateAstMetaInfo();
        EnsureKeywordThenMove("lambda");
        var args = CurrentTokenType is TokenType.Colon ? Ast.Arguments() : ParseParameterList(StopPredicates.UntilColon, allowAnnotation: false);
        EnsureTokenTypeThenMove(TokenType.Colon);
        return Ast.Lambda(args, ParseExpression()).With(metaInfo);
    }

    [GrammarSyntaxRule("expression")]
    private AstExprNode ParseExpression()
    {
        if (IsCurrentKeyword("lambda"))
            return ParseLambdaExpr();

        var metaInfo = CreateAstMetaInfo();

        var body = ParseDisjunction();
        if (!IsCurrentKeyword("if"))
            return body;

        MoveNextToken();
        var test = ParseDisjunction();
        EnsureKeywordThenMove("else");
        var orElse = ParseExpression();
        return Ast.IfExp(test, body, orElse).With(metaInfo.WithPreviousEnd());
    }

    [GrammarSyntaxRule("named_expression")]
    private AstExprNode ParseNamedExpression()
    {
        if (TestIsAssignmentExpression())
            return ParseAssignmentExpression();

        var expr = ParseExpression();
        if (CurrentTokenType is TokenType.ColonEqual)
            throw _context.ThrowableSyntaxError($"cannot use assignment expressions with {AstUtils.GetExprNodeName(expr)}");
        return expr;
    }

    private bool TestIsAssignmentExpression()
    {
        if (CurrentTokenType is not TokenType.Name)
            return false;

        var pos = TokenStreamPosition;
        MoveNextToken();
        var isAssignment = CurrentTokenType is TokenType.ColonEqual;
        TokenStreamPosition = pos;
        return isAssignment;
    }

    [GrammarSyntaxRule("assignment_expression")]
    private NamedExprNode ParseAssignmentExpression()
    {
        var metaInfo = CreateAstMetaInfo();
        var name = ParseIdentifier();
        var target = Ast.Name(name);
        EnsureTokenTypeThenMove(TokenType.ColonEqual);
        var value = ParseExpression();
        return Ast.NamedExpr(target, value).With(metaInfo.WithPreviousEnd());
    }

    [GrammarSyntaxRule("star_expression")]
    private AstExprNode ParseStarExpression()
    {
        if (CurrentTokenType is not TokenType.Star)
            return ParseExpression();

        return ParseStarredExpression();
    }

    [GrammarSyntaxRule("star_expressions")]
    private List<AstExprNode> ParseStarExpressions(StopPredicate predicate, out TokenInfo? endsWithComma)
    {
        return ParseSomethingList(ParseStarExpression, predicate, out endsWithComma);
    }

    [GrammarSyntaxRule("starred_expression")]
    private StarredNode ParseStarredExpression()
    {
        var metaInfo = CreateAstMetaInfo();
        EnsureTokenTypeThenMove(TokenType.Star);
        var value = ParseBitwiseOr();
        return Ast.Starred(value).With(metaInfo.WithPreviousEnd());
    }

    [GrammarSyntaxRule("star_named_expression")]
    private AstExprNode ParseStarNamedExpression()
    {
        if (CurrentTokenType is TokenType.Star)
            return ParseStarExpression();

        return ParseNamedExpression();
    }

    [GrammarSyntaxRule("star_named_expressions")]
    private List<AstExprNode> ParseStarNamedExpressions(StopPredicate predicate, out TokenInfo? endsWithComma)
    {
        return ParseSomethingList(ParseStarNamedExpression, predicate, out endsWithComma);
    }

    /// <summary>
    /// flexible_expression: <see cref="ParseNamedExpression">assignment_expression</see> | <see cref="ParseStarExpression">starred_expression</see>
    /// </summary>
    /// <returns></returns>
    private AstExprNode ParseFlexibleExpression()
    {
        if (CurrentTokenType is TokenType.Star)
            return ParseStarExpression();

        return ParseNamedExpression();
    }

    /// <summary>
    /// flexible_expression_list: <see cref="ParseFlexibleExpression">flexible_expression</see> ("," <see cref="ParseFlexibleExpression">flexible_expression</see>)* [","]
    /// </summary>
    /// <param name="predicate"></param>
    /// <returns></returns>
    /// <param name="endsWithComma"></param>
    private List<AstExprNode> ParseFlexibleExpressionList(StopPredicate predicate, out TokenInfo? endsWithComma)
    {
        return ParseSomethingList(ParseFlexibleExpression, predicate, out endsWithComma);
    }

    /// <summary>
    /// [expr] => Unwrap
    /// <br/> [expr, ] => MakeTuple
    /// <br/> [expr, expr] => MakeTuple
    /// </summary>
    /// <param name="list"></param>
    /// <param name="endsWithComma"></param>
    /// <returns></returns>
    private static AstExprNode UnwrapOrMakeTuple(List<AstExprNode> list, TokenInfo? endsWithComma)
    {
        return UnwrapOrPackSomething(list, endsWithComma, Ast.Tuple);
    }

    private static TResult PackSomething<TSource, TResult>(List<TSource> list, TokenInfo? endsWithComma, Func<List<TSource>, TResult> packer)
        where TSource : AstNode
        where TResult : AstNode
    {
        CodeMetaInfo? metaInfo = null;

        var startMetaInfo = list[0].MetaInfo;
        if (startMetaInfo is not null)
        {
            metaInfo = new CodeMetaInfo
            {
                Source = startMetaInfo.Source,
                Start = startMetaInfo.Start,
            };
            if (endsWithComma is not null)
            {
                metaInfo.End = endsWithComma.End;
            }
            else
            {
                var endMetaInfo = list[^1].MetaInfo;
                if (endMetaInfo is not null)
                    metaInfo.End = endMetaInfo.End;
                else
                    metaInfo = null;
            }
        }

        return packer(list).With(metaInfo);
    }

    private static T UnwrapOrPackSomething<T>(List<T> list, TokenInfo? endsWithComma, Func<List<T>, T> packer) where T : AstNode
    {
        Debug.Assert(list.Count > 0);

        if (list.Count is 1 && endsWithComma is null)
            return list[0];

        return PackSomething(list, endsWithComma, packer);
    }

    /// <summary>
    /// target: <see cref="ParseIdentifier">identifier</see>
    ///         | "(" [<see cref="ParseTargetList">target_list</see>] ")"
    ///         | "[" [<see cref="ParseTargetList">target_list</see>] "]"
    ///         | attributeref
    ///         | subscription
    ///         | slicing
    ///         | "*" target
    /// </summary>
    /// <returns></returns>
    /// <exception cref="PyRuntimeException"></exception>
    private AstExprNode ParseTarget()
    {
        if (CurrentTokenType is TokenType.Star)
        {
            _ = ParseTarget();
            throw new NotSupportedException();
        }

        var target = ParsePrimary();
        if (!target.IsValidTarget())
            throw _context.ThrowableSyntaxError($"cannot assign to {AstUtils.GetExprNodeName(target)}");
        return target;
    }

    /// <summary>
    /// target_list: <see cref="ParseTarget">target</see> ("," <see cref="ParseTarget">target</see>)* [","]
    /// </summary>
    /// <param name="predicate"></param>
    /// <param name="endsWithComma"></param>
    /// <returns></returns>
    private List<AstExprNode> ParseTargetList(StopPredicate predicate, out TokenInfo? endsWithComma)
    {
        return ParseSomethingList(ParseTarget, predicate, out endsWithComma);
    }

    /// <summary>
    /// comp_for: ["async"] "for" <see cref="ParseTargetList(StopPredicate, out bool)">target_list</see> "in" <see cref="ParseDisjunction">or_test</see> [comp_iter]
    /// <br/> comp_iter: comp_for | comp_if
    /// <br/> comp_if: "if" <see cref="ParseDisjunction">or_test</see> [comp_iter]
    /// </summary>
    /// <returns></returns>
    /// <exception cref="NotSupportedException"></exception>
    private List<AstComprehensionNode> ParseCompFor()
    {
        List<AstComprehensionNode> generators = [ParseCompForImpl()];
        while (IsCurrentKeyword("for"))
        {
            generators.Add(ParseCompForImpl());
        }
        return generators;

        AstComprehensionNode ParseCompForImpl()
        {
            if (IsCurrentKeyword("async"))
                throw new NotSupportedException();
            EnsureKeywordThenMove("for");

            var targetList = ParseTargetList(StopPredicates.UntilKeywordIn, out var endsWithComma);
            var target = UnwrapOrMakeTuple(targetList, endsWithComma);
            EnsureKeywordThenMove("in");
            var iter = ParseDisjunction();
            var ifs = new List<AstExprNode>();
            while (IsCurrentKeyword("if"))
            {
                MoveNextToken();
                ifs.Add(ParseDisjunction());
            }

            return Ast.Comprehension(target, iter, ifs);
        }
    }

    /// <summary>
    /// comprehension: <see cref="ParseNamedExpression">assignment_expression</see> <see cref="ParseCompFor">comp_for</see>
    /// </summary>
    /// <returns></returns>
    private (AstExprNode Elt, List<AstComprehensionNode> Generators) ParseComprehension()
    {
        var elt = ParseNamedExpression();
        var generators = ParseCompFor();
        return (elt, generators);
    }

    private bool TestIsComprehension()
    {
        if (CurrentTokenType is TokenType.Star)
        {
            // it must be a starred_expression
            // comprehension should start with assignment_expression

            return false;
        }
        else
        {
            var index = TokenStreamPosition;
            _ = ParseNamedExpression();
            var isComp = IsCurrentKeyword("for") || IsCurrentKeyword("async");
            TokenStreamPosition = index;
            return isComp;
        }
    }

    /// <summary>
    /// list_display: "[" [<see cref="ParseFlexibleExpressionList(StopPredicate, out bool)">flexible_expression_list</see> | <see cref="ParseComprehension">comprehension</see>] "]"
    /// </summary>
    /// <returns></returns>
    private AstExprNode ParseListDisplay()
    {
        var metaInfo = CreateAstMetaInfo();

        EnsureTokenTypeThenMove(TokenType.LeftSquareBracket);

        if (CurrentTokenType is TokenType.RightSquareBracket)
        {
            MoveNextToken();
            return Ast.List([]).With(metaInfo.WithPreviousEnd());
        }

        if (TestIsComprehension())
        {
            var (elt, generators) = ParseComprehension();
            EnsureTokenTypeThenMove(TokenType.RightSquareBracket);
            return Ast.ListComp(elt, generators).With(metaInfo.WithPreviousEnd());
        }

        var list = ParseFlexibleExpressionList(StopPredicates.UntilRightSquareBracket, out _);
        EnsureTokenTypeThenMove(TokenType.RightSquareBracket);
        return Ast.List(list).With(metaInfo.WithPreviousEnd());
    }

    /// <summary>
    /// set_display: "{" (<see cref="ParseFlexibleExpressionList(StopPredicate, out bool)">flexible_expression_list</see> | <see cref="ParseComprehension">comprehension</see>) "}"
    /// </summary>
    /// <returns></returns>
    private AstExprNode ParseSetDisplay()
    {
        var metaInfo = CreateAstMetaInfo();

        EnsureTokenTypeThenMove(TokenType.LeftBrace);

        if (TestIsComprehension())
        {
            var (elt, generators) = ParseComprehension();
            EnsureTokenTypeThenMove(TokenType.RightBrace);
            return Ast.SetComp(elt, generators).With(metaInfo.WithPreviousEnd());
        }

        var set = ParseFlexibleExpressionList(StopPredicates.UntilRightBrace, out _);
        EnsureTokenTypeThenMove(TokenType.RightBrace);
        return Ast.Set(set).With(metaInfo.WithPreviousEnd());
    }

    /// <summary>
    /// dict_item: <see cref="ParseExpression">expression</see> ":" <see cref="ParseExpression">expression</see> | "**" <see cref="ParseBitwiseOr">or_expr</see>
    /// </summary>
    /// <returns></returns>
    private (AstExprNode Key, AstExprNode Value) ParseDictItem()
    {
        if (CurrentTokenType is TokenType.DoubleStar)
            throw new NotSupportedException();

        var key = ParseExpression();
        EnsureTokenTypeThenMove(TokenType.Colon);
        var value = ParseExpression();
        return (key, value);
    }

    /// <summary>
    /// dict_item_list: <see cref="ParseDictItem">dict_item</see> ("," <see cref="ParseDictItem">dict_item</see>)* [","]
    /// </summary>
    /// <returns></returns>
    private List<(AstExprNode Key, AstExprNode Value)> ParseDictItemList()
    {
        List<(AstExprNode Key, AstExprNode Value)> list = [ParseDictItem()];
        while (CurrentTokenType is TokenType.Comma)
        {
            MoveNextToken();

            if (CurrentTokenType is TokenType.RightBrace)
                break;

            list.Add(ParseDictItem());
        }
        return list;
    }

    /// <summary>
    /// dict_display: "{" [<see cref="ParseDictItemList">dict_item_list</see> | dict_comprehension] "}"
    /// <br/> dict_comprehension: <see cref="ParseExpression">expression</see> ":" <see cref="ParseExpression">expression</see> <see cref="ParseCompFor">comp_for</see>
    /// </summary>
    /// <returns></returns>
    private AstExprNode ParseDictDisplay()
    {
        var metaInfo = CreateAstMetaInfo();

        EnsureTokenTypeThenMove(TokenType.LeftBrace);

        if (CurrentTokenType is TokenType.RightBrace)
        {
            MoveNextToken();
            return Ast.Dict([]).With(metaInfo.WithPreviousEnd());
        }

        bool isComp;

        if (CurrentTokenType is TokenType.Star)
        {
            isComp = false;
        }
        else
        {
            var index = TokenStreamPosition;
            _ = ParseDictItem();
            isComp = IsCurrentKeyword("for") || IsCurrentKeyword("async");
            TokenStreamPosition = index;
        }

        if (isComp)
        {
            var (key, value) = ParseDictItem();
            var generators = ParseCompFor();
            EnsureTokenTypeThenMove(TokenType.RightBrace);
            return Ast.DictComp(key, value, generators).With(metaInfo.WithPreviousEnd());
        }

        List<KeyValuePair<AstExprNode, AstExprNode>> pairs = [];
        var list = ParseDictItemList();
        foreach (var (key, value) in list)
        {
            pairs.Add(KeyValuePair.Create(key, value));
        }
        EnsureTokenTypeThenMove(TokenType.RightBrace);
        return Ast.Dict(pairs).With(metaInfo.WithPreviousEnd());
    }

    /// <summary>
    /// generator_expression: "(" <see cref="ParseComprehension">comprehension</see> ")"
    /// </summary>
    /// <returns></returns>
    private GeneratorExpNode ParseGeneratorExpression()
    {
        var metaInfo = CreateAstMetaInfo();
        EnsureTokenTypeThenMove(TokenType.LeftParen);
        var (elt, generators) = ParseComprehension();
        EnsureTokenTypeThenMove(TokenType.RightParen);
        return Ast.GeneratorExp(elt, generators).With(metaInfo.WithPreviousEnd());
    }

    private AstArgumentsNode ParseParameterList(StopPredicate predicate, bool allowAnnotation)
    {
        const int StateArgs = 0, StateAfterPosonly = 1, StateKwonly = 3, StateEnd = 4;

        List<AstArgNode> posonlyArgs = [];
        List<AstArgNode> args = [];
        AstArgNode? varArg = null;
        List<AstArgNode> kwonlyArgs = [];
        AstArgNode? kwArg = null;
        List<AstExprNode?> kwDefaults = [];
        List<AstExprNode> defaults = [];

        //var arguments = new AstArgumentsNode();
        var state = StateArgs;
        var needDefault = false;

        ParseParameter();
        while (CurrentTokenType is TokenType.Comma)
        {
            MoveNextToken();
            if (predicate(CurrentToken))
                break;
            ParseParameter();
        }
        return Ast.Arguments(posonlyArgs, args, varArg, kwonlyArgs, kwArg, kwDefaults, defaults);

        void ParseParameter()
        {
            if (state is StateEnd)
                throw _context.ThrowableSyntaxError($"arguments cannot follow var-keyword argument");

            switch (CurrentTokenType)
            {
                case TokenType.Slash:
                    if (state is not StateArgs)
                    {
                        if (state is StateAfterPosonly)
                            throw _context.ThrowableSyntaxError("/ may appear only once");
                        else if (state is StateKwonly)
                            throw _context.ThrowableSyntaxError("/ must be ahead of *");
                        else
                            throw new UnreachableException();
                    }

                    if (args.Count is 0)
                        throw _context.ThrowableSyntaxError("at least one argument must precede /");

                    MoveNextToken();
                    posonlyArgs.AddRange(args);
                    args.Clear();
                    state = StateAfterPosonly;
                    break;

                case TokenType.Star:
                    if (state is StateKwonly)
                        throw _context.ThrowableSyntaxError("* may appear only once");

                    MoveNextToken();
                    if (CurrentTokenType is TokenType.Name)
                    {
                        var starArg = ParseIdentifier();
                        var starAnnotation = null as AstExprNode;

                        if (allowAnnotation && CurrentTokenType is TokenType.Colon)
                        {
                            MoveNextToken();
                            // here allows starred expr
                            starAnnotation = ParseStarExpression();
                        }

                        varArg = Ast.Arg(starArg, starAnnotation);
                    }
                    state = StateKwonly;
                    needDefault = false;
                    break;

                case TokenType.DoubleStar:
                    MoveNextToken();
                    if (CurrentTokenType is not TokenType.Name)
                        throw _context.ThrowableSyntaxError("invalid syntax");

                    var doubleStarArg = ParseIdentifier();
                    var doubleStarAnnotation = null as AstExprNode;

                    if (allowAnnotation && CurrentTokenType is TokenType.Colon)
                    {
                        MoveNextToken();
                        doubleStarAnnotation = ParseExpression();
                    }

                    kwArg = Ast.Arg(doubleStarArg, doubleStarAnnotation);
                    state = StateEnd;
                    break;

                case TokenType.Name:
                    var arg = ParseIdentifier();
                    var annotation = null as AstExprNode;
                    if (allowAnnotation && CurrentTokenType is TokenType.Colon)
                    {
                        MoveNextToken();
                        annotation = ParseExpression();
                    }

                    var argNode = Ast.Arg(arg, annotation);
                    AstExprNode? defaultValue;
                    if (CurrentTokenType is TokenType.Equal)
                    {
                        MoveNextToken();
                        defaultValue = ParseExpression();
                        if (state is StateArgs or StateAfterPosonly)
                            needDefault = true;
                    }
                    else if (needDefault)
                    {
                        throw _context.ThrowableSyntaxError("parameter without a default follows parameter with a default");
                    }
                    else
                    {
                        defaultValue = null;
                    }

                    if (state is StateArgs or StateAfterPosonly)
                    {
                        args.Add(argNode);
                        if (defaultValue is not null)
                            defaults.Add(defaultValue);
                    }
                    else
                    {
                        kwonlyArgs.Add(argNode);
                        kwDefaults.Add(defaultValue);
                    }

                    break;

                default:
                    throw _context.ThrowableSyntaxError("invalid syntax");
            }
        }
    }

    [GrammarSyntaxRule("expressions")]
    private List<AstExprNode> ParseExpressions(StopPredicate predicate, out TokenInfo? endsWithComma)
    {
        return ParseSomethingList(ParseExpression, predicate, out endsWithComma);
    }

    /// <summary>
    /// parenth_form: "(" [<see cref="ParseFlexibleExpressionList(StopPredicate, out bool)">flexible_expression_list</see>] ")"
    /// </summary>
    /// <returns></returns>
    private AstExprNode ParseParenthForm()
    {
        var metaInfo = CreateAstMetaInfo();
        EnsureTokenTypeThenMove(TokenType.LeftParen);

        // () is an empty tuple
        if (CurrentTokenType is TokenType.RightParen)
        {
            MoveNextToken();
            return Ast.Tuple([]).With(metaInfo.WithPreviousEnd());
        }

        var list = ParseFlexibleExpressionList(StopPredicates.UntilRightParen, out var endsWithComma);
        EnsureTokenTypeThenMove(TokenType.RightParen);
        return UnwrapOrMakeTuple(list, endsWithComma);
    }

    [GrammarSyntaxRule("yield_expr")]
    private AstExprNode ParseYieldExpr()
    {
        var metaInfo = CreateAstMetaInfo();
        EnsureKeywordThenMove("yield");

        if (IsCurrentKeyword("from"))
        {
            MoveNextToken();
            var expr = ParseExpression();
            return Ast.YieldFrom(expr).With(metaInfo.WithPreviousEnd());
        }

        if (StopPredicates.UntilRightParenOrNewLineOrSemicolon(CurrentToken))
            return Ast.Yield(null).With(metaInfo);

        var list = ParseStarExpressions(StopPredicates.UntilRightParenOrNewLineOrSemicolon, out var endsWithComma);
        var value = UnwrapOrMakeTuple(list, endsWithComma);
        return Ast.Yield(value).With(metaInfo.WithPreviousEnd());
    }

    /// <summary>
    /// argument_list: [positional_arguments ["," keywords_arguments] [","] | keywords_arguments [","]]
    /// <br/> positional_arguments: <see cref="ParseExpression">expression</see> ("," <see cref="ParseExpression">expression</see>)*
    /// <br/> keywords_arguments: keyword_item ("," keyword_item)*
    /// <br/> keyword_item: <see cref="ParseIdentifier">identifier</see> "=" <see cref="ParseExpression">expression</see>
    /// </summary>
    /// <returns></returns>
    /// <exception cref="PyRuntimeException"></exception>
    private (List<AstExprNode> Args, List<AstKeywordNode> Kwargs) ParseArgumentList()
    {
        var args = new List<AstExprNode>();
        var kwargs = new List<AstKeywordNode>();
        bool iskw = false;

        while (CurrentTokenType is not TokenType.RightParen)
        {
            if (!iskw)
                ParseArgOrKwarg();
            else
                ParseKwarg();

            if (CurrentTokenType is TokenType.Comma)
                MoveNextToken();
            else if (CurrentTokenType is not TokenType.RightParen)
                throw _context.ThrowableSyntaxError("'(' was never closed");
        }

        return (args, kwargs);

        void ParseArgOrKwarg()
        {
            if (CurrentTokenType is TokenType.DoubleStar)
            {
                MoveNextToken();
                iskw = true;
                var value = ParseDisjunction();
                kwargs.Add(Ast.Keyword(null, value));
                return;
            }

            var arg = ParseFlexibleExpression();
            if (CurrentTokenType is TokenType.Equal)
            {
                iskw = true;

                if (arg is not NameNode argName)
                    throw _context.ThrowableSyntaxError("expression cannot contain assignment, perhaps you meant \"==\"?");

                MoveNextToken();
                var value = ParseExpression();

                kwargs.Add(Ast.Keyword(argName.Id, value));
            }
            else
            {
                args.Add(arg);
            }
        }
        void ParseKwarg()
        {
            if (CurrentTokenType is TokenType.DoubleStar)
            {
                MoveNextToken();
                iskw = true;
                var value = ParseDisjunction();
                kwargs.Add(Ast.Keyword(null, value));
            }
            else
            {
                var arg = ParseIdentifier();
                if (CurrentTokenType is not TokenType.Equal)
                    throw _context.ThrowableSyntaxError("positional argument follows keyword argument");

                MoveNextToken();
                var value = ParseExpression();
                kwargs.Add(Ast.Keyword(arg, value));
            }
        }
    }

    private List<T> ParseSomethingList<T>(Func<T> parse, StopPredicate predicate, out TokenInfo? endsWithComma, TokenType separator = TokenType.Comma)
    {
        endsWithComma = null;
        List<T> list = [parse()];
        while (CurrentTokenType == separator)
        {
            MoveNextToken();
            if (predicate(CurrentToken))
            {
                endsWithComma = CurrentToken;
                break;
            }
            list.Add(parse());
        }

        if (CurrentTokenType is TokenType.Equal && !predicate(CurrentToken) && list[^1] is AstExprNode expr)
            throw ThrowableSyntaxErrorCausedByInvalidEqualAfterExpr(expr);

        return list;
    }
}
