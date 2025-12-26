using PySharp.PyModules.Builtins;
using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;
using PySharp.Tokenization;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Numerics;
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
    /// <exception cref="AstException"></exception>
    private string ParseIdentifier()
    {
        EnsureTokenType(TokenType.Name);
        if (IsKeyword(CurrentToken.String))
            throw new AstException("should be id not kw");
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
            EnsureTokenTypeThenMove(TokenType.LeftParen);

            // () is an empty tuple
            if (CurrentTokenType is TokenType.RightParen)
                return ParseParenthForm();

            if (IsCurrentKeyword("yield"))
                return ParseYieldAtom();

            // generator_expression
            if (TestIsComprehension())
                return ParseGeneratorExpression();

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

        throw new NotSupportedException();
    }

    private AstExprNode ParseFExpression()
    {
        if (IsCurrentKeyword("yield"))
            throw new NotSupportedException();

        var list = ParseFlexibleExpressionList(StopPredicates.UntilRightBrace, out var endsWithComma);
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
                var str = FromLiteralToString(_context, CurrentToken.String, true);
                var node = AstNode.Constant(str, CreateMetaInfo());
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
        return AstNode.JoinedStr(nodes, null /* TODO: need MetaInfo? */);
    }

    private FormattedValueNode ParseFStringReplacementFieldWithoutBraces(out ConstantNode? debugSpecifier)
    {
        if (CurrentTokenType is TokenType.RightBrace)
            throw new AstException("f-string: valid expression required before '}'");

        var start = CurrentToken.Start;
        var metaInfo = CreateMetaInfo();
        var startPosition = TokenStreamPosition;
        var fexpr = ParseFExpression();


        if (CurrentTokenType is TokenType.Equal)
        {
            MoveNextToken();
            var end = CurrentToken.Start;
            metaInfo = CopyThenWithEnd(metaInfo);
            var endPosition = TokenStreamPosition;

            TokenStreamPosition = startPosition;

            var content = new StringBuilder();
            var currentLine = 0;
            while (TokenStreamPosition < endPosition - 1)
            {
                var currentToken = _tokenStream.CurrentToken;
                var line = currentToken.Line;
                var index = 0;
                var nextIndex = line.IndexOf('\n') + 1;
                for (int i = currentToken.Start.Line; i <= currentToken.End.Line; i++)
                {
                    if (i > currentLine)
                    {
                        if (i == start.Line)
                        {
                            if (i == end.Line)
                            {
                                content.Append(line[(index + start.Offset)..(index + end.Offset)]);
                                break;
                            }
                            else
                            {
                                content.Append(line[(index + start.Offset)..nextIndex]);
                            }
                        }
                        else if (i == end.Line)
                        {
                            content.Append(line[index..(index + end.Offset)]);
                            break;
                        }
                        else
                        {
                            content.Append(line[index..nextIndex]);
                        }
                    }

                    index = nextIndex;
                    nextIndex = line.IndexOf('\n', nextIndex) + 1;
                }
                currentLine = currentToken.End.Line;
                _tokenStream.MoveNextToken();
            }
            TokenStreamPosition = endPosition;
            debugSpecifier = AstNode.Constant(content.ToString(), metaInfo);
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
            {
                _context.RaiseSyntaxError("f-string: missing conversion character");
                throw new PyRuntimeException(_context.CurrentException);
            }
            if (CurrentToken.String is not ("s" or "r" or "a"))
            {
                _context.RaiseSyntaxError($"f-string: invalid conversion character '{CurrentToken.String}': expected 's', 'r', or 'a'");
                throw new PyRuntimeException(_context.CurrentException);
            }
            conversion = CurrentToken.String[0];
            MoveNextToken();
        }

        JoinedStrNode? format_spec = null;
        if (CurrentTokenType is TokenType.Colon)
            format_spec = ParseFStringFullFormatSpec();

        return new FormattedValueNode(fexpr, conversion, format_spec) { MetaInfo = fexpr.MetaInfo }; // TODO: MetaInfo
    }

    private AstExprNode ParseString()
    {
        Debug.Assert(CurrentTokenType is TokenType.String or TokenType.FStringStart);

        // ConstantNode or FormattedValueNode
        List<AstExprNode> nodes = [];
        bool hasFString = false;
        var startMetaInfo = CreateMetaInfo();
        var metaInfo = startMetaInfo;

        while (CurrentTokenType is TokenType.String or TokenType.FStringStart)
        {
            if (CurrentTokenType is TokenType.String)
            {
                var str = FromLiteralToString(_context, CurrentToken.String, false);
                var node = AstNode.Constant(str, CreateMetaInfo());
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
                        var str = FromLiteralToString(_context, CurrentToken.String, true);
                        var node = AstNode.Constant(str, CreateMetaInfo());
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

            metaInfo = CopyThenWithEnd(startMetaInfo);
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
                return AstNode.Constant(string.Empty, metaInfo);

            var node = combinedNodes[0];
            node.MetaInfo = metaInfo;
            return node;
        }

        return AstNode.JoinedStr(combinedNodes, metaInfo);

        void TryAppendCombinedConstantNode()
        {
            if (_builderForTokenString.Length is 0)
                return;

            var combinedNode = AstNode.Constant(_builderForTokenString.ToString(),
                null /* MetaInfo will be added after the combining is complete */ );
            combinedNodes.Add(combinedNode);
            _builderForTokenString.Clear();
        }


    }
    static string FromLiteralToString(PyCallContext context, string literal, bool hasWrapper)
    {
        // TODO: prefix 'b'

        bool successful;
        string? str;
        PyStrConverter.ConvertErrorInfo info;
        if (hasWrapper)
            successful = PyStrConverter.TryFromTextToString(literal, out str, out info);
        else
            successful = PyStrConverter.TryFromLiteralToString(literal, out str, out info);

        if (successful)
        {
            if (info.Error is PyStrConverter.ConvertError.InvalidEscapeSequence)
            {
                if (!context.TryWarn<PySyntaxWarningObjectType>($"invalid escape sequence '\\{info.Char}'"))
                    throw new PyRuntimeException(context.CurrentException);
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

            switch (info.Error)
            {
                case PyStrConverter.ConvertError.LowerXSequence:
                    throw PyCallContext.ThrowSyntaxError(MakeUnicodeErrorInfo("truncated \\xXX escape"));

                case PyStrConverter.ConvertError.LowerUSequence:
                    throw PyCallContext.ThrowSyntaxError(MakeUnicodeErrorInfo("truncated \\uXXXX escape"));

                case PyStrConverter.ConvertError.UpperUSequence:
                    throw PyCallContext.ThrowSyntaxError(MakeUnicodeErrorInfo("truncated \\UXXXXXXXX escape"));

                case PyStrConverter.ConvertError.SurrogatesNotAllowed:
                    throw PyCallContext.ThrowSyntaxError($"'utf-8' codec can't encode character '\\u{(uint)info.Char:x4}' in position {info.Position}: surrogates not allowed");

                case PyStrConverter.ConvertError.IllegalUnicodeCharacter:
                    throw PyCallContext.ThrowSyntaxError(MakeUnicodeErrorInfo("illegal Unicode character"));

                default:
                    throw new UnreachableException();
            }

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
    /// <exception cref="AstException"></exception>
    /// <exception cref="NotSupportedException"></exception>
    private AstExprNode ParseAtom()
    {
        var metaInfo = CreateMetaInfo();
        if (CurrentTokenType is TokenType.Name)
        {
            if (IsKeyword(CurrentToken.String))
            {
                // keyword literal

                if (CurrentToken.String is "True" or "False")
                {
                    var value = CurrentToken.String;
                    MoveNextToken();
                    return AstNode.Constant(bool.Parse(value), metaInfo);
                }
                else if (CurrentToken.String is "None")
                {
                    MoveNextToken();
                    return AstNode.Constant(PyNoneObject.None, metaInfo);
                }

                throw new AstException("invalid syntax");
            }
            else if (CurrentToken.String is PySpecialNames.Debug)
            {
                // __debug__

                MoveNextToken();
                return AstNode.Constant(_options.Debug, metaInfo);
            }
            else
            {
                // identifier

                var nameNode = AstNode.Name(ParseIdentifier(), metaInfo);
                CurrentScope.TryAddUnknown(nameNode.Identifier);
                CurrentScope.Track(nameNode);
                return nameNode;
            }
        }
        else if (CurrentTokenType is TokenType.String or TokenType.FStringStart)
        {
            return ParseString();
        }
        else if (CurrentTokenType is TokenType.Number)
        {
            // number literal
            // supports int (with prefix), float
            // complex is not supported currently

            var value = CurrentToken.String;
            MoveNextToken();
            value = value.Replace("_", string.Empty);

            if (value.StartsWith("0x") || value.StartsWith("0X"))
                return AstNode.Constant(Convert.ToInt64(value[2..], 16), metaInfo);

            if (value.StartsWith("0o") || value.StartsWith("0O"))
                return AstNode.Constant(Convert.ToInt64(value[2..], 8), metaInfo);

            if (value.StartsWith("0b") || value.StartsWith("0B"))
                return AstNode.Constant(Convert.ToInt64(value[2..], 2), metaInfo);

            if (BigInteger.TryParse(value, out var bigint))
                return AstNode.Constant(bigint, metaInfo);

            return AstNode.Constant(double.Parse(value), metaInfo);
        }
        else if (CurrentTokenType is TokenType.Ellipsis)
        {
            MoveNextToken();
            return AstNode.Constant(PyEllipsisObject.Ellipsis, metaInfo);
        }
        else if (CurrentTokenType is TokenType.LeftParen or TokenType.LeftSquareBracket or TokenType.LeftBrace)
        {
            return ParseEnclosure();
        }
        else
        {
            if (CurrentTokenType is TokenType.Indent)
            {
                _context.RaiseIndentationError("unexpected indent");
                throw new PyRuntimeException(_context.CurrentException);
            }

            throw new NotSupportedException();
        }

    }

    /// <summary>
    /// slice_item: <see cref="ParseExpression">expression</see> | proper_slice
    /// <br/> proper_slice: [lower_bound] ":" [upper_bound] [ ":" [stride] ]
    /// <br/> lower_bound: <see cref="ParseExpression">expression</see>
    /// <br/> upper_bound: <see cref="ParseExpression">expression</see>
    /// <br/> stride: <see cref="ParseExpression">expression</see>
    /// </summary>
    /// <returns></returns>
    private AstExprNode ParseSliceItem()
    {
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
            return AstNode.Slice(lowerBound, null, null);

        if (CurrentTokenType is TokenType.Colon)
        {
            upperBound = null;
        }
        else
        {
            upperBound = ParseExpression();

            // the slice item has 1 colon, optional lowerBound, upperBound, no stride
            if (CurrentTokenType is TokenType.Comma or TokenType.RightSquareBracket)
                return AstNode.Slice(lowerBound, upperBound, null);
        }

        // [lower_bound] ":" [upper_bound] ":" [stride]
        //                                  ^ we are here
        EnsureTokenTypeThenMove(TokenType.Colon);

        // the slice item has 2 colon, optional lowerBound, optional upperBound, no stride
        if (CurrentTokenType is TokenType.Comma or TokenType.RightSquareBracket)
            return AstNode.Slice(lowerBound, upperBound, null);

        // the slice item has 2 colon, optional lowerBound, optional upperBound, stride
        stride = ParseExpression();
        return AstNode.Slice(lowerBound, upperBound, stride);
    }

    /// <summary>
    /// slice_list: <see cref="ParseSliceItem">slice_item</see> ("," <see cref="ParseSliceItem">slice_item</see>)* [","]
    /// </summary>
    /// <returns></returns>
    private List<AstExprNode> ParseSliceList(out bool endsWithComma)
    {
        List<AstExprNode> list = [ParseSliceItem()];
        endsWithComma = false;
        while (CurrentTokenType is TokenType.Comma)
        {
            MoveNextToken();
            if (CurrentTokenType is TokenType.RightSquareBracket)
            {
                endsWithComma = true;
                break;
            }
            list.Add(ParseSliceItem());
        }
        return list;
    }

    /// <summary>
    /// await_expr: "await" <see cref="ParsePrimary">primary</see>
    /// </summary>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    private AstExprNode ParseAwaitExpr()
    {
        EnsureKeywordThenMove("await");
        var expr = ParsePrimary();
        _ = expr;
        throw new NotImplementedException();
    }

    /// <summary>
    /// primary: <see cref="ParseAtom">atom</see> | attributeref | subscription | slicing | call
    /// <br/> attributeref: primary "." <see cref="ParseIdentifier">identifier</see>
    /// <br/> subscription: primary "[" <see cref="ParseFlexibleExpressionList(StopPredicate, out bool)">flexible_expression_list</see> "]"
    /// <br/> slicing: primary "[" <see cref="ParseSliceList(out bool)">slice_list</see> "]"
    /// <br/> call: primary "(" [<see cref="ParseArgumentList">argument_list</see> | <see cref="ParseComprehension">comprehension</see>] ")"
    /// </summary>
    /// <returns></returns>
    /// <exception cref="AstException"></exception>
    private AstExprNode ParsePrimary()
    {
        var startMetaInfo = CreateMetaInfo();
        var expr = ParseAtom();
        while (CurrentTokenType is TokenType.LeftSquareBracket or TokenType.LeftParen or TokenType.Dot)
        {
            if (CurrentTokenType is TokenType.LeftSquareBracket)
            {
                var currentMetaInfo = CopyThenMarkCrucial(startMetaInfo);
                MoveNextToken();

                var index = TokenStreamPosition;
                try
                {
                    // try parse subscription
                    // lst[*expr1, expr2 := expr3]

                    var list = ParseFlexibleExpressionList(StopPredicates.UntilRightSquareBracket, out var endsWithComma);
                    expr = AstNode.Subscript(expr, UnwrapOrMakeTuple(list, endsWithComma), WithAllEnd(currentMetaInfo));
                }
                catch (AstException)
                {
                    // parse slicing
                    // lst[expr:expr, ::, expr]

                    TokenStreamPosition = index;
                    var sliceList = ParseSliceList(out var endsWithComma);
                    expr = AstNode.Subscript(expr, UnwrapOrMakeTuple(sliceList, endsWithComma), WithAllEnd(currentMetaInfo));
                }
                EnsureTokenTypeThenMove(TokenType.RightSquareBracket);
            }
            else if (CurrentTokenType is TokenType.LeftParen)
            {
                MoveNextToken();

                var index = TokenStreamPosition;
                try
                {
                    // primary "(" argument_list ")"

                    var (args, kwargs) = ParseArgumentList();
                    expr = AstNode.Call(expr, args, kwargs, CopyThenWithEnd(startMetaInfo));
                }
                catch (AstException)
                {
                    // primary "(" comprehension ")"

                    TokenStreamPosition = index;
                    var (elts, generators) = ParseComprehension();
                    expr = AstNode.Call(expr, [AstNode.GeneratorExp(elts, generators)], [], CopyThenWithEnd(startMetaInfo));
                }
                EnsureTokenTypeThenMove(TokenType.RightParen);
            }
            else if (CurrentTokenType is TokenType.Dot)
            {
                // primary.attr

                MoveNextToken();
                var metaInfo = CopyThenWithEnd(startMetaInfo);
                expr = AstNode.Attribute(expr, ParseIdentifier(), metaInfo);
            }
            else
            {
                Debug.Fail("unreachable");
            }
        }

        return expr;
    }

    /// <summary>
    /// power: (<see cref="ParseAwaitExpr">await_expr</see> | <see cref="ParsePrimary">primary</see>) ["**" <see cref="ParseUExpr">u_expr</see>]
    /// </summary>
    /// <returns></returns>
    private AstExprNode ParsePower()
    {
        var metaInfo = CreateMetaInfo();
        var expr = IsCurrentKeyword("await") ? ParseAwaitExpr() : ParsePrimary();

        if (CurrentTokenType is TokenType.DoubleStar)
        {
            MarkCrucialForOneToken(metaInfo);
            MoveNextToken();
            var uexpr = ParseUExpr();
            return AstNode.BinOp(PowNode.Shared, expr, uexpr, WithEndOfOtherNode(metaInfo, uexpr));
        }

        return expr;
    }

    /// <summary>
    /// u_expr: <see cref="ParsePower">power</see> | "-" u_expr | "+" u_expr | "~" u_expr
    /// </summary>
    /// <returns></returns>
    private AstExprNode ParseUExpr()
    {
        if (CurrentTokenType is TokenType.Minus or TokenType.Plus or TokenType.Tilde)
        {
            var metaInfo = CreateMetaInfo();
            AstUnaryOpNode? op = CurrentTokenType switch
            {
                TokenType.Minus => USubNode.Shared,
                TokenType.Plus => UAddNode.Shared,
                TokenType.Tilde => InvertNode.Shared,
                _ => null,
            };
            Debug.Assert(op is not null);
            MoveNextToken();
            var uexpr = ParseUExpr();
            return AstNode.UnaryOp(op, uexpr, WithEndOfOtherNode(metaInfo, uexpr));
        }

        return ParsePower();
    }

    /// <summary>
    /// m_expr: <see cref="ParseUExpr">u_expr</see> | m_expr "*" <see cref="ParseUExpr">u_expr</see> | m_expr "@" m_expr |
    ///         m_expr "//" <see cref="ParseUExpr">u_expr</see> | m_expr "/" <see cref="ParseUExpr">u_expr</see> |
    ///         m_expr "%" <see cref="ParseUExpr">u_expr</see>
    /// </summary>
    /// <returns></returns>
    private AstExprNode ParseMExpr()
    {
        var startMetaInfo = CreateMetaInfo();
        var left = ParseUExpr();

        while (CurrentTokenType is TokenType.Star or TokenType.Slash or TokenType.DoubleSlash or TokenType.Percent)
        {
            var currentMetaInfo = CopyThenMarkCrucialForOneToken(startMetaInfo);
            AstOperatorNode? op = CurrentTokenType switch
            {
                TokenType.Star => MulNode.Shared,
                TokenType.Slash => DivNode.Shared,
                TokenType.DoubleSlash => FloorDivNode.Shared,
                TokenType.Percent => ModNode.Shared,
                _ => null,
            };
            Debug.Assert(op is not null);
            MoveNextToken();
            var right = ParseMExpr();
            left = AstNode.BinOp(op, left, right, WithEndOfOtherNode(currentMetaInfo, right));
        }

        return left;
    }

    /// <summary>
    /// a_expr: <see cref="ParseMExpr">m_expr</see> | a_expr "+" <see cref="ParseMExpr">m_expr</see> | a_expr "-" <see cref="ParseMExpr">m_expr</see>
    /// </summary>
    /// <returns></returns>
    private AstExprNode ParseAExpr()
    {
        var startMetaInfo = CreateMetaInfo();
        var left = ParseMExpr();

        while (CurrentTokenType is TokenType.Plus or TokenType.Minus)
        {
            var currentMetaInfo = CopyThenMarkCrucialForOneToken(startMetaInfo);
            var add = CurrentTokenType is TokenType.Plus;
            MoveNextToken();
            var right = ParseMExpr();
            left = AstNode.BinOp(add ? AddNode.Shared : SubNode.Shared, left, right, WithEndOfOtherNode(currentMetaInfo, right));
        }

        return left;
    }

    /// <summary>
    /// shift_expr: <see cref="ParseAExpr">a_expr</see> | shift_expr ("&lt;&lt;" | "&gt;&gt;") <see cref="ParseAExpr">a_expr</see>
    /// </summary>
    /// <returns></returns>
    private AstExprNode ParseShiftExpr()
    {
        var startMetaInfo = CreateMetaInfo();
        var left = ParseAExpr();

        while (CurrentTokenType is TokenType.LeftShift or TokenType.RightShift)
        {
            var currentMetaInfo = CopyThenMarkCrucialForOneToken(startMetaInfo);
            var lshift = CurrentTokenType is TokenType.LeftShift;
            MoveNextToken();
            var right = ParseAExpr();
            left = AstNode.BinOp(lshift ? LShiftNode.Shared : RShiftNode.Shared, left, right, WithEndOfOtherNode(currentMetaInfo, right));
        }

        return left;
    }

    /// <summary>
    /// and_expr: <see cref="ParseShiftExpr">shift_expr</see> | and_expr "&" <see cref="ParseShiftExpr">shift_expr</see>
    /// </summary>
    /// <returns></returns>
    private AstExprNode ParseAndExpr()
    {
        var startMetaInfo = CreateMetaInfo();
        var andExpr = ParseShiftExpr();

        while (CurrentTokenType is TokenType.Ampersand)
        {
            var currentMetaInfo = CopyThenMarkCrucialForOneToken(startMetaInfo);
            MoveNextToken();
            var shiftExpr = ParseShiftExpr();
            andExpr = AstNode.BinOp(BitAndNode.Shared, andExpr, shiftExpr, WithEndOfOtherNode(currentMetaInfo, shiftExpr));
        }

        return andExpr;
    }

    /// <summary>
    /// xor_expr: <see cref="ParseAndExpr">and_expr</see> | xor_expr "^" <see cref="ParseAndExpr">and_expr</see>
    /// </summary>
    /// <returns></returns>
    private AstExprNode ParseXorExpr()
    {
        var startMetaInfo = CreateMetaInfo();
        var xorExpr = ParseAndExpr();

        while (CurrentTokenType is TokenType.Caret)
        {
            var currentMetaInfo = CopyThenMarkCrucialForOneToken(startMetaInfo);
            MoveNextToken();
            var andExpr = ParseAndExpr();
            xorExpr = AstNode.BinOp(BitXorNode.Shared, xorExpr, andExpr, WithEndOfOtherNode(currentMetaInfo, andExpr));
        }

        return xorExpr;
    }

    /// <summary>
    /// or_expr: <see cref="ParseXorExpr">xor_expr</see> | or_expr "|" <see cref="ParseXorExpr">xor_expr</see>
    /// </summary>
    /// <returns></returns>
    private AstExprNode ParseOrExpr()
    {
        var startMetaInfo = CreateMetaInfo();
        var orExpr = ParseXorExpr();

        while (CurrentTokenType is TokenType.Pipe)
        {
            var currentMetaInfo = CopyThenMarkCrucialForOneToken(startMetaInfo);
            MoveNextToken();
            var xorExpr = ParseXorExpr();
            orExpr = AstNode.BinOp(BitOrNode.Shared, orExpr, xorExpr, WithEndOfOtherNode(currentMetaInfo, xorExpr));
        }

        return orExpr;
    }

    /// <summary>
    /// comparison: <see cref="ParseOrExpr">or_expr</see> (comp_operator <see cref="ParseOrExpr">or_expr</see>)*
    /// <br/> comp_operator: "&lt;" | "&gt;" | "==" | "&gt;=" | "&lt;=" | "!=" | "is" ["not"] | ["not"] "in"
    /// </summary>
    /// <returns></returns>
    private AstExprNode ParseComparison()
    {
        var expr = ParseOrExpr();
        var ops = new List<AstCmpopNode>();
        var comptors = new List<AstExprNode>();

        while (true)
        {
            if (IsCurrentKeyword("is"))
            {
                MoveNextToken();
                if (IsCurrentKeyword("not"))
                {
                    MoveNextToken();
                    ops.Add(IsNotNode.Shared);
                }
                else
                {
                    ops.Add(IsNode.Shared);
                }
            }
            else if (IsCurrentKeyword("in"))
            {
                MoveNextToken();
                ops.Add(InNode.Shared);

            }
            else if (IsCurrentKeyword("not"))
            {
                MoveNextToken();
                EnsureKeywordThenMove("in");
                ops.Add(NotInNode.Shared);
            }
            else if (CurrentTokenType is TokenType.Less)
            {
                MoveNextToken();
                ops.Add(LtNode.Shared);
            }
            else if (CurrentTokenType is TokenType.LessEqual)
            {
                MoveNextToken();
                ops.Add(LtENode.Shared);
            }
            else if (CurrentTokenType is TokenType.Greater)
            {
                MoveNextToken();
                ops.Add(GtNode.Shared);
            }
            else if (CurrentTokenType is TokenType.GreaterEqual)
            {
                MoveNextToken();
                ops.Add(GtENode.Shared);
            }
            else if (CurrentTokenType is TokenType.DoubleEqual)
            {
                MoveNextToken();
                ops.Add(EqNode.Shared);
            }
            else if (CurrentTokenType is TokenType.NotEqual)
            {
                MoveNextToken();
                ops.Add(NotEqNode.Shared);
            }
            else
            {
                break;
            }

            var other = ParseOrExpr();
            comptors.Add(other);
        }

        if (ops.Count > 0)
            return AstNode.Compare(expr, ops.Zip(comptors));

        return expr;
    }

    /// <summary>
    /// not_test: <see cref="ParseComparison">comparison</see> | "not" not_test
    /// </summary>
    /// <returns></returns>
    private AstExprNode ParseNotTest()
    {
        if (CurrentTokenType is TokenType.Name && CurrentToken.String is "not")
        {
            MoveNextToken();
            return AstNode.UnaryOp(NotNode.Shared, ParseNotTest(), null /* the operator 'not' does not need MetaInfo */);
        }
        return ParseComparison();
    }

    /// <summary>
    /// and_test: <see cref="ParseNotTest">not_test</see> | and_test "and" <see cref="ParseNotTest">not_test</see>
    /// </summary>
    /// <returns></returns>
    private AstExprNode ParseAndTest()
    {
        var result = ParseNotTest();

        if (CurrentTokenType is TokenType.Name && CurrentToken.String is "and")
        {
            List<AstExprNode> values = [result];
            values.Add(result);
            while (CurrentTokenType is TokenType.Name && CurrentToken.String is "and")
            {
                MoveNextToken();
                values.Add(ParseNotTest());
            }
            result = AstNode.BoolAnd(values);
        }
        return result;
    }

    /// <summary>
    /// or_test: <see cref="ParseAndTest">and_test</see> | or_test "or" <see cref="ParseAndTest">and_test</see>
    /// </summary>
    /// <returns></returns>
    private AstExprNode ParseOrTest()
    {
        var result = ParseAndTest();

        if (CurrentTokenType is TokenType.Name && CurrentToken.String is "or")
        {
            List<AstExprNode> values = [result];
            while (CurrentTokenType is TokenType.Name && CurrentToken.String is "or")
            {
                MoveNextToken();
                values.Add(ParseAndTest());
            }
            result = AstNode.BoolOr(values);
        }
        return result;
    }

    /// <summary>
    /// conditional_expression: <see cref="ParseOrTest">or_test</see> ["if" <see cref="ParseOrTest">or_test</see> "else" <see cref="ParseExpression">expression</see>]
    /// </summary>
    /// <returns></returns>
    private AstExprNode ParseConditionalExpression()
    {
        var body = ParseOrTest();
        if (IsCurrentKeyword("if"))
        {
            MoveNextToken();
            var test = ParseOrTest();
            EnsureKeywordThenMove("else");
            var orelse = ParseExpression();
            return new IfExpNode(test, body, orelse);
        }
        return body;
    }

    /// <summary>
    /// lambda_expr: "lambda" [<see cref="ParseParameterList(StopPredicate)">parameter_list</see>] ":" <see cref="ParseExpression">expression</see>
    /// </summary>
    /// <returns></returns>
    private LambdaNode ParseLambdaExpr()
    {
        var metaInfo = CreateMetaInfo();
        EnsureKeywordThenMove("lambda");

        AstArgumentsNode args;
        if (CurrentTokenType is TokenType.Colon)
        {
            args = new AstArgumentsNode();
        }
        else
        {
            args = ParseParameterList(StopPredicates.UntilColon);
        }

        EnsureTokenTypeThenMove(TokenType.Colon);
        var lambdaNode = new LambdaNode(args);
        StartParsingLambda();
        lambdaNode.Body = ParseExpression();
        EndParsingLambda();
        lambdaNode.MetaInfo = metaInfo;
        return lambdaNode;

        void StartParsingLambda()
        {
            Context.EnterScope(lambdaNode);
            CurrentScope.AddParameters(args);
        }
        void EndParsingLambda()
        {
            var scope = Context.ExitScope();
            FillLocalVariables(scope);
        }
    }

    /// <summary>
    /// expression: <see cref="ParseConditionalExpression">conditional_expression</see> | <see cref="ParseLambdaExpr">lambda_expr</see>
    /// </summary>
    /// <returns></returns>
    private AstExprNode ParseExpression()
    {
        // lambda_expr
        if (IsCurrentKeyword("lambda"))
            return ParseLambdaExpr();

        // conditional_expression
        var expr = ParseConditionalExpression();
        return expr;
    }

    /// <summary>
    /// assignment_expression: [<see cref="ParseIdentifier">identifier</see> ":="] <see cref="ParameterExpression">expression</see>
    /// </summary>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    private AstExprNode ParseAssignmentExpression()
    {
        var expr = ParseExpression();
        if (expr is not NameNode || CurrentTokenType is not TokenType.ColonEqual)
            return expr;

        throw new NotImplementedException();
    }

    /// <summary>
    /// starred_expression: "*" <see cref="ParseOrTest">or_expr</see> | <see cref="ParseExpression">expression</see>
    /// </summary>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    private AstExprNode ParseStarredExpression()
    {
        if (CurrentTokenType is TokenType.Star)
            throw new NotImplementedException();

        return ParseOrExpr();
    }

    private List<AstExprNode> ParseStarredExpressionList(StopPredicate predicate, out bool endsWithComma)
    {
        endsWithComma = false;
        List<AstExprNode> list = [ParseStarredExpression()];
        while (CurrentTokenType is TokenType.Comma)
        {
            MoveNextToken();

            if (predicate(CurrentToken))
            {
                endsWithComma = true;
                break;
            }
            list.Add(ParseStarredExpression());
        }
        return list;
    }

    /// <summary>
    /// flexible_expression: <see cref="ParseAssignmentExpression">assignment_expression</see> | <see cref="ParseStarredExpression">starred_expression</see>
    /// </summary>
    /// <returns></returns>
    private AstExprNode ParseFlexibleExpression()
    {
        if (CurrentTokenType is TokenType.Star)
            return ParseStarredExpression();

        return ParseAssignmentExpression();
    }

    /// <summary>
    /// flexible_expression_list: <see cref="ParseFlexibleExpression">flexible_expression</see> ("," <see cref="ParseFlexibleExpression">flexible_expression</see>)* [","]
    /// </summary>
    /// <param name="predicate"></param>
    /// <returns></returns>
    /// <param name="endsWithComma"></param>
    private List<AstExprNode> ParseFlexibleExpressionList(StopPredicate predicate, out bool endsWithComma)
    {
        endsWithComma = false;
        List<AstExprNode> list = [ParseFlexibleExpression()];
        while (CurrentTokenType is TokenType.Comma)
        {
            MoveNextToken();

            if (predicate(CurrentToken))
            {
                endsWithComma = true;
                break;
            }
            list.Add(ParseFlexibleExpression());
        }
        return list;
    }

    /// <summary>
    /// [expr] => Unwrap
    /// <br/> [expr, ] => MakeTuple
    /// <br/> [expr, expr] => MakeTuple
    /// </summary>
    /// <param name="list"></param>
    /// <param name="endsWithComma"></param>
    /// <returns></returns>
    private static AstExprNode UnwrapOrMakeTuple(List<AstExprNode> list, bool endsWithComma)
    {
        if (list.Count is 1 && !endsWithComma)
            return list[0];
        return AstNode.Tuple(list);
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
    /// <exception cref="AstException"></exception>
    private AstExprNode ParseTarget()
    {
        if (CurrentTokenType is TokenType.Star)
        {
            _ = ParseTarget();
            throw new NotSupportedException();
        }

        var target = ParsePrimary();
        if (!IsValidTarget(target))
            throw new AstException("invalid target in assignment");
        return target;
    }

    /// <summary>
    /// target_list: <see cref="ParseTarget">target</see> ("," <see cref="ParseTarget">target</see>)* [","]
    /// </summary>
    /// <param name="predicate"></param>
    /// <param name="endsWithComma"></param>
    /// <returns></returns>
    private List<AstExprNode> ParseTargetList(StopPredicate predicate, out bool endsWithComma)
    {
        endsWithComma = false;
        List<AstExprNode> list = [ParseTarget()];
        while (CurrentTokenType is TokenType.Comma)
        {
            MoveNextToken();
            if (predicate(CurrentToken))
            {
                endsWithComma = true;
                break;
            }
            list.Add(ParseTarget());
        }
        return list;
    }

    //private List<AstExprNode> ParseTargetListUntilTokens(params ReadOnlySpan<TokenType> stopTokens)
    //{
    //    List<AstExprNode> list = [ParseTarget()];
    //    while (CurrentTokenType is TokenType.Comma)
    //    {
    //        MoveNextToken();
    //        if (stopTokens.Contains(CurrentTokenType))
    //            break;
    //        list.Add(ParseTarget());
    //    }
    //    return list;
    //}

    /// <summary>
    /// comp_for: ["async"] "for" <see cref="ParseTargetList(StopPredicate, out bool)">target_list</see> "in" <see cref="ParseOrTest">or_test</see> [comp_iter]
    /// <br/> comp_iter: comp_for | comp_if
    /// <br/> comp_if: "if" <see cref="ParseOrTest">or_test</see> [comp_iter]
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
            var iter = ParseOrTest();
            var ifs = new List<AstExprNode>();
            while (IsCurrentKeyword("if"))
            {
                MoveNextToken();
                ifs.Add(ParseOrTest());
            }

            return AstNode.Comprehension(target, iter, ifs);
        }
    }

    /// <summary>
    /// comprehension: <see cref="ParseAssignmentExpression">assignment_expression</see> <see cref="ParseCompFor">comp_for</see>
    /// </summary>
    /// <returns></returns>
    private (AstExprNode Elt, List<AstComprehensionNode> Generators) ParseComprehension()
    {
        _comprehensionDepth++;
        var elt = ParseAssignmentExpression();
        var generators = ParseCompFor();
        _comprehensionDepth--;
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
            _ = ParseAssignmentExpression();
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
        EnsureTokenTypeThenMove(TokenType.LeftSquareBracket);

        if (CurrentTokenType is TokenType.RightSquareBracket)
        {
            MoveNextToken();
            return AstNode.List();
        }

        if (TestIsComprehension())
        {
            var (elt, generators) = ParseComprehension();
            EnsureTokenTypeThenMove(TokenType.RightSquareBracket);
            return AstNode.ListComp(elt, generators);
        }

        var list = AstNode.List(ParseFlexibleExpressionList(StopPredicates.UntilRightSquareBracket, out _));
        EnsureTokenTypeThenMove(TokenType.RightSquareBracket);
        return list;
    }

    /// <summary>
    /// set_display: "{" (<see cref="ParseFlexibleExpressionList(StopPredicate, out bool)">flexible_expression_list</see> | <see cref="ParseComprehension">comprehension</see>) "}"
    /// </summary>
    /// <returns></returns>
    private AstExprNode ParseSetDisplay()
    {
        EnsureTokenTypeThenMove(TokenType.LeftBrace);

        if (TestIsComprehension())
        {
            var (elt, generators) = ParseComprehension();
            EnsureTokenTypeThenMove(TokenType.RightBrace);
            return AstNode.SetComp(elt, generators);
        }

        var set = AstNode.Set(ParseFlexibleExpressionList(StopPredicates.UntilRightBrace, out _));
        EnsureTokenTypeThenMove(TokenType.RightBrace);
        return set;
    }

    /// <summary>
    /// dict_item: <see cref="ParseExpression">expression</see> ":" <see cref="ParseExpression">expression</see> | "**" <see cref="ParseOrExpr">or_expr</see>
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
        EnsureTokenTypeThenMove(TokenType.LeftBrace);

        if (CurrentTokenType is TokenType.RightBrace)
        {
            MoveNextToken();
            return AstNode.Dict();
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
            return AstNode.DictComp(key, value, generators);
        }

        List<KeyValuePair<AstExprNode, AstExprNode>> pairs = [];
        var list = ParseDictItemList();
        foreach (var (key, value) in list)
        {
            pairs.Add(KeyValuePair.Create(key, value));
        }
        EnsureTokenTypeThenMove(TokenType.RightBrace);
        return AstNode.Dict(pairs);
    }

    /// <summary>
    /// generator_expression: "(" <see cref="ParseComprehension">comprehension</see> ")"
    /// </summary>
    /// <returns></returns>
    private GeneratorExpNode ParseGeneratorExpression()
    {
        // here is no need to EnsureTokenTypeThenMove(TokenType.LeftParen)
        // because TokenType.LeftParen is consumed

        var (elt, generators) = ParseComprehension();
        EnsureTokenTypeThenMove(TokenType.RightParen);
        return AstNode.GeneratorExp(elt, generators);
    }

    private AstArgumentsNode ParseParameterList(StopPredicate predicate)
    {
        const int StateArgs = 0, StateAfterPosonly = 1, StateKwonly = 3, StateEnd = 4;

        var arguments = new AstArgumentsNode();
        var state = StateArgs;
        var needDefault = false;
        var names = new HashSet<string>();

        ParseParameter();
        while (CurrentTokenType is TokenType.Comma)
        {
            MoveNextToken();
            if (predicate(CurrentToken))
                break;
            ParseParameter();
        }
        return arguments;

        void ParseParameter()
        {
            if (state is StateEnd)
                throw new AstException("should be no more args");

            switch (CurrentTokenType)
            {
                case TokenType.Slash:
                    if (state is not StateArgs)
                        throw new AstException("should not be '/'");

                    MoveNextToken();
                    arguments.PosonlyArgs.AddRange(arguments.Args);
                    arguments.Args.Clear();
                    state = StateAfterPosonly;
                    break;

                case TokenType.Star:
                    if (state is StateKwonly)
                        throw new AstException("should not be '*'");

                    MoveNextToken();
                    if (CurrentTokenType is TokenType.Name)
                        arguments.VarArg = new AstArgNode(ParseIdentifier());
                    state = StateKwonly;
                    needDefault = false;
                    break;

                case TokenType.DoubleStar:
                    MoveNextToken();
                    if (CurrentTokenType is TokenType.Name)
                        arguments.KwArg = new AstArgNode(ParseIdentifier());
                    state = StateEnd;
                    break;

                case TokenType.Name:
                    if (CurrentTokenType is TokenType.Name)
                    {
                        var arg = new AstArgNode(ParseIdentifier());
                        if (names.Contains(arg.Arg))
                            throw new AstException($"duplicate argument '{arg.Arg}' in function definition");
                        else
                            names.Add(arg.Arg);
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
                            throw new AstException("need default value");
                        }
                        else
                        {
                            defaultValue = null;
                        }

                        if (state is StateArgs or StateAfterPosonly)
                        {
                            arguments.Args.Add(arg);
                            if (defaultValue is not null)
                                arguments.Defaults.Add(defaultValue);
                        }
                        else
                        {
                            arguments.KwonlyArgs.Add(arg);
                            arguments.KwDefaults.Add(defaultValue);
                        }
                    }
                    break;

                default:
                    if (state is StateArgs)
                        throw new AstException("should be id, '/' or '*'");
                    else if (state is StateAfterPosonly)
                        throw new AstException("should be id or '*'");
                    else
                        throw new AstException("should be id");
            }
        }
    }

    /// <summary>
    /// expression_list: <see cref="ParseExpression">expression</see> ("," <see cref="ParseExpression">expression</see>)* [","]
    /// </summary>
    /// <param name="endsWithComma"></param>
    /// <param name="stopTokens"></param>
    /// <returns></returns>
    private List<AstExprNode> ParseExpressionList(StopPredicate predicate, out bool endsWithComma)
    {
        endsWithComma = false;
        List<AstExprNode> list = [ParseExpression()];
        while (CurrentTokenType is TokenType.Comma)
        {
            MoveNextToken();
            if (predicate(CurrentToken))
            {
                endsWithComma = true;
                break;
            }
            list.Add(ParseExpression());
        }
        return list;
    }

    /// <summary>
    /// parenth_form: "(" [<see cref="ParseFlexibleExpressionList(StopPredicate, out bool)">flexible_expression_list</see>] ")"
    /// </summary>
    /// <returns></returns>
    private AstExprNode ParseParenthForm()
    {
        // here is no need to EnsureTokenTypeThenMove(TokenType.LeftParen)
        // because TokenType.LeftParen is consumed

        // () is an empty tuple
        if (CurrentTokenType is TokenType.RightParen)
        {
            MoveNextToken();
            return AstNode.Tuple();
        }

        var list = ParseFlexibleExpressionList(StopPredicates.UntilRightParen, out var endsWithComma);
        EnsureTokenTypeThenMove(TokenType.RightParen);
        return UnwrapOrMakeTuple(list, endsWithComma);
    }

    /// <summary>
    /// yield_atom: "(" yield_expression ")"
    /// </summary>
    /// <returns></returns>
    private AstExprNode ParseYieldAtom()
    {
        // TokenType.LeftParen should be consumed
        var yieldExpr = ParseYieldExpression();
        EnsureTokenTypeThenMove(TokenType.RightParen);
        return yieldExpr;
    }

    private YieldFromNode ParseYieldFrom()
    {
        EnsureKeywordThenMove("from");
        return new YieldFromNode(ParseExpression());
    }

    private AstExprNode ParseYieldExpression()
    {
        if (!CurrentScope.IsCurrentFuncDefOrLambda)
            throw new AstException("'yield' outside function");

        if (_comprehensionDepth > 0)
            throw new AstException("'yield' inside comprehension" /* TODO: a more specific name like: generator expression */);

        ((IFunctionOrLambda)CurrentScope.Owner!).HasYield = true;
        EnsureKeywordThenMove("yield");
        if (IsCurrentKeyword("from"))
            return ParseYieldFrom();
        if (StopPredicates.UntilRightParenOrNewLineOrSemicolon(CurrentToken))
            return new YieldNode(null);
        var list = ParseStarredExpressionList(StopPredicates.UntilRightParenOrNewLineOrSemicolon, out var endsWithComma);
        return new YieldNode(UnwrapOrMakeTuple(list, endsWithComma));
    }

    /// <summary>
    /// argument_list: [positional_arguments ["," keywords_arguments] [","] | keywords_arguments [","]]
    /// <br/> positional_arguments: <see cref="ParseExpression">expression</see> ("," <see cref="ParseExpression">expression</see>)*
    /// <br/> keywords_arguments: keyword_item ("," keyword_item)*
    /// <br/> keyword_item: <see cref="ParseIdentifier">identifier</see> "=" <see cref="ParseExpression">expression</see>
    /// </summary>
    /// <returns></returns>
    /// <exception cref="AstException"></exception>
    private (List<AstExprNode> Args, List<AstKeywordNode> Kwargs) ParseArgumentList()
    {
        var args = new List<AstExprNode>();
        var kwargs = new List<AstKeywordNode>();
        var keys = new HashSet<string>();
        bool iskw = false;

        while (CurrentTokenType is not TokenType.RightParen)
        {
            var arg = ParseExpression();
            if (CurrentTokenType is TokenType.Equal)
            {
                iskw = true;

                if (arg is not NameNode argName)
                    throw new AstException("expression cannot contain assignment, perhaps you meant \"==\"?");

                if (keys.Contains(argName.Identifier))
                    throw new AstException($"keyword argument repeated: {argName.Identifier}");
                else
                    keys.Add(argName.Identifier);

                MoveNextToken();
                var value = ParseExpression();

                kwargs.Add(AstNode.Keyword(argName.Identifier, value));
            }
            else if (iskw)
            {
                throw new AstException("positional argument follows keyword argument");
            }
            else
            {
                args.Add(arg);
            }

            if (CurrentTokenType is TokenType.Comma)
                MoveNextToken();
            else if (CurrentTokenType is not TokenType.RightParen)
                throw new AstException("'(' was never closed");
        }

        return (args, kwargs);
    }
}
