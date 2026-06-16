using PySharp.Compilation.CodeAnalysis;
using PySharp.Compilation.Primitives;
using PySharp.Compilation.Tokenization;
using PySharp.Modules.Builtins;
using PySharp.Runtime;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PySharp.Compilation.AstNodes;

partial class Parser
{
    /// <summary>
    /// identifier: &lt;NAME, except keywords&gt;
    /// </summary>
    /// <returns></returns>
    /// <exception cref="PyRuntimeException"></exception>
    private string ParseIdentifier()
    {
        return ParseIdentifier(out _);
    }

    private string ParseNonMangledIdentifier()
    {
        if (!IsCurrentIdentifier)
            throw SyntaxError();

        var id = CurrentTokenString;
        MoveNextToken();
        return id;
    }

    private string ParseIdentifier(out string originalIdentifier)
    {
        originalIdentifier = ParseNonMangledIdentifier();
        return MangleIdentifier(originalIdentifier);
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
            return ParseSequenceOrComprehension(TokenType.LeftSquareBracket, TokenType.RightSquareBracket,
                ParseNamedExpression, ParseList, ParseListComp);
        }
        else if (CurrentTokenType is TokenType.LeftParen)
        {
            if (TryParseGroup(out var group))
                return group;

            return ParseSequenceOrComprehension(TokenType.LeftParen, TokenType.RightParen,
                ParseNamedExpression, ParseTuple, ParseGenExp);
        }
        else if (CurrentTokenType is TokenType.LeftBrace)
        {
            if (TestIsDictRatherThanSet())
                return ParseSequenceOrComprehension(TokenType.LeftBrace, TokenType.RightBrace,
                    ParseKvpair, ParseDict, ParseDictComp);

            return ParseSequenceOrComprehension(TokenType.LeftBrace, TokenType.RightBrace,
                ParseNamedExpression, ParseSet, ParseSetComp);
        }

        throw SyntaxError();

