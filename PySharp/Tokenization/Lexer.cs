using PySharp.CodeAnalysis;
using PySharp.PyRuntime.Calls;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.RegularExpressions;

namespace PySharp.Tokenization;

public sealed partial class Lexer : ICodeMetaInfoProvider
{
    private enum LexerState
    {
        Unknown = 0,

        Default,
        TokenizingMultiLineSingleOrDoubleString,
        TokenizingTripleString,

        FStringMiddle,
        FStringDefault,
    }

    public static List<TokenInfo> Tokenize(PyCallContext context, CodeSource codeSource)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(codeSource);

        var lexer = new Lexer(context, codeSource);
        lexer.InternalStart();
        lexer.InternalTokenize(codeSource.Code.Text);
        lexer.InternalEnd();
        return [.. lexer._tokens];
    }

    private sealed class FStringInfo
    {
        public LexerState State;
        public string Wrapper { get; }
        public int ParenLevelWhenEntering { get; }
        public bool IsTriple => Wrapper.Length is 3;
        public bool IsRaw { get; }
        public Regex WrapperRegex { get; }
        public Stack<int> FormatSpec { get; }

        public FStringInfo(bool isRaw, string wrapper, int parenLevelWhenEntering)
        {
            FormatSpec = [];
            State = LexerState.FStringMiddle;
            Wrapper = wrapper;
            ParenLevelWhenEntering = parenLevelWhenEntering;
            IsRaw = isRaw;
            WrapperRegex = wrapper switch
            {
                "\'" => LexerRegexes.Single,
                "\"" => LexerRegexes.Double,
                "\'\'\'" => LexerRegexes.Single3,
                "\"\"\"" => LexerRegexes.Double3,
                _ => throw new UnreachableException()
            };
        }
    }
    private LexerState _rootState;
    private readonly Stack<FStringInfo> _fstringStack;
    private FStringInfo CurrentFStringInfo => _fstringStack.Peek();
    private LexerState CurrentState
    {
        get
        {
            if (_fstringStack.Count is 0)
                return _rootState;
            return CurrentFStringInfo.State;
        }
        set
        {
            if (_fstringStack.Count is 0)
            {
                _rootState = value;
                return;
            }

            CurrentFStringInfo.State = value;
        }
    }

    private readonly PyCallContext _context;
    private readonly CodeSource _codeSource;

    private readonly List<TokenInfo> _tokens;
    private int _lineno;
    private int _offsetOfPreviousLine;
    private int _offset;
    private bool _explicitLineJoining;
    private readonly Stack<int> _indentationLevels;

    private bool _needSetNewLine;
    private bool _needIndentation;

    private char _wrapper;
    private bool _isRawString;
    private CodeTextSpan _preStringSpan;
    private CodeTextPosition _stringStart;

    private int _parenLevel;
    private string? _currentContent;
    private StringBuilder SharedBuilder => field ??= new();

    internal IList<TokenInfo> Tokens => _tokens;
    private ReadOnlySpan<char> CurrentLine => _codeSource.Code.GetLineOrDefault(_lineno, false);
    CodeMetaInfo? ICodeMetaInfoProvider.MetaInfo => new()
    {
        Source = _codeSource,
        Start = new(_lineno, 0)
    };

    internal Lexer(PyCallContext context, CodeSource codeSource)
    {
        _context = context;
        _context.CurrentFrame.MetaInfoProvider = this;
        _codeSource = codeSource;
        _tokens = [];
        _lineno = 0;
        _offsetOfPreviousLine = 0;
        _offset = 0;
        _explicitLineJoining = false;
        _indentationLevels = [];
        _indentationLevels.Push(0);
        _parenLevel = 0;
        _currentContent = null;
        _fstringStack = [];
        CurrentState = LexerState.Default;
    }

    internal void InternalStart()
    {
        AppendToken(TokenType.Encoding, "utf-8", default, default);
        SetNewLine();
    }

    internal void InternalClearIndentation()
    {
        while (_indentationLevels.Count > 1)
        {
            _ = _indentationLevels.Pop();
            var token = new TokenInfo(
                TokenType.Dedent,
                string.Empty,
                new CodeTextPosition(_lineno, 0),
                new CodeTextPosition(_lineno, 0),
                _codeSource
                );
            _tokens.Add(token);
        }
    }

    internal void InternalEnd()
    {
        if (CurrentState is LexerState.TokenizingMultiLineSingleOrDoubleString)
            throw _context.ThrowableSyntaxError($"unterminated string literal (detected at line {_lineno})");

        if (CurrentState is LexerState.TokenizingTripleString)
            throw _context.ThrowableSyntaxError($"unterminated triple-quoted string literal (detected at line {_lineno})");

        Debug.Assert(_tokens.Count > 0);

        if (_tokens[^1].Type is not (TokenType.NewLine or TokenType.NL))
        {
            AppendNewLineToken(string.Empty);
        }

        InternalClearIndentation();

        if (_offset != _offsetOfPreviousLine)
            _lineno++;
        var pos = new CodeTextPosition(_lineno, 0);
        AppendToken(TokenType.EndMarker, string.Empty, pos, pos);
    }

    internal void InternalTokenize(string content)
    {
        _currentContent = content;

        _offset = 0;
        _offsetOfPreviousLine = 0;
        while (_offset < content.Length)
        {
            switch (CurrentState)
            {
                case LexerState.TokenizingMultiLineSingleOrDoubleString:
                    TokenizeMultiLineSingleOrDoubleString();
                    break;

                case LexerState.TokenizingTripleString:
                    TokenizeTripleString();
                    break;

                case LexerState.Default or LexerState.FStringDefault:
                    if (IsFString(content, _offset, out var wrapper, out var whiteLength, out var prefix))
                    {
                        var fStringOffset = _offset + whiteLength;

                        if (whiteLength > 0 && content.AsSpan().Slice(_offset, whiteLength).Contains('\n'))
                        {
                            _offset = content.IndexOf('\n', _offset);
                            if (content[_offset - 1] is '\r')
                            {
                                _offset--;
                                AppendNewLineToken("\r\n");
                                _offset += 2;
                            }
                            else
                            {
                                AppendNewLineToken("\n");
                                _offset++;
                            }
                            SetNewLine();
                        }

                        _offset = fStringOffset;
                        AppendToken(TokenType.FStringStart, prefix + wrapper);
                        _fstringStack.Push(new FStringInfo(prefix.ContainsAny('r', 'R'), wrapper, _parenLevel));
                        _offset += prefix.Length + wrapper.Length;
                        break;
                    }

                    TokenizeToken(out var group);
                    Debug.Assert(group.Index == _offset);
                    _offset += group.Length;
                    if (_needSetNewLine)
                    {
                        SetNewLine();
                        _needSetNewLine = false;
                    }

                    if (CurrentState is LexerState.FStringDefault)
                    {
                        if (CurrentFStringInfo.ParenLevelWhenEntering == _parenLevel)
                        {
                            var lastToken = _tokens[^1];
                            if (lastToken.Type is TokenType.RightBrace)
                            {
                                CurrentState = LexerState.FStringMiddle;
                            }
                        }
                        else
                        {
                            var lastToken = _tokens[^1];
                            if (lastToken.Type is TokenType.Colon && _parenLevel == CurrentFStringInfo.ParenLevelWhenEntering + 1)
                            {
                                // TODO: too deep

                                CurrentState = LexerState.FStringMiddle;
                                CurrentFStringInfo.FormatSpec.Push(_parenLevel);
                            }
                            else if (lastToken.Type is TokenType.RightBrace)
                            {
                                if (CurrentFStringInfo.FormatSpec.Count > 0 && CurrentFStringInfo.FormatSpec.Peek() == _parenLevel)
                                {
                                    CurrentState = LexerState.FStringMiddle;
                                }
                            }
                        }
                    }
                    break;

                case LexerState.FStringMiddle:
                    var info = CurrentFStringInfo;

                    var m = info.WrapperRegex.Match(content, _offset);
                    if (!m.Success)
                        throw _context.ThrowableSyntaxError($"unterminated triple-quoted f-string literal (detected at line {_lineno})");

                    int indexOfWrapper = m.Index + m.Length - info.Wrapper.Length;
                    var indexOfLeftBrace = content.IndexOf('{', _offset);
                    var indexOfRightBrace = content.IndexOf('}', _offset);

                    if (IsFirstNotFoundOrBehindSecond(indexOfLeftBrace, indexOfWrapper) &&
                        IsFirstNotFoundOrBehindSecond(indexOfRightBrace, indexOfWrapper))
                    {
                        if (CurrentFStringInfo.FormatSpec.Count > 0)
                            throw _context.ThrowableSyntaxError("f-string: expecting '}', or format specs");
                        var start = new CodeTextPosition(_lineno, _offset - _offsetOfPreviousLine);

                        ExtractMultiLineTextInFString(indexOfWrapper, info, out var value);

                        var end = new CodeTextPosition(_lineno, _offset - _offsetOfPreviousLine);
                        AppendToken(TokenType.FStringMiddle, value, start, end);
                        AppendToken(TokenType.FStringEnd, info.Wrapper);
                        _offset = indexOfWrapper + info.Wrapper.Length;
                        _fstringStack.Pop();
                    }
                    else if (indexOfLeftBrace is not -1 &&
                        IsFirstNotFoundOrBehindSecond(indexOfRightBrace, indexOfLeftBrace))
                    {
                        var start = new CodeTextPosition(_lineno, _offset - _offsetOfPreviousLine);


                        bool isEscape = CurrentFStringInfo.FormatSpec.Count is 0 && indexOfLeftBrace + 1 < content.Length && content[indexOfLeftBrace + 1] is '{';

                        ExtractMultiLineTextInFString(indexOfLeftBrace + (isEscape ? 1 : 0), info, out var value);

                        if (isEscape)
                        {
                            // escape '{'
                            // one is consumed by ExtractMultiLineTextInFString
                            // the other one is here
                            _offset += 1;
                        }

                        var end = new CodeTextPosition(_lineno, _offset - _offsetOfPreviousLine);
                        AppendToken(TokenType.FStringMiddle, value, start, end);
                        if (!isEscape)
                        {
                            AppendToken(TokenType.Operator, "{");
                            _offset = indexOfLeftBrace + 1;
                            CurrentState = LexerState.FStringDefault;

                            // increment _parenLevel here
                            // while other operators including RightBrace
                            // should be processed by TokenizePseudoToken
                            _parenLevel++;
                        }
                    }
                    else if (indexOfRightBrace is not -1 &&
                        IsFirstNotFoundOrBehindSecond(indexOfLeftBrace, indexOfRightBrace))
                    {
                        if (CurrentFStringInfo.FormatSpec.Count > 0)
                        {
                            var start = new CodeTextPosition(_lineno, _offset - _offsetOfPreviousLine);

                            ExtractMultiLineTextInFString(indexOfRightBrace, info, out var value);

                            var end = new CodeTextPosition(_lineno, _offset - _offsetOfPreviousLine);
                            AppendToken(TokenType.FStringMiddle, value, start, end);
                            AppendToken(TokenType.Operator, "}");
                            _offset = indexOfRightBrace + 1;
                            CurrentFStringInfo.FormatSpec.Pop();
                            _parenLevel--;
                            break;
                        }
                        else
                        {
                            if (indexOfRightBrace + 1 < content.Length && content[indexOfRightBrace + 1] is not '}')
                                throw _context.ThrowableSyntaxError("f-string: single '}' is not allowed");

                            var start = new CodeTextPosition(_lineno, _offset - _offsetOfPreviousLine);

                            ExtractMultiLineTextInFString(indexOfRightBrace + 1 /* included escape '}' */, info, out var value);

                            // escape '}'
                            // one is consumed by ExtractMultiLineTextInFString
                            // the other one is here
                            _offset += 1;
                            var end = new CodeTextPosition(_lineno, _offset - _offsetOfPreviousLine);
                            AppendToken(TokenType.FStringMiddle, value, start, end);
                        }
                    }
                    else
                    {
                        throw new UnreachableException();
                    }

                    static bool IsFirstNotFoundOrBehindSecond(int indexOfFirst, int indexOfSecond)
                    {
                        Debug.Assert(indexOfSecond is not -1);
                        return indexOfFirst is -1 || indexOfFirst > indexOfSecond;
                    }

                    break;

                default:
                    throw new UnreachableException();
            }
        }
    }

    private static readonly string[] FStringPrefixes = ["fr", "Fr", "fR", "FR", "rf", "rF", "Rf", "RF", "f", "F"];

    private void ExtractMultiLineTextInFString(int untilIndex, FStringInfo info, out string value)
    {
        Debug.Assert(_currentContent is not null);

        var builder = SharedBuilder.Clear();
        builder.Append(_currentContent.AsSpan()[_offset..untilIndex]);

        // all the \r\n or \r should be \n
        builder.Replace("\r\n", "\n");
        builder.Replace('\r', '\n');

        if (!info.IsRaw)
            // explicit line joining
            builder.Replace("\\\n", string.Empty);

        value = builder.ToString();

        var countOfNewLine = value.Count('\n');
        if (countOfNewLine > 0)
        {
            if (!info.IsTriple && !info.IsRaw)
                throw _context.ThrowableSyntaxError($"unterminated string literal (detected at line {_lineno})");

            _lineno += countOfNewLine;
            _offset = _currentContent.LastIndexOf('\n', untilIndex) + 1;
            _offsetOfPreviousLine = _offset;
        }

        _offset = untilIndex;
    }

    private static bool IsFString(string content, int offset, [NotNullWhen(true)] out string? wrapper, out int whiteLength, [NotNullWhen(true)] out string? prefix)
    {
        var current = content.AsSpan()[offset..];
        var length = current.Length;
        current = current.TrimStart();
        whiteLength = length - current.Length;

        prefix = null;
        foreach (var fStringPrefix in FStringPrefixes)
        {
            if (current.StartsWith(fStringPrefix))
            {
                prefix = fStringPrefix;
                break;
            }
        }
        if (prefix is null)
        {
            wrapper = null;
            return false;
        }
        current = current[prefix.Length..];

        if (current.StartsWith("\"\"\"") || current.StartsWith("\'\'\'"))
        {
            wrapper = current[..3].ToString();
            return true;
        }
        else if (current.StartsWith("\"") || current.StartsWith("\'"))
        {
            wrapper = current[..1].ToString();
            return true;
        }

        wrapper = null;
        return false;
    }

    private bool IsStrictMatchFromCurrent(Regex regex, out ValueGroup group)
    {
        Debug.Assert(_currentContent is not null);

        group = default;

        var enumerator = regex.EnumerateMatches(_currentContent.AsSpan()[_offset..]);
        if (!enumerator.MoveNext())
            return false;

        var match = enumerator.Current;
        if (match.Index is not 0)
            return false;

        group.TotalContent = _currentContent;
        group.Index = match.Index + _offset;
        group.Length = match.Length;
        return true;
    }

    private static bool IsIgnored(ReadOnlySpan<char> line)
    {
        Debug.Assert(!line.ContainsAny(['\n', '\r']));
        line = line.TrimStart();
        return line.Length is 0 || line[0] is '#';
    }

    private static string GetStringPrefix(ReadOnlySpan<char> str, out char firstWrapper)
    {
        Debug.Assert(str.Length >= 2);

        if (str[0] is '\'' or '"')
        {
            firstWrapper = str[0];
            return string.Empty;
        }

        if (str[1] is '\'' or '"')
        {
            firstWrapper = str[1];
            return str[0].ToString();
        }

        Debug.Assert(str.Length >= 3);
        firstWrapper = str[2];
        return str[..2].ToString();
    }

    private void AppendToken(TokenType type, CodeTextSpan span, CodeTextPosition start, CodeTextPosition end)
    {
        var token = new TokenInfo(
            type,
            span,
            start,
            end,
            _codeSource);
        _tokens.Add(token);
    }
    private void AppendToken(TokenType type, string str, CodeTextPosition start, CodeTextPosition end)
    {
        var token = new TokenInfo(
            type,
            str,
            start,
            end,
            _codeSource);
        _tokens.Add(token);
    }
    private void AppendToken(TokenType type, CodeTextSpan span)
    {
        var start = new CodeTextPosition(_lineno, _offset - _offsetOfPreviousLine);
        var end = new CodeTextPosition(_lineno, _offset - _offsetOfPreviousLine + span.Length);
        AppendToken(type, span, start, end);
    }
    private void AppendToken(TokenType type, string str)
    {
        var start = new CodeTextPosition(_lineno, _offset - _offsetOfPreviousLine);
        var end = new CodeTextPosition(_lineno, _offset - _offsetOfPreviousLine + str.Length);
        AppendToken(type, str, start, end);
    }

    private void AppendNewLineToken(string str)
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

        AppendToken(isNewLine ? TokenType.NewLine : TokenType.NL, str);
    }

    private void SetNewLine()
    {
        _lineno++;
        _offsetOfPreviousLine = _offset;
        _needIndentation = true;
    }

    private void TokenizeMultiLineString(Regex wrapper, bool isTriple)
    {
        Debug.Assert(_currentContent is not null);

        var m = wrapper.Match(_currentContent, _offset);
        if (!m.Success)
        {
            if (isTriple)
                throw _context.ThrowableSyntaxError($"unterminated triple-quoted string literal (detected at line {_lineno})");

            throw _context.ThrowableSyntaxError($"unterminated string literal (detected at line {_lineno})");
        }

        Debug.Assert(!_preStringSpan.IsEmpty);
        Debug.Assert(_offset == _preStringSpan.End);
        var pastString = _currentContent.AsSpan()[_offset..(m.Index + m.Length)];

        var builder = SharedBuilder.Clear();
        builder.Append(_currentContent.AsSpan()[_preStringSpan.Start..(m.Index + m.Length)]);

        // all the \r\n or \r should be \n
        builder.Replace("\r\n", "\n");
        builder.Replace('\r', '\n');

        if (!_isRawString)
            // explicit line joining
            builder.Replace("\\\n", string.Empty);

        var fullString = builder.ToString();

        _lineno += pastString.Count('\n');

        _offsetOfPreviousLine = _currentContent.LastIndexOf('\n', m.Index + m.Length - 1, m.Index + m.Length - _offset + 1) + 1;
        var end = new CodeTextPosition(_lineno, (m.Index + m.Length) - _offsetOfPreviousLine);

        AppendToken(TokenType.String, fullString, _stringStart, end);
        _offset = m.Index + m.Length;
        CurrentState = LexerState.Default;
    }

    private void TokenizeMultiLineSingleOrDoubleString()
    {
        Debug.Assert(_currentContent is not null);
        Debug.Assert(CurrentState is LexerState.TokenizingMultiLineSingleOrDoubleString);
        Debug.Assert(_wrapper is '\'' or '"');

        _lineno++;
        TokenizeMultiLineString(_wrapper is '"' ? LexerRegexes.Double : LexerRegexes.Single, false);
    }

    private void TokenizeTripleString()
    {
        Debug.Assert(_currentContent is not null);
        Debug.Assert(CurrentState is LexerState.TokenizingTripleString);
        Debug.Assert(_wrapper is '\'' or '"');

        TokenizeMultiLineString(_wrapper is '"' ? LexerRegexes.Double3 : LexerRegexes.Single3, true);
    }

    private void EnsureIndentation(int indentationLevel)
    {
        if (!_needIndentation)
            return;

        _needIndentation = false;

        if (_explicitLineJoining)
        {
            _explicitLineJoining = false;
            return;
        }

        if (_parenLevel is not 0 || IsIgnored(CurrentLine))
            return;

        if (indentationLevel > _indentationLevels.Peek())
        {
            _indentationLevels.Push(indentationLevel);
            Debug.Assert(_currentContent is not null);
            AppendToken(TokenType.Indent, _currentContent.Substring(_offset, indentationLevel));
            return;
        }

        while (_indentationLevels.Peek() > indentationLevel)
        {
            _ = _indentationLevels.Pop();
            var pos = new CodeTextPosition(_lineno, 0);
            AppendToken(TokenType.Dedent, string.Empty, pos, pos);
        }

        if (indentationLevel != _indentationLevels.Peek())
            throw _context.ThrowableIndentationError("unindent does not match any outer indentation level");
    }

    private struct ValueGroup
    {
        public string TotalContent;
        public int Index;
        public int Length;
        public readonly ReadOnlySpan<char> Value => TotalContent.AsSpan(Index, Length);
        public readonly CodeTextSpan Span => new(Index, Length);
    }

    private void TokenizeToken(out ValueGroup group)
    {
        var success = IsStrictMatchFromCurrent(LexerRegexes.Whitespace, out group);
        Debug.Assert(success);

        EnsureIndentation(group.Length);
        _offset += group.Length;

        if (IsStrictMatchFromCurrent(LexerRegexes.StartsWithPseudoExtras, out group))
        {
            if (group.Length is 0)
            {
            }
            else if (group.Value.StartsWith('\\'))
            {
                _explicitLineJoining = true;
                _needSetNewLine = true;
            }
            else if (group.Value[0] is '#')
            {
                AppendToken(TokenType.Comment, group.Span);
            }
            else
            {
                _isRawString = group.Value.ContainsAny('r', 'R');
                _wrapper = group.Value[^1];
                _stringStart = new CodeTextPosition(_lineno, _offset - _offsetOfPreviousLine);
                _preStringSpan = group.Span;
                CurrentState = LexerState.TokenizingTripleString;
            }
        }
        else if (IsStrictMatchFromCurrent(LexerRegexes.StartsWithNumber, out group))
        {
            AppendToken(TokenType.Number, group.Span);
        }
        else if (IsStrictMatchFromCurrent(LexerRegexes.StartsWithFunny, out group))
        {
            if (group.Value is "\r\n" or "\n")
            {
                AppendNewLineToken(group.Value.ToString());
                _needSetNewLine = true;
            }
            else
            {
                AppendToken(TokenType.Operator, group.Span);
                if (group.Value is "(" or "[" or "{")
                    _parenLevel++;
                else if (group.Value is ")" or "]" or "}")
                    _parenLevel--;
            }
        }
        else if (IsStrictMatchFromCurrent(LexerRegexes.StartsWithContStr, out group))
        {
            var prefix = GetStringPrefix(group.Value, out _wrapper);

            _isRawString = prefix.ContainsAny('r', 'R');
            if (group.Value.EndsWith("\\\r\n") || group.Value.EndsWith("\\\n"))
            {
                _stringStart = new CodeTextPosition(_lineno, _offset - _offsetOfPreviousLine);
                _preStringSpan = group.Span;
                CurrentState = LexerState.TokenizingMultiLineSingleOrDoubleString;
            }
            else
            {
                AppendToken(TokenType.String, group.Span);
            }
        }
        else if (IsStrictMatchFromCurrent(LexerRegexes.StartsWithName, out group))
        {
            AppendToken(TokenType.Name, group.Span);
        }
        else
        {
            Debug.Assert(_currentContent is not null);

            if (_currentContent[_offset] is '\r')
            {
                AppendNewLineToken("\r");
                _needSetNewLine = true;
                group.TotalContent = _currentContent;
                group.Index = _offset;
                group.Length = 1;
                return;
            }

            throw _context.ThrowableSyntaxError("invalid syntax");
        }
    }
}
