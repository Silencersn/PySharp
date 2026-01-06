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
                TokenStreamPosition = index;
                return ParseYieldAtom();
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
            return ParseYieldExpression();

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
                var str = FromLiteralToString(_context, CurrentToken.String, true);
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

            if (CurrentToken.String is not ("s" or "r" or "a"))
                throw _context.ThrowableSyntaxError($"f-string: invalid conversion character '{CurrentToken.String}': expected 's', 'r', or 'a'");

            conversion = CurrentToken.String[0];
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
                var str = FromLiteralToString(_context, CurrentToken.String, false);
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
                        var str = FromLiteralToString(_context, CurrentToken.String, true);
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
    static string FromLiteralToString(PyCallContext context, string literal, bool nonWrapper)
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
            // number literal
            // supports int (with prefix), float
            // complex is not supported currently

            var value = CurrentToken.String;
            MoveNextToken();

            if (PyIntObjectType.TryParse(value, 0, out var integer))
                return Ast.Constant(integer).With(metaInfo);

            value = value.Replace("_", string.Empty);
            return Ast.Constant(double.Parse(value)).With(metaInfo);
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

    /// <summary>
    /// slice_list: <see cref="ParseSliceItem">slice_item</see> ("," <see cref="ParseSliceItem">slice_item</see>)* [","]
    /// </summary>
    /// <returns></returns>
    private List<AstExprNode> ParseSliceList(out TokenInfo? endsWithComma)
    {
        return ParseSomeExpressionList(ParseSliceItem, StopPredicates.UntilRightSquareBracket, out endsWithComma);
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
    /// <exception cref="PyRuntimeException"></exception>
    private AstExprNode ParsePrimary()
    {
        var startMetaInfo = CreateAstMetaInfo();
        var expr = ParseAtom();
        while (CurrentTokenType is TokenType.LeftSquareBracket or TokenType.LeftParen or TokenType.Dot)
        {
            if (CurrentTokenType is TokenType.LeftSquareBracket)
            {
                var currentMetaInfo = startMetaInfo.WithCrucial();
                MoveNextToken();

                var index = TokenStreamPosition;
                AstExprNode nextExpr;
                try
                {
                    // try parse subscription
                    // lst[*expr1, expr2 := expr3]

                    var list = ParseFlexibleExpressionList(StopPredicates.UntilRightSquareBracket, out var endsWithComma);
                    nextExpr = Ast.Subscript(expr, UnwrapOrMakeTuple(list, endsWithComma)).With(currentMetaInfo.WithAllEnd());
                    EnsureTokenTypeThenMove(TokenType.RightSquareBracket);
                }
                catch (PyRuntimeException)
                {
                    // parse slicing
                    // lst[expr:expr, ::, expr]

                    TokenStreamPosition = index;
                    var sliceList = ParseSliceList(out var endsWithComma);
                    nextExpr = Ast.Subscript(expr, UnwrapOrMakeTuple(sliceList, endsWithComma)).With(currentMetaInfo.WithAllEnd());
                    EnsureTokenTypeThenMove(TokenType.RightSquareBracket);
                }
                expr = nextExpr;
            }
            else if (CurrentTokenType is TokenType.LeftParen)
            {
                MoveNextToken();

                var index = TokenStreamPosition;
                try
                {
                    // primary "(" argument_list ")"

                    var (args, kwargs) = ParseArgumentList();
                    expr = Ast.Call(expr, args, kwargs).With(startMetaInfo.WithEnd());
                }
                catch (PyRuntimeException)
                {
                    // primary "(" comprehension ")"

                    TokenStreamPosition = index;
                    var metaInfo = CreateAstMetaInfo();
                    var (elts, generators) = ParseComprehension();
                    expr = Ast.Call(expr, [Ast.GeneratorExp(elts, generators).With(metaInfo.WithPreviousEnd())], []).With(startMetaInfo.WithEnd());
                }
                EnsureTokenTypeThenMove(TokenType.RightParen);
            }
            else if (CurrentTokenType is TokenType.Dot)
            {
                // primary.attr

                MoveNextToken();
                expr = Ast.Attribute(expr, ParseIdentifier()).With(startMetaInfo.WithPreviousEnd());
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
        var metaInfo = CreateAstMetaInfo();
        var expr = IsCurrentKeyword("await") ? ParseAwaitExpr() : ParsePrimary();

        if (CurrentTokenType is TokenType.DoubleStar)
        {
            metaInfo = metaInfo.WithCrucial();
            MoveNextToken();
            var uexpr = ParseUExpr();
            return Ast.BinOp(OperatorType.Pow, expr, uexpr).With(metaInfo.WithPreviousEnd());
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
            var metaInfo = CreateAstMetaInfo();
            UnaryOpType op = CurrentTokenType switch
            {
                TokenType.Minus => UnaryOpType.USub,
                TokenType.Plus => UnaryOpType.UAdd,
                TokenType.Tilde => UnaryOpType.Invert,
                _ => throw new UnreachableException(),
            };
            MoveNextToken();
            var uexpr = ParseUExpr();
            return Ast.UnaryOp(op, uexpr).With(metaInfo.WithPreviousEnd());
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
        var startMetaInfo = CreateAstMetaInfo();
        var left = ParseUExpr();

        while (CurrentTokenType is TokenType.Star or TokenType.Slash or TokenType.DoubleSlash or TokenType.Percent)
        {
            var currentMetaInfo = startMetaInfo.WithCrucial();
            OperatorType op = CurrentTokenType switch
            {
                TokenType.Star => OperatorType.Mult,
                TokenType.Slash => OperatorType.Div,
                TokenType.DoubleSlash => OperatorType.FloorDiv,
                TokenType.Percent => OperatorType.Mod,
                _ => throw new UnreachableException(),
            };
            MoveNextToken();
            var right = ParseMExpr();
            left = Ast.BinOp(op, left, right).With(currentMetaInfo.WithPreviousEnd());
        }

        return left;
    }

    /// <summary>
    /// a_expr: <see cref="ParseMExpr">m_expr</see> | a_expr "+" <see cref="ParseMExpr">m_expr</see> | a_expr "-" <see cref="ParseMExpr">m_expr</see>
    /// </summary>
    /// <returns></returns>
    private AstExprNode ParseAExpr()
    {
        var startMetaInfo = CreateAstMetaInfo();
        var left = ParseMExpr();

        while (CurrentTokenType is TokenType.Plus or TokenType.Minus)
        {
            var currentMetaInfo = startMetaInfo.WithCrucial();
            var add = CurrentTokenType is TokenType.Plus;
            MoveNextToken();
            var right = ParseMExpr();
            left = add ? Ast.Add(left, right) : Ast.Sub(left, right);
            left.With(currentMetaInfo.WithPreviousEnd());
        }

        return left;
    }

    /// <summary>
    /// shift_expr: <see cref="ParseAExpr">a_expr</see> | shift_expr ("&lt;&lt;" | "&gt;&gt;") <see cref="ParseAExpr">a_expr</see>
    /// </summary>
    /// <returns></returns>
    private AstExprNode ParseShiftExpr()
    {
        var startMetaInfo = CreateAstMetaInfo();
        var left = ParseAExpr();

        while (CurrentTokenType is TokenType.LeftShift or TokenType.RightShift)
        {
            var currentMetaInfo = startMetaInfo.WithCrucial();
            var lshift = CurrentTokenType is TokenType.LeftShift;
            MoveNextToken();
            var right = ParseAExpr();
            left = lshift ? Ast.LShift(left, right) : Ast.RShift(left, right);
            left.With(currentMetaInfo.WithPreviousEnd());
        }

        return left;
    }

    /// <summary>
    /// and_expr: <see cref="ParseShiftExpr">shift_expr</see> | and_expr "&" <see cref="ParseShiftExpr">shift_expr</see>
    /// </summary>
    /// <returns></returns>
    private AstExprNode ParseAndExpr()
    {
        var startMetaInfo = CreateAstMetaInfo();
        var andExpr = ParseShiftExpr();

        while (CurrentTokenType is TokenType.Ampersand)
        {
            var currentMetaInfo = startMetaInfo.WithCrucial();
            MoveNextToken();
            var shiftExpr = ParseShiftExpr();
            andExpr = Ast.BitAnd(andExpr, shiftExpr).With(currentMetaInfo.WithPreviousEnd());
        }

        return andExpr;
    }

    /// <summary>
    /// xor_expr: <see cref="ParseAndExpr">and_expr</see> | xor_expr "^" <see cref="ParseAndExpr">and_expr</see>
    /// </summary>
    /// <returns></returns>
    private AstExprNode ParseXorExpr()
    {
        var startMetaInfo = CreateAstMetaInfo();
        var xorExpr = ParseAndExpr();

        while (CurrentTokenType is TokenType.Caret)
        {
            var currentMetaInfo = startMetaInfo.WithCrucial();
            MoveNextToken();
            var andExpr = ParseAndExpr();
            xorExpr = Ast.BitXor(xorExpr, andExpr).With(currentMetaInfo.WithPreviousEnd());
        }

        return xorExpr;
    }

    /// <summary>
    /// or_expr: <see cref="ParseXorExpr">xor_expr</see> | or_expr "|" <see cref="ParseXorExpr">xor_expr</see>
    /// </summary>
    /// <returns></returns>
    private AstExprNode ParseOrExpr()
    {
        var startMetaInfo = CreateAstMetaInfo();
        var orExpr = ParseXorExpr();

        while (CurrentTokenType is TokenType.Pipe)
        {
            var currentMetaInfo = startMetaInfo.WithCrucial();
            MoveNextToken();
            var xorExpr = ParseXorExpr();
            orExpr = Ast.BitOr(orExpr, xorExpr).With(currentMetaInfo.WithPreviousEnd());
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
        var metaInfo = CreateAstMetaInfo();
        var expr = ParseOrExpr();
        var ops = new List<CmpopType>();
        var comptors = new List<AstExprNode>();

        while (true)
        {
            if (IsCurrentKeyword("is"))
            {
                MoveNextToken();
                if (IsCurrentKeyword("not"))
                {
                    MoveNextToken();
                    ops.Add(CmpopType.IsNot);
                }
                else
                {
                    ops.Add(CmpopType.Is);
                }
            }
            else if (IsCurrentKeyword("in"))
            {
                MoveNextToken();
                ops.Add(CmpopType.In);

            }
            else if (IsCurrentKeyword("not"))
            {
                MoveNextToken();
                EnsureKeywordThenMove("in");
                ops.Add(CmpopType.NotIn);
            }
            else if (CurrentTokenType is TokenType.Less)
            {
                MoveNextToken();
                ops.Add(CmpopType.Lt);
            }
            else if (CurrentTokenType is TokenType.LessEqual)
            {
                MoveNextToken();
                ops.Add(CmpopType.LtE);
            }
            else if (CurrentTokenType is TokenType.Greater)
            {
                MoveNextToken();
                ops.Add(CmpopType.Gt);
            }
            else if (CurrentTokenType is TokenType.GreaterEqual)
            {
                MoveNextToken();
                ops.Add(CmpopType.GtE);
            }
            else if (CurrentTokenType is TokenType.DoubleEqual)
            {
                MoveNextToken();
                ops.Add(CmpopType.Eq);
            }
            else if (CurrentTokenType is TokenType.NotEqual)
            {
                MoveNextToken();
                ops.Add(CmpopType.NotEq);
            }
            else
            {
                break;
            }

            var other = ParseOrExpr();
            comptors.Add(other);
        }

        if (ops.Count > 0)
            return Ast.Compare(expr, ops.Zip(comptors)).With(metaInfo.WithPreviousEnd());

        return expr;
    }

    /// <summary>
    /// not_test: <see cref="ParseComparison">comparison</see> | "not" not_test
    /// </summary>
    /// <returns></returns>
    private AstExprNode ParseNotTest()
    {
        if (!IsCurrentKeyword("not"))
            return ParseComparison();

        var metaInfo = CreateAstMetaInfo();
        MoveNextToken();
        return Ast.Not(ParseNotTest()).With(metaInfo.WithPreviousEnd());
    }

    /// <summary>
    /// and_test: <see cref="ParseNotTest">not_test</see> | and_test "and" <see cref="ParseNotTest">not_test</see>
    /// </summary>
    /// <returns></returns>
    private AstExprNode ParseAndTest()
    {
        var metaInfo = CreateAstMetaInfo();
        var result = ParseNotTest();

        if (IsCurrentKeyword("and"))
        {
            List<AstExprNode> values = [result];
            while (IsCurrentKeyword("and"))
            {
                MoveNextToken();
                values.Add(ParseNotTest());
            }
            result = Ast.And(values).With(metaInfo.WithPreviousEnd());
        }
        return result;
    }

    /// <summary>
    /// or_test: <see cref="ParseAndTest">and_test</see> | or_test "or" <see cref="ParseAndTest">and_test</see>
    /// </summary>
    /// <returns></returns>
    private AstExprNode ParseOrTest()
    {
        var metaInfo = CreateAstMetaInfo();
        var result = ParseAndTest();

        if (IsCurrentKeyword("or"))
        {
            List<AstExprNode> values = [result];
            while (IsCurrentKeyword("or"))
            {
                MoveNextToken();
                values.Add(ParseAndTest());
            }
            result = Ast.Or(values).With(metaInfo.WithPreviousEnd());
        }
        return result;
    }

    /// <summary>
    /// conditional_expression: <see cref="ParseOrTest">or_test</see> ["if" <see cref="ParseOrTest">or_test</see> "else" <see cref="ParseExpression">expression</see>]
    /// </summary>
    /// <returns></returns>
    private AstExprNode ParseConditionalExpression()
    {
        var metaInfo = CreateAstMetaInfo();
        var body = ParseOrTest();
        if (IsCurrentKeyword("if"))
        {
            MoveNextToken();
            var test = ParseOrTest();
            EnsureKeywordThenMove("else");
            var orelse = ParseExpression();
            return Ast.IfExp(test, body, orelse).With(metaInfo.WithPreviousEnd());
        }
        return body;
    }

    /// <summary>
    /// lambda_expr: "lambda" [<see cref="ParseParameterList(StopPredicate)">parameter_list</see>] ":" <see cref="ParseExpression">expression</see>
    /// </summary>
    /// <returns></returns>
    private LambdaNode ParseLambdaExpr()
    {
        var metaInfo = CreateAstMetaInfo();
        EnsureKeywordThenMove("lambda");
        var args = CurrentTokenType is TokenType.Colon ? new() : ParseParameterList(StopPredicates.UntilColon);
        EnsureTokenTypeThenMove(TokenType.Colon);
        return Ast.Lambda(args, ParseExpression()).With(metaInfo);
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
        if (CurrentTokenType is not TokenType.Star)
            return ParseExpression();
        
        var metaInfo = CreateAstMetaInfo();
        MoveNextToken();
        var value = ParseOrExpr();
        return Ast.Starred(value).With(metaInfo.WithPreviousEnd());
    }

    private List<AstExprNode> ParseStarredExpressionList(StopPredicate predicate, out TokenInfo? endsWithComma)
    {
        return ParseSomeExpressionList(ParseStarredExpression, predicate, out endsWithComma);
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
    private List<AstExprNode> ParseFlexibleExpressionList(StopPredicate predicate, out TokenInfo? endsWithComma)
    {
        return ParseSomeExpressionList(ParseFlexibleExpression, predicate, out endsWithComma);
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
        Debug.Assert(list.Count > 0);

        if (list.Count is 1 && endsWithComma is null)
            return list[0];

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

        return Ast.Tuple(list).With(metaInfo);
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
        return ParseSomeExpressionList(ParseTarget, predicate, out endsWithComma);
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

            return Ast.Comprehension(target, iter, ifs);
        }
    }

    /// <summary>
    /// comprehension: <see cref="ParseAssignmentExpression">assignment_expression</see> <see cref="ParseCompFor">comp_for</see>
    /// </summary>
    /// <returns></returns>
    private (AstExprNode Elt, List<AstComprehensionNode> Generators) ParseComprehension()
    {
        var elt = ParseAssignmentExpression();
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

                    if (arguments.Args.Count is 0)
                        throw _context.ThrowableSyntaxError("at least one argument must precede /");

                    MoveNextToken();
                    arguments.PosonlyArgs.AddRange(arguments.Args);
                    arguments.Args.Clear();
                    state = StateAfterPosonly;
                    break;

                case TokenType.Star:
                    if (state is StateKwonly)
                        throw _context.ThrowableSyntaxError("* may appear only once");

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
                            throw _context.ThrowableSyntaxError($"duplicate argument '{arg.Arg}' in function definition");
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
                            throw _context.ThrowableSyntaxError("parameter without a default follows parameter with a default");
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
                    throw _context.ThrowableSyntaxError("invalid syntax");
            }
        }
    }

    /// <summary>
    /// expression_list: <see cref="ParseExpression">expression</see> ("," <see cref="ParseExpression">expression</see>)* [","]
    /// </summary>
    /// <param name="endsWithComma"></param>
    /// <param name="stopTokens"></param>
    /// <returns></returns>
    private List<AstExprNode> ParseExpressionList(StopPredicate predicate, out TokenInfo? endsWithComma)
    {
        return ParseSomeExpressionList(ParseExpression, predicate, out endsWithComma);
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

    /// <summary>
    /// yield_atom: "(" yield_expression ")"
    /// </summary>
    /// <returns></returns>
    private AstExprNode ParseYieldAtom()
    {
        EnsureTokenTypeThenMove(TokenType.LeftParen);
        var yieldExpr = ParseYieldExpression();
        EnsureTokenTypeThenMove(TokenType.RightParen);
        return yieldExpr;
    }

    private AstExprNode ParseYieldExpression()
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
        var list = ParseStarredExpressionList(StopPredicates.UntilRightParenOrNewLineOrSemicolon, out var endsWithComma);
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
        var keys = new HashSet<string>();
        bool iskw = false;

        while (CurrentTokenType is not TokenType.RightParen)
        {
            var arg = ParseFlexibleExpression();
            if (CurrentTokenType is TokenType.Equal)
            {
                iskw = true;

                if (arg is not NameNode argName)
                    throw _context.ThrowableSyntaxError("expression cannot contain assignment, perhaps you meant \"==\"?");

                if (keys.Contains(argName.Id))
                    throw _context.ThrowableSyntaxError($"keyword argument repeated: {argName.Id}");
                else
                    keys.Add(argName.Id);

                MoveNextToken();
                var value = ParseExpression();

                kwargs.Add(Ast.Keyword(argName.Id, value));
            }
            else if (iskw)
            {
                throw _context.ThrowableSyntaxError("positional argument follows keyword argument");
            }
            else
            {
                args.Add(arg);
            }

            if (CurrentTokenType is TokenType.Comma)
                MoveNextToken();
            else if (CurrentTokenType is not TokenType.RightParen)
                throw _context.ThrowableSyntaxError("'(' was never closed");
        }

        return (args, kwargs);
    }

    private List<AstExprNode> ParseSomeExpressionList(Func<AstExprNode> parse, StopPredicate predicate, out TokenInfo? endsWithComma)
    {
        endsWithComma = null;
        List<AstExprNode> list = [parse()];
        while (CurrentTokenType is TokenType.Comma)
        {
            MoveNextToken();
            if (predicate(CurrentToken))
            {
                endsWithComma = CurrentToken;
                break;
            }
            list.Add(parse());
        }

        if (CurrentTokenType is TokenType.Equal && !predicate(CurrentToken))
            throw ThrowableSyntaxErrorCausedByInvalidEqualAfterExpr(list[^1]);

        return list;
    }
}
