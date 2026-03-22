using PySharp.Compilation.CodeAnalysis;
using PySharp.Runtime;
using PySharp.Runtime.Calls;
using System.Diagnostics;
using System.Numerics;
using System.Text.RegularExpressions;

namespace PySharp.Compilation.Tokenization;

public sealed partial class Lexer : ICodeMetaInfoProvider
{
    private enum LexerState : byte
    {
        Unknown = 0,

        Default,
        TokenizingMultiLineSingleOrDoubleString,
        TokenizingTripleString,

        FStringMiddle,
        FStringDefault,
    }

    public static TokenSequence Tokenize(PyCallContext context, CodeSource codeSource, bool extraNewLine = false)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(codeSource);

        var lexer = new Lexer(context, codeSource);
        lexer.InternalStart();
        lexer.InternalTokenize();
        lexer.InternalEnd();
        if (extraNewLine)
            lexer._tokens.Insert(lexer._tokens.Count - 1, new Token(TokenType.NewLine, default, codeSource));
        return new TokenSequence(lexer._tokens);
    }

    private struct FStringInfo
    {
        public LexerState State;
        public char WrapperChar { get; }
        public bool IsTriple { get; }
        public bool IsTemplate { get; }
        public int ParenLevelWhenEntering { get; }
        public Stack<int> FormatSpec { get; }
        public readonly int WrapperLength => IsTriple ? 3 : 1;

        public FStringInfo(bool isTemplate, char wrapperChar, bool isTriple, int parenLevelWhenEntering)
        {
            IsTemplate = isTemplate;
            FormatSpec = [];
            State = LexerState.FStringMiddle;
            WrapperChar = wrapperChar;
            IsTriple = isTriple;
            ParenLevelWhenEntering = parenLevelWhenEntering;
        }
    }

    private readonly Stack<FStringInfo> _fstringStack;
    // for the root FStringInfo, only State are used
    private FStringInfo CurrentFStringInfo;
    private ref LexerState CurrentState => ref CurrentFStringInfo.State;
    private char _wrapper;

    private readonly PyCallContext _context;
    private readonly CodeSource _codeSource;

    private readonly List<Token> _tokens;
    private int _offset;
    private bool _explicitLineJoining;
    private readonly Stack<int> _indentationLevels;

    private bool _needIndentation;

    private int _stringStartOffset;

    private int _parenLevel;

    CodeMetaInfo? ICodeMetaInfoProvider.MetaInfo => CodeMetaInfo.FromPosition(_codeSource, new(Lineno, 0));
    private int Lineno => _codeSource.Code.OffsetToPosition(_offset).Line;

    internal Lexer(PyCallContext context, CodeSource codeSource)
    {
        _context = context;
        _codeSource = codeSource;
        _tokens = new List<Token>(GetTokensDefaultCapacity(codeSource.Code.Text.Length));
        _offset = 0;
        _explicitLineJoining = false;
        _indentationLevels = [];
        _indentationLevels.Push(0);
        _parenLevel = 0;
        _fstringStack = [];
        CurrentState = LexerState.Default;
    }

    private static int GetTokensDefaultCapacity(int charsLength)
    {
        Debug.Assert(charsLength >= 0);
        return (int)BitOperations.RoundUpToPowerOf2((uint)charsLength / 4);
    }

    public PyRuntimeException SyntaxError(string message = PySR.InvalidSyntax, params ReadOnlySpan<object?> args)
    {
        return _context.SyntaxError(this, message, args);
    }

    internal void InternalStart()
    {
        AppendToken(TokenType.Encoding, length: 0);
        _needIndentation = true;
    }

    internal void InternalClearIndentation()
    {
        while (_indentationLevels.Count > 1)
        {
            _ = _indentationLevels.Pop();
            AppendToken(TokenType.Dedent, 0);
        }
    }

    internal void InternalEnd()
    {
        if (CurrentState is LexerState.TokenizingMultiLineSingleOrDoubleString)
            throw SyntaxError(PySR.InvalidSyntax_Tokenize_Unterminated_StringLiteral, Lineno);

        if (CurrentState is LexerState.TokenizingTripleString)
            throw SyntaxError(PySR.InvalidSyntax_Tokenize_Unterminated_TripleStringLiteral, Lineno);

        Debug.Assert(_tokens.Count > 0);

        if (_tokens[^1].Type is not (TokenType.NewLine or TokenType.NL))
        {
            AppendNewLineToken(length: 0);
        }

        InternalClearIndentation();

        AppendToken(TokenType.EndMarker, length: 0);
    }

    private void EnterFString(FStringInfo info)
    {
        _fstringStack.Push(CurrentFStringInfo);
        CurrentFStringInfo = info;
    }

    private void ExitFString()
    {
        CurrentFStringInfo = _fstringStack.Pop();
    }

    internal void InternalTokenize()
    {
        var content = _codeSource.Code.Text.AsSpan();

        _offset = 0;
        while (_offset < content.Length)
        {
            switch (CurrentState)
            {
                case LexerState.TokenizingMultiLineSingleOrDoubleString:
                    TokenizeMultiLineSingleOrDoubleString(content);
                    break;

                case LexerState.TokenizingTripleString:
                    TokenizeTripleString(content);
                    break;

                case LexerState.Default:
                    {
                        TokenizeToken(content, out var group);
                        Debug.Assert(group.Index == _offset);
                        _offset += group.Length;
                    }
                    break;

                case LexerState.FStringDefault:
                    {
                        TokenizeToken(content, out var group);
                        Debug.Assert(group.Index == _offset);
                        _offset += group.Length;
                    }

                    var lastToken = _tokens[^1];
                    if (CurrentFStringInfo.ParenLevelWhenEntering == _parenLevel)
                    {
                        if (lastToken.Type is TokenType.RightBrace)
                            CurrentState = LexerState.FStringMiddle;
                    }
                    else
                    {
                        if (lastToken.Type is TokenType.Colon && _parenLevel == CurrentFStringInfo.ParenLevelWhenEntering + 1)
                        {
                            // TODO: too deep

                            CurrentState = LexerState.FStringMiddle;
                            CurrentFStringInfo.FormatSpec.Push(_parenLevel);
                        }
                        else if (lastToken.Type is TokenType.RightBrace &&
                            CurrentFStringInfo.FormatSpec.Count > 0 &&
                            CurrentFStringInfo.FormatSpec.Peek() == _parenLevel)
                        {
                            CurrentState = LexerState.FStringMiddle;
                        }
                    }
                    break;

                case LexerState.FStringMiddle:
                    static int FindNextSpecialChar(ReadOnlySpan<char> content, FStringInfo info, int offset, out char c)
                    {
                        for (int i = offset; i < content.Length; i++)
                        {
                            c = content[i];
                            if (c is '{' or '}')
                                // TODO: f'\{0}' (invalid escape) it is need to warn (here or somewhere)
                                return i;

                            if (c is '\\')
                            {
                                i++;
                                continue;
                            }

                            if (c != info.WrapperChar)
                                continue;

                            if (!info.IsTriple || content[i..].StartsWith([info.WrapperChar, info.WrapperChar, info.WrapperChar]))
                                return i;
                        }

                        c = default;
                        return -1;
                    }

                    var info = CurrentFStringInfo;
                    var indexOfChar = FindNextSpecialChar(content, info, _offset, out var c);
                    if (indexOfChar is -1)
                        throw SyntaxError(PySR.InvalidSyntax_Tokenize_Unterminated_TripleFStringLiteral, Lineno);

                    if (c == info.WrapperChar)
                    {
                        if (CurrentFStringInfo.FormatSpec.Count > 0)
                            throw SyntaxError(PySR.InvalidSyntax_FString_ReplacementField_ExpectingRightBraceOrSpecs);

                        AppendToken(info.IsTemplate ? TokenType.TStringMiddle : TokenType.FStringMiddle, indexOfChar - _offset);
                        _offset = indexOfChar;
                        AppendToken(info.IsTemplate ? TokenType.TStringEnd : TokenType.FStringEnd, info.WrapperLength);
                        _offset = indexOfChar + info.WrapperLength;
                        ExitFString();
                    }
                    else if (c is '{')
                    {
                        bool isEscape = CurrentFStringInfo.FormatSpec.Count is 0 && indexOfChar + 1 < content.Length && content[indexOfChar + 1] is '{';
                        var nextOffset = indexOfChar + (isEscape ? 1 : 0);
                        AppendToken(info.IsTemplate ? TokenType.TStringMiddle : TokenType.FStringMiddle, nextOffset - _offset);
                        _offset = nextOffset;

                        if (isEscape)
                        {
                            // escape '{'
                            // one is consumed by nextOffset
                            // the other one is here
                            _offset += 1;
                        }
                        else
                        {
                            AppendToken(TokenType.LeftBrace, length: 1);
                            _offset = indexOfChar + 1;
                            CurrentState = LexerState.FStringDefault;

                            // increment _parenLevel here
                            // while other operators including RightBrace
                            // should be processed by TokenizePseudoToken
                            _parenLevel++;
                        }
                    }
                    else if (c is '}')
                    {
                        if (CurrentFStringInfo.FormatSpec.Count > 0)
                        {
                            AppendToken(info.IsTemplate ? TokenType.TStringMiddle : TokenType.FStringMiddle, indexOfChar - _offset);
                            _offset = indexOfChar;
                            AppendToken(TokenType.RightBrace, length: 1);
                            _offset = indexOfChar + 1;
                            CurrentFStringInfo.FormatSpec.Pop();
                            _parenLevel--;
                            break;
                        }
                        else
                        {
                            if (indexOfChar + 1 < content.Length && content[indexOfChar + 1] is not '}')
                                throw SyntaxError(PySR.InvalidSyntax_Tokenize_FStringSingleRightBrace);

                            AppendToken(info.IsTemplate ? TokenType.TStringMiddle : TokenType.FStringMiddle, indexOfChar + 1 /* included escape '}' */ - _offset);
                            _offset = indexOfChar + 2;
                        }
                    }
                    else
                    {
                        throw new UnreachableException();
                    }
                    break;

                default:
                    throw new UnreachableException();
            }
        }
    }

    private bool IsStrictMatchFromCurrent(ReadOnlySpan<char> content, Regex regex, out ValueGroup group)
    {
        group = default;

        if (!TryMatch(regex, content[_offset..], offset: 0, out var match))
            return false;

        if (match.Index is not 0)
            return false;

        group.Index = match.Index + _offset;
        group.Length = match.Length;
        group.Value = content.Slice(group.Index, group.Length);
        return true;
    }

    private bool IsIgnored(ReadOnlySpan<char> content, int indentationLevel)
    {
        var span = content[(_offset + indentationLevel)..];
        return span.Length is 0 || span[0] is '#' or '\n' or '\r';
    }

    private static ReadOnlySpan<char> GetStringPrefix(ReadOnlySpan<char> str, out char firstWrapper)
    {
        Debug.Assert(str.Length >= 2);

        if (str[0] is '\'' or '"')
        {
            firstWrapper = str[0];
            return [];
        }

        if (str[1] is '\'' or '"')
        {
            firstWrapper = str[1];
            return str[..1];
        }

        Debug.Assert(str.Length >= 3);
        firstWrapper = str[2];
        return str[..2];
    }

    private void AppendToken(TokenType type, CodeTextSpan span)
    {
        var token = new Token(
            type,
            span,
            _codeSource);
        _tokens.Add(token);
    }

    private void AppendToken(TokenType type, int length)
    {
        AppendToken(type, new CodeTextSpan(_offset, length));
    }

    private void AppendNewLineToken(int length)
    {
        bool isNewLine = false;
        Debug.Assert(_parenLevel >= 0);
        if (_parenLevel is 0)
        {
            for (int i = _tokens.Count - 1; i >= 0; i--)
            {
                var type = _tokens[i].Type;
                if (type is TokenType.NewLine or TokenType.NL)
                    break;

                if (type is TokenType.Comment or TokenType.Encoding)
                    continue;

                isNewLine = true;
                break;
            }
        }

        AppendToken(isNewLine ? TokenType.NewLine : TokenType.NL, length);
    }

    private static bool TryMatch(Regex regex, ReadOnlySpan<char> content, int offset, out ValueMatch match)
    {
        var enumerator = regex.EnumerateMatches(content, offset);
        if (!enumerator.MoveNext())
        {
            match = default;
            return false;
        }

        match = enumerator.Current;
        return true;
    }

    private void TokenizeMultiLineString(ReadOnlySpan<char> content, Regex wrapper, bool isTriple)
    {
        if (!TryMatch(wrapper, content, _offset, out var m))
        {
            if (isTriple)
                throw SyntaxError(PySR.InvalidSyntax_Tokenize_Unterminated_TripleStringLiteral, Lineno);

            throw SyntaxError(PySR.InvalidSyntax_Tokenize_Unterminated_StringLiteral, Lineno);
        }

        var endOffset = m.Index + m.Length;
        AppendToken(TokenType.String, new CodeTextSpan(_stringStartOffset, endOffset - _stringStartOffset));
        _offset = endOffset;
        CurrentState = LexerState.Default;
    }

    private void TokenizeMultiLineSingleOrDoubleString(ReadOnlySpan<char> content)
    {
        Debug.Assert(CurrentState is LexerState.TokenizingMultiLineSingleOrDoubleString);
        Debug.Assert(_wrapper is '\'' or '"');

        TokenizeMultiLineString(content, _wrapper is '"' ? LexerRegexes.Double : LexerRegexes.Single, false);
    }

    private void TokenizeTripleString(ReadOnlySpan<char> content)
    {
        Debug.Assert(CurrentState is LexerState.TokenizingTripleString);
        Debug.Assert(_wrapper is '\'' or '"');

        TokenizeMultiLineString(content, _wrapper is '"' ? LexerRegexes.Double3 : LexerRegexes.Single3, true);
    }

    private void EnsureIndentation(ReadOnlySpan<char> content, int indentationLevel)
    {
        if (!_needIndentation)
            return;

        _needIndentation = false;

        if (_explicitLineJoining)
        {
            _explicitLineJoining = false;
            return;
        }

        if (_parenLevel is not 0 || IsIgnored(content, indentationLevel))
            return;

        if (indentationLevel > _indentationLevels.Peek())
        {
            _indentationLevels.Push(indentationLevel);
            AppendToken(TokenType.Indent, indentationLevel);
            return;
        }

        while (_indentationLevels.Peek() > indentationLevel)
        {
            _ = _indentationLevels.Pop();
            AppendToken(TokenType.Dedent, length: 0);
        }

        if (indentationLevel != _indentationLevels.Peek())
            throw _context.IndentationError(PySR.InvalidSyntax_Tokenize_UnindentNotMatch);
    }

    private ref struct ValueGroup
    {
        public ReadOnlySpan<char> Value;
        public int Index;
        public int Length;
    }

    private int GetWhitespaceCount(ReadOnlySpan<char> content)
    {
        var span = content[_offset..];
        for (int i = 0; i < span.Length; i++)
        {
            if (span[i] is ' ' or '\t' or '\f')
                continue;

            return i;
        }
        return span.Length;
    }

    private void TokenizePseudoExtras(ValueGroup group)
    {
        if (group.Length is 0)
        {
        }
        else if (group.Value.StartsWith('\\'))
        {
            _explicitLineJoining = true;
            _needIndentation = true;
        }
        else if (group.Value[0] is '#')
        {
            AppendToken(TokenType.Comment, group.Length);
        }
        else
        {
            var prefix = GetStringPrefix(group.Value, out _wrapper);
            var isFString = prefix.ContainsAny('f', 'F');
            var isTString = prefix.ContainsAny('t', 'T');

            if (isFString || isTString)
            {
                AppendToken(isTString ? TokenType.TStringStart : TokenType.FStringStart, group.Length);
                EnterFString(new FStringInfo(isTString, _wrapper, isTriple: true, parenLevelWhenEntering: _parenLevel));
                return;
            }

            _stringStartOffset = _offset;
            CurrentState = LexerState.TokenizingTripleString;
        }
    }

    private void TokenizeFunny(ValueGroup group)
    {
        if (group.Value is "\r\n" or "\n")
        {
            AppendNewLineToken(group.Length);
            _needIndentation = true;
        }
        else
        {
            AppendToken(TokenType.Operator, group.Length);
            if (group.Value is "(" or "[" or "{")
                _parenLevel++;
            else if (group.Value is ")" or "]" or "}")
                _parenLevel--;
        }
    }

    private void TokenizeContStr(ref ValueGroup group)
    {
        var prefix = GetStringPrefix(group.Value, out _wrapper);
        var isFString = prefix.ContainsAny('f', 'F');
        var isTString = prefix.ContainsAny('t', 'T');

        if (isFString || isTString)
        {
            group.Length = prefix.Length + 1 /* len of wrapper */;
            AppendToken(isTString ? TokenType.TStringStart : TokenType.FStringStart, group.Length);
            EnterFString(new FStringInfo(isTString, _wrapper, isTriple: false, parenLevelWhenEntering: _parenLevel));
            return;
        }

        if (group.Value.EndsWith("\\\r\n") || group.Value.EndsWith("\\\n"))
        {
            _stringStartOffset = _offset;
            CurrentState = LexerState.TokenizingMultiLineSingleOrDoubleString;
        }
        else
        {
            AppendToken(TokenType.String, group.Length);
        }

    }

    private bool TryTokenizeSingleFString(ReadOnlySpan<char> content, out ValueGroup group)
    {
        Debug.Assert(_offset < content.Length);
        Debug.Assert(content[_offset] is 'b' or 'B' or 'f' or 'F' or 't' or 'T' or 'r' or 'R' or 'u' or 'U');

        var searchLength = Math.Min(3 /* max len of prefix (2) + len of wrapper (1) */, content.Length - _offset);
        var span = content.Slice(_offset, searchLength);
        var indexOfWrapper = span.IndexOfAny('\'', '"');

        group = default;

        if (indexOfWrapper is -1)
            return false;

        var prefix = span[..indexOfWrapper];
        var isFString = prefix.ContainsAny('f', 'F');
        var isTString = prefix.ContainsAny('t', 'T');

        if (isFString || isTString)
        {
            group.Index = _offset;
            group.Length = prefix.Length + 1 /* len of wrapper */;
            AppendToken(isTString ? TokenType.TStringStart : TokenType.FStringStart, group.Length);
            EnterFString(new FStringInfo(isTString, span[indexOfWrapper], isTriple: false, parenLevelWhenEntering: _parenLevel));
            return true;
        }

        return false;
    }

    private void TokenizeFallback(ReadOnlySpan<char> content, out ValueGroup group)
    {
        if (content[_offset] is '\r')
        {
            AppendNewLineToken(length: 1);
            _needIndentation = true;
            group.Index = _offset;
            group.Length = 1;
            group.Value = content.Slice(group.Index, group.Length);
            return;
        }

        throw SyntaxError();
    }

    private void TokenizeToken(ReadOnlySpan<char> content, out ValueGroup group)
    {
        var indentationLevel = GetWhitespaceCount(content);
        EnsureIndentation(content, indentationLevel);
        _offset += indentationLevel;

        if (_offset >= content.Length)
        {
            group = default;
            group.Index = _offset;
            return;
        }
        var c = content[_offset];

        switch (c)
        {
            case '\\':
            case '#':
                if (IsStrictMatchFromCurrent(content, LexerRegexes.StartsWithPseudoExtras, out group))
                    TokenizePseudoExtras(group);
                else
                    throw SyntaxError();
                break;

            case '\'':
            case '"':
                if (IsStrictMatchFromCurrent(content, LexerRegexes.StartsWithPseudoExtras, out group))
                    TokenizePseudoExtras(group);
                else if (IsStrictMatchFromCurrent(content, LexerRegexes.StartsWithContStr, out group))
                    TokenizeContStr(ref group);
                else
                    throw SyntaxError();
                break;

            case 'b':
            case 'B':
            case 'f':
            case 'F':
            case 't':
            case 'T':
            case 'r':
            case 'R':
            case 'u':
            case 'U':
                if (IsStrictMatchFromCurrent(content, LexerRegexes.StartsWithPseudoExtras, out group))
                    TokenizePseudoExtras(group);
                else if (IsStrictMatchFromCurrent(content, LexerRegexes.StartsWithContStr, out group))
                    TokenizeContStr(ref group);
                else if (TryTokenizeSingleFString(content, out group))
                { }
                else if (IsStrictMatchFromCurrent(content, LexerRegexes.StartsWithName, out group))
                    AppendToken(TokenType.Name, group.Length);
                else
                    throw SyntaxError();
                break;

            case >= '0' and <= '9':
                if (IsStrictMatchFromCurrent(content, LexerRegexes.StartsWithNumber, out group))
                    AppendToken(TokenType.Number, group.Length);
                else
                    throw SyntaxError();
                break;

            case '.':
                if (IsStrictMatchFromCurrent(content, LexerRegexes.StartsWithNumber, out group))
                    AppendToken(TokenType.Number, group.Length);
                else if (IsStrictMatchFromCurrent(content, LexerRegexes.StartsWithFunny, out group))
                    TokenizeFunny(group);
                else
                    throw SyntaxError();
                break;

            case '\r':
            case '\n':
            case '~':
            case '}':
            case '|':
            case '{':
            case '^':
            case ']':
            case '[':
            case '@':
            case '>':
            case '=':
            case '<':
            case ';':
            case ':':
            case '/':
            case '-':
            case ',':
            case '+':
            case '*':
            case ')':
            case '(':
            case '&':
            case '%':
            case '!':
                if (IsStrictMatchFromCurrent(content, LexerRegexes.StartsWithFunny, out group))
                    TokenizeFunny(group);
                else
                    TokenizeFallback(content, out group);
                break;

            default:
                if (IsStrictMatchFromCurrent(content, LexerRegexes.StartsWithName, out group))
                    AppendToken(TokenType.Name, group.Length);
                else
                    TokenizeFallback(content, out group);
                break;
        }
    }
}
