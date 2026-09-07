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
    private readonly Stack<(int Col, int AltCol)> _indentationLevels;

    private bool _needIndentation;

    private int _stringStartOffset;

    private readonly Stack<(char Bracket, int Offset)> _bracketStack;
    private int ParenLevel => _bracketStack.Count;

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
        _indentationLevels.Push((Col: 0, AltCol: 0));
        _bracketStack = [];
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
        // reject null bytes before tokenization, wherever they occur
        // (CPython checks each line as it is read in)
        var nullIndex = _codeSource.Code.Text.IndexOf('\0');
        if (nullIndex is not -1)
        {
            _offset = nullIndex;
            throw SyntaxError(PySR.InvalidSyntax_Tokenize_SourceNullBytes);
        }

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

        // the innermost unclosed opener wins, exactly like CPython reading
        // parenstack[level-1] on EOF
        if (_bracketStack.TryPeek(out var tuple))
            throw BracketError(tuple.Offset, PySR.InvalidSyntax_ParenNeverClosed, tuple.Bracket);

        Debug.Assert(_tokens.Count > 0);

        if (_tokens[^1].Type is not (TokenType.NewLine or TokenType.NL))
            AppendNewLineToken(length: 0);

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
                    if (CurrentFStringInfo.ParenLevelWhenEntering == ParenLevel)
                    {
                        if (lastToken.Type is TokenType.RightBrace)
                            CurrentState = LexerState.FStringMiddle;
                    }
                    else
                    {
                        if (lastToken.Type is TokenType.Colon && ParenLevel == CurrentFStringInfo.ParenLevelWhenEntering + 1)
                        {
                            // TODO: too deep

                            CurrentState = LexerState.FStringMiddle;
                            CurrentFStringInfo.FormatSpec.Push(ParenLevel);
                        }
                        else if (lastToken.Type is TokenType.RightBrace &&
                            CurrentFStringInfo.FormatSpec.Count > 0 &&
                            CurrentFStringInfo.FormatSpec.Peek() == ParenLevel)
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
                    {
                        var message = info.IsTemplate
                            ? info.IsTriple
                                ? PySR.InvalidSyntax_Tokenize_Unterminated_TripleTStringLiteral
                                : PySR.InvalidSyntax_Tokenize_Unterminated_TStringLiteral
                            : info.IsTriple
                                ? PySR.InvalidSyntax_Tokenize_Unterminated_TripleFStringLiteral
                                : PySR.InvalidSyntax_Tokenize_Unterminated_FStringLiteral;
                        throw SyntaxError(message, Lineno);
                    }

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
                            ThrowIfTooManyNestedParentheses();

                            AppendToken(TokenType.LeftBrace, length: 1);
                            _offset = indexOfChar + 1;
                            CurrentState = LexerState.FStringDefault;

                            // the replacement-field brace bypasses TokenizeFunny,
                            // so it is pushed here while other operators including
                            // RightBrace are processed by TokenizePseudoToken
                            _bracketStack.Push(('{', indexOfChar));
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
                            _bracketStack.Pop();
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
        if (ParenLevel is 0)
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

    private void EnsureIndentation(ReadOnlySpan<char> content, int whitespaceLength, int col, int altcol)
    {
        if (!_needIndentation)
            return;

        _needIndentation = false;

        if (_explicitLineJoining)
        {
            _explicitLineJoining = false;
            return;
        }

        if (ParenLevel is not 0 || IsIgnored(content, whitespaceLength))
            return;

        var (topCol, topAltCol) = _indentationLevels.Peek();

        if (col == topCol)
        {
            // same width reached with a different mix of tabs and spaces
            if (altcol != topAltCol)
                throw _context.TabError(this, PySR.InvalidSyntax_Tokenize_InconsistentTabsAndSpaces);
        }
        else if (col > topCol)
        {
            // the count includes the baseline level, so at most
            // MaxIndent - 1 nesting levels are allowed
            if (_indentationLevels.Count >= MaxIndent)
                throw _context.IndentationError(this, PySR.InvalidSyntax_Tokenize_TooManyIndentLevels);

            // a deeper level must also advance the tab-insensitive column
            if (altcol <= topAltCol)
                throw _context.TabError(this, PySR.InvalidSyntax_Tokenize_InconsistentTabsAndSpaces);

            _indentationLevels.Push((col, altcol));
            AppendToken(TokenType.Indent, whitespaceLength);
        }
        else
        {
            while (_indentationLevels.Peek().Col > col)
            {
                _ = _indentationLevels.Pop();
                AppendToken(TokenType.Dedent, length: 0);
            }

            if (col != _indentationLevels.Peek().Col)
                throw _context.IndentationError(this, PySR.InvalidSyntax_Tokenize_UnindentNotMatch);

            if (altcol != _indentationLevels.Peek().AltCol)
                throw _context.TabError(this, PySR.InvalidSyntax_Tokenize_InconsistentTabsAndSpaces);
        }
    }

    private ref struct ValueGroup
    {
        public ReadOnlySpan<char> Value;
        public int Index;
        public int Length;
    }

    private const int TabSize = 8;
    private const int MaxIndent = 100;
    private const int MaxParenLevel = 100;

    // Returns the physical whitespace length and, for logical line
    // starts, the column pair used for indentation comparison:
    // a space advances both columns, a tab rounds col up to the next
    // multiple of TabSize while advancing altcol by one, and a form
    // feed resets both to zero.
    private int GetIndentation(ReadOnlySpan<char> content, out int col, out int altcol)
    {
        col = 0;
        altcol = 0;
        var span = content[_offset..];
        for (int i = 0; i < span.Length; i++)
        {
            switch (span[i])
            {
                case ' ':
                    col++;
                    altcol++;
                    break;

                case '\t':
                    col = (col / TabSize + 1) * TabSize;
                    altcol++;
                    break;

                case '\f':
                    col = 0;
                    altcol = 0;
                    break;

                default:
                    return i;
            }
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
                EnterFString(new FStringInfo(isTString, _wrapper, isTriple: true, parenLevelWhenEntering: ParenLevel));
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
            {
                ThrowIfTooManyNestedParentheses();
                _bracketStack.Push((group.Value[0], _offset));
            }
            else if (group.Value is ")" or "]" or "}")
            {
                if (ParenLevel is 0)
                    throw SyntaxError($"unmatched '{group.Value}'");

                var (opening, openingOffset) = _bracketStack.Pop();
                ThrowIfBracketMismatch(opening, openingOffset, group.Value[0]);
            }
        }
    }

    // Mirrors CPython's MAXLEVEL check (Parser/lexer/lexer.c): all bracket
    // types share one counter, and the opener that would exceed the limit
    // is rejected before it is pushed
    private void ThrowIfTooManyNestedParentheses()
    {
        if (ParenLevel >= MaxParenLevel)
            throw SyntaxError(PySR.InvalidSyntax_Tokenize_TooManyNestedParentheses);
    }

    // Mirrors CPython's parenstack bookkeeping (Parser/lexer/lexer.c): a
    // closing bracket must pair with the innermost opener, and an opener
    // still on the stack at EOF is reported at its own position
    private void ThrowIfBracketMismatch(char opening, int openingOffset, char closing)
    {
        if ((opening is '(' && closing is ')') ||
            (opening is '[' && closing is ']') ||
            (opening is '{' && closing is '}'))
            return;

        var openingLine = _codeSource.Code.OffsetToPosition(openingOffset).Line;
        if (openingLine != Lineno)
            throw BracketError(_offset, PySR.InvalidSyntax_ParenMismatchOnLine, closing, opening, openingLine);

        throw BracketError(_offset, PySR.InvalidSyntax_ParenMismatch, closing, opening);
    }

    private PyRuntimeException BracketError(int offset, string message, params ReadOnlySpan<object?> args)
    {
        var info = CodeMetaInfo.FromPosition(
            _codeSource,
            _codeSource.Code.OffsetToPosition(offset),
            _codeSource.Code.OffsetToPosition(offset + 1));
        return _context.SyntaxError(new PositionMetaInfo(info), message, args);
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
            EnterFString(new FStringInfo(isTString, _wrapper, isTriple: false, parenLevelWhenEntering: ParenLevel));
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

        Debug.Assert(indexOfWrapper is -1 or 1 or 2);

        if (indexOfWrapper is -1)
            return false;

        var isTString = span[0] is 't' or 'T';
        var isFirstFOrT = isTString || span[0] is 'f' or 'F';

        if (indexOfWrapper is 1)
        {
            if (!isFirstFOrT)
                return false;
        }
        else
        {
            char other = span[1];
            if (!isFirstFOrT)
            {
                if (other is not ('f' or 'F' or 't' or 'T'))
                    return false;

                isTString = other is 't' or 'T';
                other = span[0];
            }

            if (other is not ('r' or 'R'))
                return false;
        }

        var prefix = span[..indexOfWrapper];

        group.Index = _offset;
        group.Length = prefix.Length + 1 /* len of wrapper */;
        AppendToken(isTString ? TokenType.TStringStart : TokenType.FStringStart, group.Length);
        EnterFString(new FStringInfo(isTString, span[indexOfWrapper], isTriple: false, parenLevelWhenEntering: ParenLevel));
        return true;
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
        var whitespaceLength = GetIndentation(content, out var col, out var altcol);
        EnsureIndentation(content, whitespaceLength, col, altcol);
        _offset += whitespaceLength;

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
                {
                    AppendToken(TokenType.Number, group.Length);
                    VerifyEndOfNumber(content, group);
                }
                else
                {
                    throw SyntaxError();
                }
                break;

            case '.':
                if (IsStrictMatchFromCurrent(content, LexerRegexes.StartsWithNumber, out group))
                {
                    AppendToken(TokenType.Number, group.Length);
                    VerifyEndOfNumber(content, group);
                }
                else if (IsStrictMatchFromCurrent(content, LexerRegexes.StartsWithFunny, out group))
                {
                    TokenizeFunny(group);
                }
                else
                {
                    throw SyntaxError();
                }
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

    // Mirrors CPython's verify_end_of_number (Parser/lexer/lexer.c): a
    // committed 0o/0x/0b prefix that matched no digits is an error (the
    // regex alternation would otherwise fall back to a bare decimal "0"),
    // and a number directly followed by an identifier character is either
    // warned about (keyword continuation) or rejected.
    private void VerifyEndOfNumber(ReadOnlySpan<char> content, ValueGroup group)
    {
        if (group.Length is 1 && content[_offset] is '0' && _offset + 1 < content.Length)
        {
            switch (content[_offset + 1])
            {
                case 'o' or 'O':
                    throw SyntaxError(PrefixedLiteralMessage(content, PySR.InvalidSyntax_Tokenize_InvalidDigitInOctalLiteral, PySR.InvalidSyntax_Tokenize_InvalidOctalLiteral));
                case 'b' or 'B':
                    throw SyntaxError(PrefixedLiteralMessage(content, PySR.InvalidSyntax_Tokenize_InvalidDigitInBinaryLiteral, PySR.InvalidSyntax_Tokenize_InvalidBinaryLiteral));
                case 'x' or 'X':
                    throw SyntaxError(PySR.InvalidSyntax_Tokenize_InvalidHexadecimalLiteral);
            }
        }

        var end = _offset + group.Length;
        if (end >= content.Length)
            return;

        var c = content[end];
        if (!IsAsciiIdentifierChar(c))
            return;

        var message = InvalidLiteralMessage(group.Value);
        if (IsKeywordContinuation(content[end..]))
        {
            // the exact literal position keeps several offending literals on
            // one line distinct in the per-position warning dedup
            var info = CodeMetaInfo.FromPosition(_codeSource, _codeSource.Code.OffsetToPosition(_offset));
            _ = _context.WarnSyntax(message, new PositionMetaInfo(info)).PyUnwrap(_context);
            return;
        }

        throw SyntaxError(message);
    }

    // a digit right after a failed 0o/0b prefix is out of range for the
    // base (8/9 octal, 2-9 binary), which CPython reports per digit
    private string PrefixedLiteralMessage(ReadOnlySpan<char> content, string digitFormat, string plainMessage)
    {
        var index = _offset + 2;
        return index < content.Length && char.IsAsciiDigit(content[index])
            ? PySR.Format(digitFormat, content[index])
            : plainMessage;
    }

    private static string InvalidLiteralMessage(ReadOnlySpan<char> value)
    {
        if (value.Length >= 2 && value[0] is '0')
        {
            if (value[1] is 'x' or 'X')
                return PySR.InvalidSyntax_Tokenize_InvalidHexadecimalLiteral;
            if (value[1] is 'o' or 'O')
                return PySR.InvalidSyntax_Tokenize_InvalidOctalLiteral;
            if (value[1] is 'b' or 'B')
                return PySR.InvalidSyntax_Tokenize_InvalidBinaryLiteral;
        }

        return value[^1] is 'j' or 'J'
            ? PySR.InvalidSyntax_Tokenize_InvalidImaginaryLiteral
            : PySR.InvalidSyntax_Tokenize_InvalidDecimalLiteral;
    }

    // CPython rejects only ASCII identifier chars here (verify_end_of_number
    // guards with c < 128); wider chars are left to the parser
    private static bool IsAsciiIdentifierChar(char c)
        => char.IsAsciiLetterOrDigit(c) || c is '_';

    // and/else/for/or/not must be followed by a non-identifier char, while
    // if/in/is are matched on the single following char only (CPython's
    // lookahead asymmetry in verify_end_of_number)
    private static bool IsKeywordContinuation(ReadOnlySpan<char> rest)
    {
        switch (rest[0])
        {
            case 'a':
                return Matches(rest, "and");
            case 'e':
                return Matches(rest, "else");
            case 'f':
                return Matches(rest, "for");
            case 'i':
                return rest.Length >= 2 && rest[1] is 'f' or 'n' or 's';
            case 'n':
                return Matches(rest, "not");
            case 'o':
                return Matches(rest, "or");
            default:
                return false;
        }

        static bool Matches(ReadOnlySpan<char> rest, ReadOnlySpan<char> keyword)
            => rest.StartsWith(keyword) &&
               (rest.Length == keyword.Length || !IsAsciiIdentifierChar(rest[keyword.Length]));
    }

    private readonly struct PositionMetaInfo(CodeMetaInfo info) : ICodeMetaInfoProvider
    {
        public CodeMetaInfo? MetaInfo => info;
    }
}