        bool TestIsDictRatherThanSet()
        {
            var pos = TokenPosition;
            bool isDict;
            EnsureTokenTypeThenMove(TokenType.LeftBrace);

            if (CurrentTokenType is TokenType.RightBrace)
            {
                // {} is an empty dict instead of an empty set
                isDict = true;
            }
            else if (TestIsAssignmentExpression())
            {
                // as element, assignment_expression only appears in set or setcomp
                isDict = false;
            }
            else if (CurrentTokenType is TokenType.Star)
            {
                // only set or setcomp allow *expr 
                isDict = false;
            }
            else if (CurrentTokenType is TokenType.DoubleStar)
            {
                // only dict or dictcomp allow **expr 
                isDict = true;
            }
            else
            {
                _ = ParseExpression();
                isDict = CurrentTokenType is TokenType.Colon;
            }
            TokenPosition = pos;

            return isDict;
        }
    }

    [GrammarSyntaxRule("group")]
    private bool TryParseGroup([NotNullWhen(true)] out AstExprNode? group)
    {
        var pos = TokenPosition;
        EnsureTokenTypeThenMove(TokenType.LeftParen);

        if (CurrentTokenType is TokenType.RightParen or TokenType.Star)
        {
            TokenPosition = pos;
            group = null;
            return false;
        }

        if (IsCurrentKeyword("yield"))
        {
            group = ParseYieldExpr();
            EnsureTokenTypeThenMove(TokenType.RightParen);
            return true;
        }

        group = ParseNamedExpression();
        if (CurrentTokenType is TokenType.RightParen)
        {
            MoveNextToken();
            return true;
        }

        TokenPosition = pos;
        return false;
    }

    [GrammarSyntaxRule("fstring_middle")]
    private AstExprNode ParseFStringMiddle(bool isRaw)
    {
        if (CurrentTokenType is not (TokenType.FStringMiddle or TokenType.TStringMiddle))
            return ParseFStringReplacementField(isRaw);

        var literal = PyStrConverter.FromSourceToLiteral(CurrentTokenStringAsSpan, isRaw, SharedBuilder);
        var str = isRaw ? literal : FromLiteralToString(literal, true);
        var middle = Ast.Constant(str).With(CreateAstMetaInfo());
        MoveNextToken();
        return middle;
    }

    [GrammarSyntaxRule("fstring_replacement_field")]
    private AstExprNode ParseFStringReplacementField(bool isRaw)
    {
        EnsureTokenTypeThenMove(TokenType.LeftBrace);
        if (CurrentTokenType is TokenType.Equal)
            throw SyntaxError(PySR.InvalidSyntax_FString_ReplacementField_BeforeEqual);
        if (CurrentTokenType is TokenType.Exclamation)
            throw SyntaxError(PySR.InvalidSyntax_FString_ReplacementField_BeforeExclamation);
        if (CurrentTokenType is TokenType.Colon)
            throw SyntaxError(PySR.InvalidSyntax_FString_ReplacementField_BeforeColon);
        if (CurrentTokenType is TokenType.RightBrace)
            throw SyntaxError(PySR.InvalidSyntax_FString_ReplacementField_BeforeRightBrace);

        var start = CurrentToken.GetStart(_codeSource);
        var metaInfo = CreateAstMetaInfo();
        var value = ParseAnnotatedRhs(StopPredicates.UntilRightBraceOrEqualOrExclamationOrColon);

        var debugSpec = null as AstExprNode;
        if (CurrentTokenType is TokenType.Equal)
        {
            MoveNextToken();
            var end = CurrentToken.GetStart(_codeSource);

            if (!_codeSource.Code.TryGetRange(start, end, out var range))
                throw _context.PySharpException("incorrect code text position");

            debugSpec = Ast.Constant(range.ToString()).With(metaInfo.WithEnd());
        }
        else
        {
            if (!IsCurrentTypeTokenAnyOf(TokenType.Exclamation, TokenType.Colon, TokenType.RightBrace))
                throw SyntaxError(PySR.InvalidSyntax_FString_ReplacementField_ExpectingEqual);
        }

        var conversion = -1;
        if (CurrentTokenType is TokenType.Exclamation)
            conversion = ParseFStringConversion();
        else if (!IsCurrentTypeTokenAnyOf(TokenType.Colon, TokenType.RightBrace))
            throw SyntaxError(PySR.InvalidSyntax_FString_ReplacementField_ExpectingExclamation);

        var format_spec = null as JoinedStrNode;
        if (CurrentTokenType is TokenType.Colon)
            format_spec = ParseFStringFullFormatSpec(isRaw);
        else if (!IsCurrentTypeTokenAnyOf(TokenType.RightBrace))
            throw SyntaxError(PySR.InvalidSyntax_FString_ReplacementField_ExpectingColon);

        EnsureTokenTypeThenMove(TokenType.RightBrace, format_spec is null
            ? PySR.InvalidSyntax_FString_ReplacementField_ExpectingRightBrace
            : PySR.InvalidSyntax_FString_ReplacementField_ExpectingRightBraceOrSpecs);

        var formatted = Ast.FormattedValue(value, conversion, format_spec).With(metaInfo.WithPreviousEnd());
        if (debugSpec is null)
            return formatted;
        return Ast.JoinedStr([debugSpec, formatted]).With(metaInfo.WithPreviousEnd());
    }

    [GrammarSyntaxRule("fstring_conversion")]
    private int ParseFStringConversion()
    {
        EnsureTokenTypeThenMove(TokenType.Exclamation);
        if (IsCurrentTypeTokenAnyOf(TokenType.Colon, TokenType.RightBrace))
            throw SyntaxError(PySR.InvalidSyntax_FString_ConversionCharacter_Missing);

        if (!IsCurrentIdentifier)
            throw SyntaxError(PySR.InvalidSyntax_FString_ConversionCharacter_Invalid);

        var conversion = CurrentTokenStringAsSpan;
        if (conversion is not ("s" or "r" or "a"))
            throw SyntaxError(PySR.InvalidSyntax_FString_ConversionCharacter_InvalidCharacter, CurrentTokenString);
        MoveNextToken();

        return conversion[0];
    }

    [GrammarSyntaxRule("fstring_full_format_spec")]
    private JoinedStrNode ParseFStringFullFormatSpec(bool isRaw)
    {
        EnsureTokenTypeThenMove(TokenType.Colon);

        var metaInfo = CreateAstMetaInfo();

        // ConstantNode(string) or FormattedValueNode or JoinedStrNode
        List<AstExprNode> formatSpecs = [];
        while (CurrentTokenType is not TokenType.RightBrace)
        {
            var formatSpec = ParseFStringFormatSpec(isRaw);
            formatSpecs.Add(formatSpec);
        }

        return ConcatToJoinedStr(formatSpecs).With(metaInfo.WithPreviousEnd());
    }

    [GrammarSyntaxRule("fstring_format_spec")]
    private AstExprNode ParseFStringFormatSpec(bool isRaw)
    {
        return ParseFStringMiddle(isRaw);
    }

    [GrammarSyntaxRule("fstring")]
    private JoinedStrNode ParseFString()
    {
        var metaInfo = CreateAstMetaInfo();
        EnsureTokenType(TokenType.FStringStart);
        var isRaw = CurrentTokenStringAsSpan.Contains('r');
        MoveNextToken();

        // ConstantNode(string) or FormattedValueNode or JoinedStrNode
        List<AstExprNode> values = [];
        while (CurrentTokenType is not TokenType.FStringEnd)
            values.Add(ParseFStringMiddle(isRaw));
        MoveNextToken();

        return ConcatToJoinedStr(values).With(metaInfo.WithPreviousEnd());
    }

    private static JoinedStrNode ConcatToJoinedStr(List<AstExprNode> nodes)
    {
        return Ast.JoinedStr(nodes.SelectMany(FlattenIfJoinedStr).Where(IsNotEmptyConstantString));

        static bool IsNotEmptyConstantString(AstExprNode node)
        {
            if (node is not ConstantNode constant)
                return true;

            return constant.Value is not PyStrObject { Value: "" };
        }

        static IEnumerable<AstExprNode> FlattenIfJoinedStr(AstExprNode node)
        {
            if (node is not JoinedStrNode joinedStrNode)
                return [node];

            return joinedStrNode.Values;
        }
    }

    [GrammarSyntaxRule("string")]
    private ConstantNode ParseString()
    {
        EnsureTokenType(TokenType.String);
        var source = CurrentTokenStringAsSpan;
        var prefix = source[..source.IndexOfAny('\'', '"')];
        var isRaw = prefix.ContainsAny('r', 'R');
        var literal = PyStrConverter.FromSourceToLiteral(CurrentTokenStringAsSpan, isRaw, SharedBuilder);
        var isBytes = prefix.ContainsAny('b', 'B');

        var constant = isBytes ? Ast.Constant(FromLiteralToBytes(literal))
            : Ast.Constant(FromLiteralToString(literal, noWrapper: false));

        MoveNextToken();
        return constant;
    }

    [GrammarSyntaxRule("tstring_middle")]
    private void ParseTStringMiddle(List<AstExprNode> values, bool isRaw)
    {
        if (CurrentTokenType is not TokenType.TStringMiddle)
        {
            var (debugSpec, interpolation) = ParseTStringReplacementField(isRaw);
            if (debugSpec is not null)
                values.Add(debugSpec);
            values.Add(interpolation);
            return;
        }

        var literal = PyStrConverter.FromSourceToLiteral(CurrentTokenStringAsSpan, isRaw, SharedBuilder);
        var str = isRaw ? literal : FromLiteralToString(literal, true);
        var middle = Ast.Constant(str).With(CreateAstMetaInfo());
        MoveNextToken();
        values.Add(middle);
    }

    [GrammarSyntaxRule("tstring_replacement_field")]
    private (ConstantNode? DebugSpec, InterpolationNode Interpolation) ParseTStringReplacementField(bool isRaw)
    {
        EnsureTokenTypeThenMove(TokenType.LeftBrace);
        if (CurrentTokenType is TokenType.Equal)
            throw SyntaxError(PySR.InvalidSyntax_TString_ReplacementField_BeforeEqual);
        if (CurrentTokenType is TokenType.Exclamation)
            throw SyntaxError(PySR.InvalidSyntax_TString_ReplacementField_BeforeExclamation);
        if (CurrentTokenType is TokenType.Colon)
            throw SyntaxError(PySR.InvalidSyntax_TString_ReplacementField_BeforeColon);
        if (CurrentTokenType is TokenType.RightBrace)
            throw SyntaxError(PySR.InvalidSyntax_TString_ReplacementField_BeforeRightBrace);

        var start = CurrentToken.GetStart(_codeSource);
        var metaInfo = CreateAstMetaInfo();
        var value = ParseAnnotatedRhs(StopPredicates.UntilRightBraceOrEqualOrExclamationOrColon);
        string str;
        {
            var position = TokenPosition - 1;
            var span = _tokenSequence.AsSpan();
            while (IsUselessToken(span[position]))
                position--;
            var exprEndToken = span[position];
            var end = exprEndToken.GetEnd(_codeSource);

            if (!_codeSource.Code.TryGetRange(start, end, out var range))
                throw _context.PySharpException("incorrect code text position");

            str = range.ToString();
        }

        var debugSpec = null as ConstantNode;
        if (CurrentTokenType is TokenType.Equal)
        {
            MoveNextToken();
            var end = CurrentToken.GetStart(_codeSource);

            if (!_codeSource.Code.TryGetRange(start, end, out var range))
                throw _context.PySharpException("incorrect code text position");

            debugSpec = Ast.Constant(range.ToString()).With(metaInfo.WithEnd());
        }
        else
        {
            if (!IsCurrentTypeTokenAnyOf(TokenType.Exclamation, TokenType.Colon, TokenType.RightBrace))
                throw SyntaxError(PySR.InvalidSyntax_TString_ReplacementField_ExpectingEqual);
        }

        var conversion = -1;
        if (CurrentTokenType is TokenType.Exclamation)
            conversion = ParseTStringConversion();
        else if (!IsCurrentTypeTokenAnyOf(TokenType.Colon, TokenType.RightBrace))
            throw SyntaxError(PySR.InvalidSyntax_TString_ReplacementField_ExpectingExclamation);

        var format_spec = null as JoinedStrNode;
        if (CurrentTokenType is TokenType.Colon)
            format_spec = ParseTStringFullFormatSpec(isRaw);
        else if (!IsCurrentTypeTokenAnyOf(TokenType.RightBrace))
            throw SyntaxError(PySR.InvalidSyntax_TString_ReplacementField_ExpectingColon);

        EnsureTokenTypeThenMove(TokenType.RightBrace, format_spec is null
            ? PySR.InvalidSyntax_TString_ReplacementField_ExpectingRightBrace
            : PySR.InvalidSyntax_TString_ReplacementField_ExpectingRightBraceOrSpecs);

        var interpolation = Ast.Interpolation(value, str, conversion, format_spec).With(metaInfo.WithPreviousEnd());
        return (debugSpec, interpolation);
    }

    [GrammarSyntaxRule("tstring_conversion")]
    private int ParseTStringConversion()
    {
        EnsureTokenTypeThenMove(TokenType.Exclamation);
        if (IsCurrentTypeTokenAnyOf(TokenType.Colon, TokenType.RightBrace))
            throw SyntaxError(PySR.InvalidSyntax_TString_ConversionCharacter_Missing);

        if (!IsCurrentIdentifier)
            throw SyntaxError(PySR.InvalidSyntax_TString_ConversionCharacter_Invalid);

        var conversion = CurrentTokenStringAsSpan;
        if (conversion is not ("s" or "r" or "a"))
            throw SyntaxError(PySR.InvalidSyntax_TString_ConversionCharacter_InvalidCharacter, CurrentTokenString);
        MoveNextToken();

        return conversion[0];
    }

    [GrammarSyntaxRule("tstring_full_format_spec")]
    private JoinedStrNode ParseTStringFullFormatSpec(bool isRaw)
    {
        EnsureTokenTypeThenMove(TokenType.Colon);

        var metaInfo = CreateAstMetaInfo();

        // ConstantNode(string) or FormattedValueNode or JoinedStrNode
        List<AstExprNode> formatSpecs = [];
        while (CurrentTokenType is not TokenType.RightBrace)
        {
            var formatSpec = ParseTStringFormatSpec(isRaw);
            formatSpecs.Add(formatSpec);
        }

        return ConcatToJoinedStr(formatSpecs).With(metaInfo.WithPreviousEnd());
    }

    [GrammarSyntaxRule("tstring_format_spec")]
    private AstExprNode ParseTStringFormatSpec(bool isRaw)
    {
        return ParseFStringMiddle(isRaw);
    }

    private static TemplateStrNode ConcatToTemplateStr(List<AstExprNode> nodes)
    {
        if (nodes.Count is 0 || nodes[0] is not ConstantNode)
            nodes.Insert(0, Ast.Constant(string.Empty));

        var preNode = nodes[0];
        Debug.Assert(preNode is ConstantNode);

        for (int i = 1; i < nodes.Count; i++)
        {
            var node = nodes[i];
            Debug.Assert(node is ConstantNode or InterpolationNode);
            Debug.Assert((preNode is ConstantNode && node is ConstantNode or InterpolationNode)
                || (preNode is InterpolationNode && node is ConstantNode));

            if (node is ConstantNode constant && preNode is ConstantNode preConstant)
            {
                Debug.Assert(constant.Value is PyStrObject);
                Debug.Assert(preConstant.Value is PyStrObject);

                node = nodes[i] = Ast.Constant(((PyStrObject)preConstant.Value).Value + ((PyStrObject)constant.Value).Value);
                nodes[i - 1] = null!;
            }

            preNode = node;
        }

        return Ast.TemplateStr(nodes.Where(static node => node is not null));
    }

    [GrammarSyntaxRule("tstring")]
    private void ParseTString(List<AstExprNode> values)
    {
        EnsureTokenType(TokenType.TStringStart);
        var isRaw = CurrentTokenStringAsSpan.Contains('r');
        MoveNextToken();

        while (CurrentTokenType is not TokenType.TStringEnd)
            ParseTStringMiddle(values, isRaw);
        MoveNextToken();
    }

    private TemplateStrNode ParseTStrings()
    {
        Debug.Assert(CurrentTokenType is TokenType.TStringStart);

        var metaInfo = CreateAstMetaInfo();

        // ConstantNode(string) or InterpolationNode
        List<AstExprNode> values = [];

        while (CurrentTokenType is TokenType.TStringStart)
            ParseTString(values);

        return ConcatToTemplateStr(values).With(metaInfo.WithPreviousEnd());
    }

    [GrammarSyntaxRule("strings")]
    private AstExprNode ParseStrings()
    {
        if (CurrentTokenType is TokenType.TStringStart)
            return ParseTStrings();

        if (CurrentTokenType is not (TokenType.String or TokenType.FStringStart))
            throw SyntaxError();

        var metaInfo = CreateAstMetaInfo();

        // ConstantNode(string/bytes) or JoinedStrNode
        List<AstExprNode> nodes = [];

        while (CurrentTokenType is TokenType.String or TokenType.FStringStart)
        {
            if (CurrentTokenType is TokenType.String)
                nodes.Add(ParseString());
            else
                nodes.Add(ParseFString());
        }

        return ConcatStrings(nodes);

        AstExprNode ConcatConstants(List<AstExprNode> nodes, int skipCount = 0)
        {
            var span = Unsafe.BitCast<Span<AstExprNode>, Span<ConstantNode>>(CollectionsMarshal.AsSpan(nodes)[skipCount..]);
            Debug.Assert(!span.IsEmpty);

            if (span.Length is 1)
                return span[0];

            if (span[0].Value is PyBytesObject)
            {
                var totalLength = 0;
                foreach (var node in span)
                {
                    if (node.Value is not PyBytesObject bytes)
                        throw SyntaxError();
                    totalLength += bytes.Length;
                }

                var combined = new byte[totalLength];
                var offset = 0;
                foreach (var node in span)
                {
                    var bytes = (PyBytesObject)node.Value;
                    bytes.AsSpan().CopyTo(combined.AsSpan(offset));
                    offset += bytes.Length;
                }

                return Ast.Constant(PyBytesObject.MoveBytes(combined));
            }

            var builder = SharedBuilder.Clear();
            foreach (var node in span)
            {
                if (node.Value is not PyStrObject value)
                    throw SyntaxError();
                builder.Append(value.Value);
            }

            return Ast.Constant(builder.ToString());
        }

        AstExprNode ConcatStrings(List<AstExprNode> nodes)
        {
            if (nodes.All(static node => node is ConstantNode))
                return ConcatConstants(nodes);

            if (nodes.Any(static node => node is ConstantNode { Value: PyBytesObject }))
                throw SyntaxError();

            var flattened = nodes.SelectMany(static node => node switch
            {
                ConstantNode n => [n],
                JoinedStrNode n => n.Values,
                _ => throw new UnreachableException()
            });

            var combinedNodes = new List<AstExprNode>();

            foreach (var node in flattened)
            {
                if (node is FormattedValueNode)
                {
                    for (int i = combinedNodes.Count - 1; i >= 0; i--)
                    {
                        if (combinedNodes[i] is ConstantNode)
                            continue;

                        if (i == combinedNodes.Count - 1)
                            break;

                        var combinedConstant = ConcatConstants(combinedNodes, i + 1);
                        combinedNodes.RemoveRange(i + 1, combinedNodes.Count - i - 1);
                        combinedNodes.Add(combinedConstant);
                        break;
                    }
                    combinedNodes.Add(node);
                }
                else
                {
                    Debug.Assert(node is ConstantNode);
                    combinedNodes.Add(node);
                }
            }

            return ConcatToJoinedStr(combinedNodes);
        }
    }

    private PyBytesObject FromLiteralToBytes(ReadOnlySpan<char> literal)
    {
        var text = FromLiteralToString(literal, noWrapper: false);

        var bytes = new byte[text.Length];
        for (int i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (ch > byte.MaxValue)
                throw SyntaxError();
            bytes[i] = (byte)ch;
        }

        return PyBytesObject.MoveBytes(bytes);
    }

    string FromLiteralToString(ReadOnlySpan<char> literal, bool noWrapper)
    {
        bool successful;
        string? str;
        PyStrConverter.ConvertErrorInfo info;
        if (noWrapper)
            successful = PyStrConverter.TryFromTextToString(literal, out str, out info);
        else
            successful = PyStrConverter.TryFromLiteralToString(literal, out str, out info);

        if (successful)
        {
            if (info.Error is PyStrConverter.ConvertError.InvalidEscapeSequence)
            {
                if (!_context.TryWarn<PySyntaxWarningObjectType>(
                    PySR.Format(PySR.InvalidSyntax_Warning_InvalidEscapeSequence, info.Char)))
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

            var start = info.Position;
            var end = info.Position + info.Length - 1;
            throw info.Error switch
            {
                PyStrConverter.ConvertError.LowerXSequence => SyntaxError(PySR.InvalidSyntax_UnicodeError_TruncatedLowerXSequence, start, end),
                PyStrConverter.ConvertError.LowerUSequence => SyntaxError(PySR.InvalidSyntax_UnicodeError_TruncatedLowerUSequence, start, end),
                PyStrConverter.ConvertError.UpperUSequence => SyntaxError(PySR.InvalidSyntax_UnicodeError_TruncatedUpperUSequence, start, end),
                PyStrConverter.ConvertError.SurrogatesNotAllowed => _context.UnicodeEncodeError(PySR.Unicode_Encode_SurrogatesNotAllowed, $"{(uint)info.Char:x4}", info.Position),
                PyStrConverter.ConvertError.IllegalUnicodeCharacter => SyntaxError(PySR.InvalidSyntax_UnicodeError_IllegalCharacter, start, end),
                _ => new UnreachableException(),
            };
        }
    }

    [GrammarSyntaxRule("atom")]
    private AstExprNode ParseAtom()
    {
        var metaInfo = CreateAstMetaInfo();
        if (CurrentTokenType is TokenType.Name)
        {
            if (IsKeyword(CurrentTokenStringAsSpan))
            {
                // keyword literal

                if (CurrentTokenStringAsSpan is "True" or "False")
                {
                    var value = CurrentTokenStringAsSpan;
                    MoveNextToken();
                    return Ast.Constant(bool.Parse(value)).With(metaInfo);
                }
                else if (CurrentTokenStringAsSpan is "None")
                {
                    MoveNextToken();
                    return Ast.Constant(PyNoneObject.None).With(metaInfo);
                }

                throw SyntaxError();
            }
            else if (CurrentTokenStringAsSpan is PySpecialNames.Debug)
            {
                // __debug__

                MoveNextToken();
                return Ast.Constant(_optimizationLevel is 0).With(metaInfo);
            }
            else
            {
                // identifier

                var nameNode = Ast.Name(ParseIdentifier()).With(metaInfo);
                return nameNode;
            }
        }
        else if (CurrentTokenType is TokenType.String or TokenType.FStringStart or TokenType.TStringStart)
        {
            return ParseStrings();
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
                throw _context.IndentationError(PySR.InvalidSyntax_Indentation_Unexpected);

            throw SyntaxError();
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
                var pos = TokenPosition;
                MoveNextToken();
                var isGenExp = TestIsGenExp();
                TokenPosition = pos;

                if (isGenExp)
                {
                    var genExp = ParseGenExp();
                    primary = Ast.Call(primary, [genExp], []).With(startMetaInfo.WithPreviousEnd());
                }
                else
                {
                    MoveNextToken();
                    var (args, kwargs) = ParseArguments();
                    EnsureTokenTypeThenMove(TokenType.RightParen);
                    primary = Ast.Call(primary, args, kwargs).With(startMetaInfo.WithPreviousEnd());
                }

                bool TestIsGenExp()
                {
                    if (CurrentTokenType is TokenType.Star or TokenType.DoubleStar or TokenType.RightParen)
                        // func(*args) or func(**kwargs) or func()
                        return false;

                    if (IsCurrentIdentifier)
                    {
                        var pos = TokenPosition;
                        MoveNextToken();
                        if (CurrentTokenType is TokenType.Equal)
                            // func(arg=value)
                            return false;
                        TokenPosition = pos;
                    }

                    _ = ParseNamedExpression();
                    return IsCurrentKeyword("for");
                }
            }
            else if (CurrentTokenType is TokenType.LeftSquareBracket)
            {
                var currentMetaInfo = startMetaInfo.WithCrucial();
                MoveNextToken();

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

        var metaInfo = CreateAstMetaInfo();
        MoveNextToken();
        var expr = ParsePrimary();
        return Ast.Await(expr).With(metaInfo.WithPreviousEnd());
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

    [GrammarSyntaxRule("lambdef")]
    private LambdaNode ParseLambDef()
    {
        var metaInfo = CreateAstMetaInfo();
        EnsureKeywordThenMove("lambda");
        var args = CurrentTokenType is TokenType.Colon ? Ast.Arguments() : ParseParams(isLambda: true);
        EnsureTokenTypeThenMove(TokenType.Colon);
        return Ast.Lambda(args, ParseExpression()).With(metaInfo);
    }

    [GrammarSyntaxRule("expression")]
    private AstExprNode ParseExpression()
    {
        if (IsCurrentKeyword("lambda"))
            return ParseLambDef();

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
        if (expr is NameNode && CurrentTokenType is TokenType.Equal)
            throw SyntaxError(PySR.InvalidSyntax_NamedExpression_NameWithEqual);
        if (CurrentTokenType is TokenType.ColonEqual)
            throw SyntaxError(PySR.InvalidSyntax_NamedExpression_InvalidTarget, AstUtils.GetExprNodeName(expr));
        return expr;
    }

    private bool TestIsAssignmentExpression()
    {
        if (!IsCurrentIdentifier)
            return false;

        var pos = TokenPosition;
        MoveNextToken();
        var isAssignment = CurrentTokenType is TokenType.ColonEqual;
        TokenPosition = pos;
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
    private AstExprNode ParseStarExpressions(StopPredicate predicate)
    {
        var list = ParseSomethingList(ParseStarExpression, predicate, out var endsWithComma);
        return UnwrapOrMakeTuple(list, endsWithComma);
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
    private List<AstExprNode> ParseStarNamedExpressions(StopPredicate predicate, out Token? endsWithComma)
    {
        return ParseSomethingList(ParseStarNamedExpression, predicate, out endsWithComma);
    }

    /// <summary>
    /// [expr] => Unwrap
    /// <br/> [expr, ] => MakeTuple
    /// <br/> [expr, expr] => MakeTuple
    /// </summary>
    /// <param name="list"></param>
    /// <param name="endsWithComma"></param>
    /// <returns></returns>
    private static AstExprNode UnwrapOrMakeTuple(List<AstExprNode> list, Token? endsWithComma)
    {
        return UnwrapOrPackSomething(list, endsWithComma, Ast.Tuple);
    }

    private static TResult PackSomething<TSource, TResult>(List<TSource> list, Token? endsWithComma, Func<List<TSource>, TResult> packer)
        where TSource : AstNode
        where TResult : AstNode
    {
        ValueCodeMetaInfo metaInfo = ValueCodeMetaInfo.Empty;

        var startMetaInfo = list[0].MetaInfo;
        if (!startMetaInfo.IsEmpty)
        {
            var start = startMetaInfo.Range.Start;
            var end = start;

            if (endsWithComma is not null)
            {
                end = endsWithComma.Value.StringSpan.End;
            }
            else
            {
                var endMetaInfo = list[^1].MetaInfo;
                if (!endMetaInfo.IsEmpty)
                    end = endMetaInfo.Range.End;
            }

            metaInfo = ValueCodeMetaInfo.FromSpan(new CodeTextSpan(start, end - start), default);
        }

        return packer(list).With(metaInfo);
    }

    private static T UnwrapOrPackSomething<T>(List<T> list, Token? endsWithComma, Func<List<T>, T> packer) where T : AstNode
    {
        Debug.Assert(list.Count > 0);

        if (list.Count is 1 && endsWithComma is null)
            return list[0];

        return PackSomething(list, endsWithComma, packer);
    }

    private bool TestIsComprehension<TItem>(TokenType closeToken, Func<TItem> parseItem)
    {
        if (CurrentTokenType == closeToken)
            // empty sequence
            return false;

        if (CurrentTokenType is TokenType.Star or TokenType.DoubleStar)
            // it must be a starred_expression
            // comprehension should start with named_expression
            return false;

        var index = TokenPosition;
        _ = parseItem();
        var isComp = IsCurrentKeyword("for") || IsCurrentKeyword("async");
        TokenPosition = index;
        return isComp;
    }

    [GrammarSyntaxRule("params")]
    [GrammarSyntaxRule("lambda_params")]
    private AstArgumentsNode ParseParams(bool isLambda)
    {
        if (CurrentTokenType is TokenType.Slash)
        {
            MoveNextToken();
            if (CurrentTokenType is TokenType.Comma)
                throw SyntaxError(PySR.InvalidSyntax_Parameters_NoArgsBeforeSlash);

            throw SyntaxError();
        }

        return ParseParameters(isLambda);
    }

    [GrammarSyntaxRule("parameters")]
    [GrammarSyntaxRule("lambda_parameters")]
    private AstArgumentsNode ParseParameters(bool isLambda)
    {
        const int StateMaybePosonlyArgsOrArgs = 0, StateArgs = 1, StateKwonly = 2, StateEnd = 3;

        List<AstArgNode> posonlyArgs = [];
        List<AstArgNode> args = [];
        AstArgNode? varArg = null;
        List<AstArgNode> kwonlyArgs = [];
        AstArgNode? kwArg = null;
        List<AstExprNode?> kwDefaults = [];
        List<AstExprNode> defaults = [];

        var state = StateMaybePosonlyArgsOrArgs;
        var needDefault = false;
        var stopToken = isLambda ? TokenType.Colon : TokenType.RightParen;

        ParseParameter();
        while (CurrentTokenType is TokenType.Comma)
        {
            MoveNextToken();
            if (CurrentTokenType == stopToken)
                break;
            ParseParameter();
        }
        return Ast.Arguments(posonlyArgs, args, varArg, kwonlyArgs, kwArg, kwDefaults, defaults);

        void ParseParameter()
        {
            if (state is StateEnd)
                throw SyntaxError(PySR.InvalidSyntax_Parameters_ArgsFollowVarKwArg);

            switch (CurrentTokenType)
            {
                case TokenType.Slash:
                    if (state is StateArgs)
                        throw SyntaxError(PySR.InvalidSyntax_Parameters_MultipleSlashes);

                    if (state is StateKwonly)
                        throw SyntaxError(PySR.InvalidSyntax_Parameters_SlashAfterStar);

                    MoveNextToken();
                    posonlyArgs.AddRange(args);
                    args.Clear();
                    state = StateArgs;
                    break;

                case TokenType.Star:
                    if (state is StateKwonly)
                        throw SyntaxError(PySR.InvalidSyntax_Parameters_MultipleStars);

                    MoveNextToken();
                    if (CurrentTokenType is not TokenType.Comma)
                        varArg = ParseParamStarAnnotation();

                    state = StateKwonly;
                    needDefault = false;
                    break;

                case TokenType.DoubleStar:
                    MoveNextToken();
                    kwArg = ParseParam(isLambda);

                    if (CurrentTokenType is TokenType.Equal)
                        throw SyntaxError(PySR.InvalidSyntax_Parameters_VarKwArgWithDefault);

                    state = StateEnd;
                    break;

                case TokenType.Name:
                    var argNode = ParseParam(isLambda);

                    AstExprNode? defaultValue = null;
                    if (needDefault || CurrentTokenType is TokenType.Equal)
                    {
                        defaultValue = ParseDefault();
                        if (state is StateMaybePosonlyArgsOrArgs or StateArgs)
                            needDefault = true;
                    }

                    if (state is StateMaybePosonlyArgsOrArgs or StateArgs)
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
                    throw SyntaxError();
            }
        }
    }

    [GrammarSyntaxRule("expressions")]
    private List<AstExprNode> ParseExpressions(StopPredicate predicate, out Token? endsWithComma)
    {
        return ParseSomethingList(ParseExpression, predicate, out endsWithComma);
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

        if (CurrentTokenType is TokenType.Equal)
            throw SyntaxError(PySR.InvalidSyntax_Assignment_AssignToYield);

        var value = ParseStarExpressions(StopPredicates.UntilRightParenOrNewLineOrSemicolon);
        return Ast.Yield(value).With(metaInfo.WithPreviousEnd());
    }

    private List<T> ParseSomethingList<T>(Func<T> parse, StopPredicate predicate, out Token? endsWithComma, TokenType separator = TokenType.Comma)
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

        return list;
    }

    [GrammarSyntaxRule("star_target")]
    private AstExprNode ParseStarTarget()
    {
        if (CurrentTokenType is TokenType.Star)
        {
            MoveNextToken();
            var target = ParseStarTarget();
            if (target is StarredNode)
                throw SyntaxError(PySR.InvalidSyntax_StarredExpression_Invalid);
            return Ast.Starred(target);
        }
        else
        {
            var target = ParsePrimary();
            if (!target.IsValidTarget())
                throw SyntaxError(PySR.InvalidSyntax_InvalidTarget, AstUtils.GetExprNodeName(target));
            return target;
        }
    }

    [GrammarSyntaxRule("star_targets")]
    private AstExprNode ParseStarTargets(StopPredicate predicate)
    {
        var targets = ParseSomethingList(ParseStarTarget, predicate, out var endsWithComma);
        return UnwrapOrMakeTuple(targets, endsWithComma);
    }

    [GrammarSyntaxRule("for_if_clause")]
    private AstComprehensionNode ParseForIfClause()
    {
        var metaInfo = CreateAstMetaInfo();

        if (IsCurrentKeyword("async"))
            throw new NotSupportedException();

        EnsureKeywordThenMove("for");
        var target = ParseStarTargets(StopPredicates.UntilKeywordIn(this));
        EnsureKeywordThenMove("in", PySR.InvalidSyntax_ForStmt_ExpectedIn);

        var iter = ParseDisjunction();
        var ifs = new List<AstExprNode>();
        while (IsCurrentKeyword("if"))
        {
            MoveNextToken();
            ifs.Add(ParseDisjunction());
        }

        return Ast.Comprehension(target, iter, ifs).With(metaInfo.WithPreviousEnd());
    }

    [GrammarSyntaxRule("for_if_clauses")]
    private List<AstComprehensionNode> ParseForIfClauses()
    {
        List<AstComprehensionNode> generators = [ParseForIfClause()];
        while (IsCurrentKeyword("for"))
            generators.Add(ParseForIfClause());
        return generators;
    }

    private TComprehension ParseComp<TComprehension, TItem>(
        TokenType openToken, TokenType closeToken,
        Func<TItem> parseItem, Func<TItem, List<AstComprehensionNode>, TComprehension> factory)
        where TComprehension : AstNode
    {
        var metaInfo = CreateAstMetaInfo();
        EnsureTokenTypeThenMove(openToken);

        var elt = parseItem();
        var generators = ParseForIfClauses();
        EnsureTokenTypeThenMove(closeToken);

        return factory(elt, generators).With(metaInfo.WithPreviousEnd());
    }

    [GrammarSyntaxRule("listcomp")]
    private ListCompNode ParseListComp()
    {
        return ParseComp(TokenType.LeftSquareBracket, TokenType.RightSquareBracket,
            ParseNamedExpression, Ast.ListComp);
    }

    [GrammarSyntaxRule("setcomp")]
    private SetCompNode ParseSetComp()
    {
        return ParseComp(TokenType.LeftBrace, TokenType.RightBrace,
            ParseNamedExpression, Ast.SetComp);
    }

    [GrammarSyntaxRule("genexp")]
    private GeneratorExpNode ParseGenExp()
    {
        return ParseComp(TokenType.LeftParen, TokenType.RightParen,
            ParseNamedExpression, Ast.GeneratorExp);
    }

    [GrammarSyntaxRule("dictcomp")]
    private DictCompNode ParseDictComp()
    {
        return ParseComp(TokenType.LeftBrace, TokenType.RightBrace,
            ParseKvpair, Ast.DictComp);
    }

    private TSequence ParseSequence<TSequence, TItem>(
        TokenType openToken, TokenType closeToken,
        Func<TItem> parseItem, Func<IEnumerable<TItem>, TSequence> factory,
        bool allowSingleItemWithoutComma = true, bool allowEmptySequence = true)
        where TSequence : AstNode
    {
        var metaInfo = CreateAstMetaInfo();
        EnsureTokenTypeThenMove(openToken);

        if (CurrentTokenType == closeToken)
        {
            if (!allowEmptySequence)
                throw SyntaxError();

            MoveNextToken();
            return factory([]).With(metaInfo.WithPreviousEnd());
        }

        var list = ParseSomethingList(parseItem, StopPredicates.Until(closeToken), out var endsWithComma);
        if (!allowSingleItemWithoutComma && list.Count is 1 && endsWithComma is null)
            throw SyntaxError();

        EnsureTokenTypeThenMove(closeToken);
        return factory(list).With(metaInfo.WithPreviousEnd());
    }

    [GrammarSyntaxRule("list")]
    private ListNode ParseList()
    {
        return ParseSequence(TokenType.LeftSquareBracket, TokenType.RightSquareBracket,
            ParseStarNamedExpression, Ast.List);
    }

    [GrammarSyntaxRule("tuple")]
    private TupleNode ParseTuple()
    {
        return ParseSequence(TokenType.LeftParen, TokenType.RightParen,
            ParseStarNamedExpression, Ast.Tuple, allowSingleItemWithoutComma: false);
    }

    [GrammarSyntaxRule("set")]
    private SetNode ParseSet()
    {
        return ParseSequence(TokenType.LeftBrace, TokenType.RightBrace,
            ParseStarNamedExpression, Ast.Set, allowEmptySequence: false);
    }

    [GrammarSyntaxRule("dict")]
    private DictNode ParseDict()
    {
        return ParseSequence(TokenType.LeftBrace, TokenType.RightBrace,
            ParseDoubleStarredKvpair, Ast.Dict);
    }

    [GrammarSyntaxRule("double_starred_kvpair")]
    private KeyValuePair<AstExprNode?, AstExprNode> ParseDoubleStarredKvpair()
    {
        if (CurrentTokenType is not TokenType.DoubleStar)
            return ParseKvpair()!;

        MoveNextToken();
        var value = ParseBitwiseOr();
        return KeyValuePair.Create<AstExprNode?, AstExprNode>(key: null, value);
    }

    [GrammarSyntaxRule("kvpair")]
    private KeyValuePair<AstExprNode, AstExprNode> ParseKvpair()
    {
        var key = ParseExpression();
        EnsureTokenTypeThenMove(TokenType.Colon);
        var value = ParseExpression();
        return KeyValuePair.Create(key, value);
    }

    private AstExprNode ParseSequenceOrComprehension<TSequence, TComprehension, TItem>(
        TokenType openToken, TokenType closeToken,
        Func<TItem> parseItem, Func<TSequence> parseSequence, Func<TComprehension> parseComprehension)
        where TSequence : AstExprNode
        where TComprehension : AstExprNode
    {
        var pos = TokenPosition;
        EnsureTokenTypeThenMove(openToken);
        var isComp = TestIsComprehension(closeToken, parseItem);
        TokenPosition = pos;
        return isComp ? parseComprehension() : parseSequence();
    }

    [GrammarSyntaxRule("arguments")]
    private (IEnumerable<AstExprNode> Args, IEnumerable<AstKeywordNode> Kwargs) ParseArguments()
    {
        var result = ParseArgs(out _);
        EnsureTokenType(TokenType.RightParen, PySR.InvalidSyntax_RightParenNeverClosed);
        return result;
    }

    [GrammarSyntaxRule("args")]
    private (IEnumerable<AstExprNode> Args, IEnumerable<AstKeywordNode> Kwargs) ParseArgs(out Token? endsWithComma)
    {
        if (CurrentTokenType is TokenType.RightParen)
        {
            endsWithComma = null;
            return ([], []);
        }

        if (TestIsKwarg())
            return ParseKwargs(out endsWithComma);

        var args = ParseSomethingList(ParseStarredExpressionOrNamedExpression,
            _ => CurrentTokenType is TokenType.RightParen or TokenType.Equal || TestIsKwarg(), out endsWithComma);

        if (CurrentTokenType is TokenType.RightParen)
            return (args, []);

        if (CurrentTokenType is TokenType.Equal)
            throw SyntaxError(PySR.InvalidSyntax_Arguments_ExpressionContainsAssignment);

        var (restArgs, kwargs) = ParseKwargs(out endsWithComma);
        return (args.Concat(restArgs), kwargs);
    }

    private AstExprNode ParseStarredExpressionOrNamedExpression()
    {
        if (CurrentTokenType is TokenType.Star)
            return ParseStarredExpression();

        return ParseNamedExpression();
    }

    private bool TestIsKwarg()
    {
        if (CurrentTokenType is TokenType.DoubleStar)
            return true;

        if (!IsCurrentIdentifier)
            return false;

        var pos = TokenPosition;
        MoveNextToken();
        var isKwarg = CurrentTokenType is TokenType.Equal;
        TokenPosition = pos;

        return isKwarg;
    }

    [GrammarSyntaxRule("kwargs")]
    private (IEnumerable<StarredNode> Args, IEnumerable<AstKeywordNode> Kwargs) ParseKwargs(out Token? endsWithComma)
    {
        if (CurrentTokenType is TokenType.DoubleStar)
        {
            var kwargs = ParseSomethingList(ParseKwargOrDoubleStarred, StopPredicates.UntilRightParen, out endsWithComma);
            return ([], kwargs);
        }

        var list = ParseSomethingList(ParseKwargOrStarred, StopPredicates.UntilRightParenOrDoubleStar, out endsWithComma);
        if (CurrentTokenType is TokenType.RightParen)
            return (WhereNotNull(list.Select(static pair => pair.Arg)),
                WhereNotNull(list.Select(static pair => pair.Kwarg)));

        if (endsWithComma is null)
            throw SyntaxError();

        var kwlist = ParseSomethingList(ParseKwargOrDoubleStarred, StopPredicates.UntilRightParen, out endsWithComma);
        return (WhereNotNull(list.Select(static pair => pair.Arg)),
            WhereNotNull(list.Select(static pair => pair.Kwarg)).Concat(kwlist));

        static IEnumerable<T> WhereNotNull<T>(IEnumerable<T?> source)
        {
            return source.Where(static item => item is not null)!;
        }
    }

    private AstKeywordNode ParseNameKwarg()
    {
        if (CurrentTokenType is not TokenType.Name)
            throw SyntaxError(PySR.InvalidSyntax_Arguments_PosArgFollowsKeyword);

        var metaInfo = CreateAstMetaInfo();
        var arg = ParseNonMangledIdentifier();
        EnsureTokenTypeThenMove(TokenType.Equal, PySR.InvalidSyntax_Arguments_PosArgFollowsKeyword);
        var value = ParseExpression();
        return Ast.Keyword(arg, value).With(metaInfo.WithPreviousEnd());
    }

    [GrammarSyntaxRule("kwarg_or_starred")]
    private (StarredNode? Arg, AstKeywordNode? Kwarg) ParseKwargOrStarred()
    {
        if (CurrentTokenType is TokenType.Star)
            return (ParseStarredExpression(), null);

        return (null, ParseNameKwarg());
    }

    [GrammarSyntaxRule("kwarg_or_double_starred")]
    private AstKeywordNode ParseKwargOrDoubleStarred()
    {
        if (CurrentTokenType is not TokenType.DoubleStar)
            return ParseNameKwarg();

        var metaInfo = CreateAstMetaInfo();
        MoveNextToken();
        var value = ParseExpression();
        if (CurrentTokenType is TokenType.Equal)
            throw SyntaxError(PySR.InvalidSyntax_Arguments_AssignToKeywordArgumentUnpacking);
        return Ast.Keyword(arg: null, value).With(metaInfo.WithPreviousEnd());
    }

    [GrammarSyntaxRule("param_star_annotation")]
    private AstArgNode ParseParamStarAnnotation()
    {
        var metaInfo = CreateAstMetaInfo();
        var arg = ParseIdentifier();
        var annotation = TryParseStarAnnotation();
        return Ast.Arg(arg, annotation).With(metaInfo);

        AstExprNode? TryParseStarAnnotation()
        {
            if (CurrentTokenType is not TokenType.Colon)
                return null;

            return ParseStarAnnotation();
        }
    }

    [GrammarSyntaxRule("param")]
    [GrammarSyntaxRule("lambda_param")]
    private AstArgNode ParseParam(bool isLambda)
    {
        var metaInfo = CreateAstMetaInfo();
        var arg = ParseIdentifier();
        var annotation = TryParseAnnotation();
        return Ast.Arg(arg, annotation).With(metaInfo);

        AstExprNode? TryParseAnnotation()
        {
            if (isLambda)
                return null;

            if (CurrentTokenType is not TokenType.Colon)
                return null;

            return ParseAnnotation();
        }
    }

    [GrammarSyntaxRule("annotation")]
    private AstExprNode ParseAnnotation()
    {
        EnsureTokenTypeThenMove(TokenType.Colon);
        return ParseExpression();
    }

    [GrammarSyntaxRule("star_annotation")]
    private AstExprNode ParseStarAnnotation()
    {
        EnsureTokenTypeThenMove(TokenType.Colon);
        return ParseStarExpression();
    }

    [GrammarSyntaxRule("default")]
    private AstExprNode ParseDefault()
    {
        EnsureTokenTypeThenMove(TokenType.Equal, PySR.InvalidSyntax_Parameters_ParameterWithoutDefault);

        if (CurrentTokenType is TokenType.RightParen or TokenType.Colon)
            throw SyntaxError(PySR.InvalidSyntax_Parameters_ExpectedDefault);

        return ParseExpression();
    }
}
