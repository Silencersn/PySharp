using PySharp.PyRuntime;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.RegularExpressions;

namespace PySharp.Tokenization;

public sealed partial class Lexer
{
    private enum LexerState
    {
        Unknown = 0,

        Default,
        TokenizingMultiLineSingleOrDoubleString,
        TokenizingTripleString,

        FStringMiddle,
        FStringDefault
    }

    public static IReadOnlyList<TokenInfo> Tokenize(string content)
    {
        var lexer = new Lexer();
        lexer.InternalStart();
        lexer.InternalTokenize(content);
        lexer.InternalEnd();
        return lexer._tokens;
    }

    private sealed class FStringInfo
    {
        public LexerState State;
        public string Prefix;
        public string Wrapper;
        public int ParenLevelWhenEntering;
        public bool IsTriple => Wrapper.Length is 3;
        public bool IsRaw { get; }
        public Regex WrapperRegex { get; }

        public FStringInfo(string prefix, string wrapper, int parenLevelWhenEntering)
        {
            State = LexerState.FStringMiddle;
            Prefix = prefix;
            Wrapper = wrapper;
            ParenLevelWhenEntering = parenLevelWhenEntering;
            IsRaw = prefix.ContainsAny('r', 'R');
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
    private string? _preString;
    private TokenPosition _stringStart;

    private int _parenLevel;
    private string? _currentLine;
    private string? _currentContent;

    internal IList<TokenInfo> Tokens => _tokens;

    internal Lexer()
    {
        _tokens = [];
        _lineno = 0;
        _offsetOfPreviousLine = 0;
        _offset = 0;
        _explicitLineJoining = false;
        _indentationLevels = [];
        _indentationLevels.Push(0);
        _parenLevel = 0;
        _currentLine = null;
        _currentContent = null;
        _fstringStack = [];
        CurrentState = LexerState.Default;
    }

    internal void InternalStart()
    {
        AppendToken(TokenType.Encoding, "utf-8", default, default, string.Empty);
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
                new TokenPosition(_lineno, 0),
                new TokenPosition(_lineno, 0),
                string.Empty
                );
            _tokens.Add(token);
        }
    }

    internal void InternalEnd()
    {
        Debug.Assert(_tokens.Count > 0);

        if (_tokens[^1].Type is not (TokenType.NewLine or TokenType.NL))
        {
            if (_tokens[^1].Type is TokenType.Encoding)
                _currentLine = string.Empty;
            AppendNewLineToken(string.Empty);
        }

        InternalClearIndentation();

        if (_offset != _offsetOfPreviousLine)
            _lineno++;
        var pos = new TokenPosition(_lineno, 0);
        AppendToken(TokenType.EndMarker, string.Empty, pos, pos, string.Empty);
    }

    internal void InternalTokenize(string content)
    {
        _currentContent = content;

        _offset = 0;
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
                        _currentLine ??= GetCurrentLine();
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
                            _currentLine = GetCurrentLine();
                        }

                        _offset = fStringOffset;
                        AppendToken(TokenType.FStringStart, prefix + wrapper);
                        _fstringStack.Push(new FStringInfo(prefix, wrapper, _parenLevel));
                        _offset += prefix.Length + wrapper.Length;
                        break;
                    }

                    var match = LexerRegexes.PseudoToken.Match(content, _offset);
                    if (match.Index != _offset)
                        throw new TokenizationException("internal error: no match");

                    TokenizePseudoToken(match);
                    _offset = match.Index + match.Length;
                    if (_needSetNewLine)
                    {
                        SetNewLine();
                        _needSetNewLine = false;
                    }

                    if (CurrentState is LexerState.FStringDefault && CurrentFStringInfo.ParenLevelWhenEntering == _parenLevel)
                    {
                        var lastToken = _tokens[^1];
                        if (lastToken.Type is TokenType.RightBrace)
                        {
                            CurrentState = LexerState.FStringMiddle;
                        }
                    }
                    break;

                case LexerState.FStringMiddle:
                    var info = CurrentFStringInfo;

                    var m = info.WrapperRegex.Match(content, _offset);
                    if (!m.Success)
                        throw new TokenizationException("internal error: f-string no end");
                    int indexOfWrapper = m.Index + m.Length - info.Wrapper.Length;
                    var indexOfLeftBrace = content.IndexOf('{', _offset);
                    var indexOfRightBrace = content.IndexOf('}', _offset);

                    if (IsFirstNotFoundOrBehindSecond(indexOfLeftBrace, indexOfWrapper) &&
                        IsFirstNotFoundOrBehindSecond(indexOfRightBrace, indexOfWrapper))
                    {
                        var value = content[_offset..indexOfWrapper];

                        var start = new TokenPosition(_lineno, _offset - _offsetOfPreviousLine);
                        var currentLine = _currentLine;
                        Debug.Assert(currentLine is not null);

                        // all the \r\n should be \n
                        value = value.Replace("\r", string.Empty);

                        if (!info.IsRaw)
                            // explicit line joining
                            value = value.Replace("\\\n", string.Empty);

                        var countOfNewLine = value.Count('\n');
                        if (countOfNewLine > 0)
                        {
                            if (!info.IsTriple && !info.IsRaw)
                                throw new TokenizationException("internal error: unexpected newline in single or double non-raw f-string");

                            _lineno += countOfNewLine;
                            _offset = content.LastIndexOf('\n', indexOfWrapper) + 1;
                            var lastLine = GetCurrentLine(out var offsetOfNextLine);
                            currentLine = content[_offsetOfPreviousLine..offsetOfNextLine];
                            _offsetOfPreviousLine = _offset;
                            _currentLine = lastLine;
                        }

                        _offset = indexOfWrapper;
                        var end = new TokenPosition(_lineno, _offset - _offsetOfPreviousLine);
                        AppendToken(TokenType.FStringMiddle, value, start, end, currentLine);
                        AppendToken(TokenType.FStringEnd, info.Wrapper);
                        _offset = indexOfWrapper + info.Wrapper.Length;
                        _fstringStack.Pop();
                    }
                    else if (indexOfLeftBrace is not -1 &&
                        IsFirstNotFoundOrBehindSecond(indexOfRightBrace, indexOfLeftBrace))
                    {
                        var value = content[_offset..indexOfLeftBrace];

                        var start = new TokenPosition(_lineno, _offset - _offsetOfPreviousLine);
                        var currentLine = _currentLine;
                        Debug.Assert(currentLine is not null);

                        // all the \r\n should be \n
                        value = value.Replace("\r", string.Empty);

                        if (!info.IsRaw)
                            // explicit line joining
                            value = value.Replace("\\\n", string.Empty);

                        var countOfNewLine = value.Count('\n');
                        if (countOfNewLine > 0)
                        {
                            if (!info.IsTriple && !info.IsRaw)
                                throw new TokenizationException("internal error: unexpected newline in single or double non-raw f-string");

                            _lineno += countOfNewLine;
                            _offset = content.LastIndexOf('\n', indexOfLeftBrace) + 1;
                            var lastLine = GetCurrentLine(out var offsetOfNextLine);
                            currentLine = content[_offsetOfPreviousLine..offsetOfNextLine];
                            _offsetOfPreviousLine = _offset;
                            _currentLine = lastLine;
                        }

                        _offset = indexOfLeftBrace;

                        bool isEscape = false;
                        if (_offset + 1 < content.Length && content[_offset + 1] is '{')
                        {
                            _offset += 2;
                            value += '{';
                            isEscape = true;
                        }

                        var end = new TokenPosition(_lineno, _offset - _offsetOfPreviousLine);
                        AppendToken(TokenType.FStringMiddle, value, start, end, currentLine);
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
                        if (indexOfRightBrace + 1 < content.Length && content[indexOfRightBrace + 1] is not '}')
                            throw new TokenizationException("f-string: single '}' is not allowed");

                        var value = content[_offset..indexOfRightBrace];

                        var start = new TokenPosition(_lineno, _offset - _offsetOfPreviousLine);
                        var currentLine = _currentLine;
                        Debug.Assert(currentLine is not null);

                        // all the \r\n should be \n
                        value = value.Replace("\r", string.Empty);

                        if (!info.IsRaw)
                            // explicit line joining
                            value = value.Replace("\\\n", string.Empty);

                        var countOfNewLine = value.Count('\n');
                        if (countOfNewLine > 0)
                        {
                            if (!info.IsTriple && !info.IsRaw)
                                throw new TokenizationException("internal error: unexpected newline in single or double non-raw f-string");

                            _lineno += countOfNewLine;
                            _offset = content.LastIndexOf('\n', indexOfRightBrace) + 1;
                            var lastLine = GetCurrentLine(out var offsetOfNextLine);
                            currentLine = content[_offsetOfPreviousLine..offsetOfNextLine];
                            _offsetOfPreviousLine = _offset;
                            _currentLine = lastLine;
                        }

                        _offset = indexOfRightBrace + 2;
                        value += '}';
                        var end = new TokenPosition(_lineno, _offset - _offsetOfPreviousLine);
                        AppendToken(TokenType.FStringMiddle, value, start, end, currentLine);
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

        // TODO: 'f' or 'F'
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

    private static bool IsStrictMatch(Regex regex, Group group)
    {
        var match = regex.Match(group.Value);
        return match.Success && match.Length == group.Length;
    }

    private static bool IsIgnored(string line)
    {
        var m = LexerRegexes.Ignore.Match(line);
        return m.Success && m.Index is 0 && m.Length == line.Length;
    }

    private static string GetStringPrefix(string str, out char firstWrapper)
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
        return str[..2];
    }

    private void AppendToken(TokenType type, string str, TokenPosition start, TokenPosition end, string line)
    {
        var token = new TokenInfo(
            type,
            str,
            start,
            end,
            line
            );
        _tokens.Add(token);
    }

    private void AppendToken(TokenType type, string str)
    {
        Debug.Assert(_currentLine is not null);
        var start = new TokenPosition(_lineno, _offset - _offsetOfPreviousLine);
        var end = new TokenPosition(_lineno, _offset - _offsetOfPreviousLine + str.Length);
        AppendToken(type, str, start, end, _currentLine);
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
        _currentLine = null;
        _needIndentation = true;
    }

    private string GetCurrentLine()
    {
        return GetCurrentLine(out _);
    }

    private string GetCurrentLine(out int offsetOfNextLine)
    {
        Debug.Assert(_currentContent is not null);
        var m = NewLineRegex.Match(_currentContent, _offset);
        string currentLine;
        if (m.Success)
            currentLine = _currentContent[_offset..(offsetOfNextLine = m.Index + m.Length)];
        else
            currentLine = _currentContent[_offset..(offsetOfNextLine = _currentContent.Length)];
        return currentLine;
    }

    private void TokenizeMultiLineString(Regex wrapper)
    {
        Debug.Assert(_currentContent is not null);

        var m = wrapper.Match(_currentContent, _offset);
        if (!m.Success)
            throw new TokenizationException("unterminated string literal");

        Debug.Assert(_preString is not null);
        var pastString = _currentContent[_offset..(m.Index + m.Length)];
        var fullString = _preString + pastString;

        // all the \r\n should be \n
        fullString = fullString.Replace("\r", string.Empty);

        if (!_isRawString)
            // explicit line joining
            fullString = fullString.Replace("\\\n", string.Empty);

        _lineno += pastString.AsSpan().Count('\n');

        var lastLine = GetCurrentLine(out var offsetOfNextLine);
        _currentLine = _currentContent[_offsetOfPreviousLine..offsetOfNextLine];
        _offsetOfPreviousLine = _currentContent.LastIndexOf('\n', m.Index + m.Length - 1, m.Index + m.Length - _offset + 1) + 1;
        var end = new TokenPosition(_lineno, (m.Index + m.Length) - _offsetOfPreviousLine);

        Debug.Assert(_currentLine is not null);
        AppendToken(TokenType.String, fullString, _stringStart, end, _currentLine);
        _currentLine = lastLine;
        _offset = m.Index + m.Length;
        CurrentState = LexerState.Default;
    }

    private void TokenizeMultiLineSingleOrDoubleString()
    {
        Debug.Assert(_currentContent is not null);
        Debug.Assert(CurrentState is LexerState.TokenizingMultiLineSingleOrDoubleString);
        Debug.Assert(_wrapper is '\'' or '"');

        _lineno++;
        TokenizeMultiLineString(_wrapper is '"' ? LexerRegexes.Double : LexerRegexes.Single);
    }

    private void TokenizeTripleString()
    {
        Debug.Assert(_currentContent is not null);
        Debug.Assert(CurrentState is LexerState.TokenizingTripleString);
        Debug.Assert(_wrapper is '\'' or '"');

        TokenizeMultiLineString(_wrapper is '"' ? LexerRegexes.Double3 : LexerRegexes.Single3);
    }

    private void TokenizeSingleLineSingleOrDoubleFString(string prefix, Group strGroup)
    {
        Debug.Assert(_wrapper is '\'' or '"');

        _offset = strGroup.Index;
        AppendToken(TokenType.FStringStart, prefix + _wrapper);

        var value = strGroup.Value[(prefix.Length + 1)..^1];

        var builder = new StringBuilder();

        for (int i = 0; i < value.Length; i++)
        {
            var c = value[i];

            if (c is '{')
            {
                i++;
                if (value[i] is '{')
                {
                    builder.Append('{');
                    continue;
                }
                else
                {
                    var middle = builder.ToString();
                    builder.Clear();
                    _offset = strGroup.Index + prefix.Length + i - middle.Length;
                    AppendToken(TokenType.FStringMiddle, middle);
                    _offset += middle.Length;
                    AppendToken(TokenType.Operator, "{");

                    while (true)
                    {
                        var match = LexerRegexes.PseudoToken.Match(value, i);
                        if (match.Index != i)
                            throw new TokenizationException("internal error: no match");

                        var group = match.Groups[1];
                        _offset = strGroup.Index + prefix.Length + 1 + group.Index;
                        if (IsStrictMatch(LexerRegexes.Number, group))
                        {
                            AppendToken(TokenType.Number, group.Value);
                        }
                        else if (IsStrictMatch(LexerRegexes.Special, group))
                        {
                            AppendToken(TokenType.Operator, group.Value);
                            if (group.Value is "}")
                            {
                                i = group.Index + group.Length - 1;
                                break;
                            }
                        }
                        else if (IsStrictMatch(LexerRegexes.String, group))
                        {
                            AppendToken(TokenType.String, group.Value);
                        }
                        else if (IsStrictMatch(LexerRegexes.Name, group))
                        {
                            AppendToken(TokenType.Name, group.Value);
                        }
                        else
                        {
                            throw new NotSupportedException();
                        }
                        i = group.Index + group.Length;
                    }
                }
            }
            else
            {
                builder.Append(c);
            }
        }

        if (builder.Length > 0)
        {
            var middle = builder.ToString();
            builder.Clear();
            _offset = strGroup.Index + strGroup.Length - 1 - middle.Length;
            AppendToken(TokenType.FStringMiddle, middle);
        }

        _offset = strGroup.Index + strGroup.Length - 1;
        AppendToken(TokenType.FStringEnd, _wrapper.ToString());
    }

    private void TokenizePseudoToken(Match match)
    {
        _currentLine ??= GetCurrentLine();

        var group = match.Groups[1];

        if (_needIndentation)
        {
            _needIndentation = false;

            if (_explicitLineJoining)
            {
                _explicitLineJoining = false;
            }
            else if (_parenLevel is 0 && !IsIgnored(_currentLine.TrimEnd('\r', '\n')))
            {
                var indentationLevel = group.Index - match.Index;

                if (indentationLevel > _indentationLevels.Peek())
                {
                    _indentationLevels.Push(indentationLevel);
                    Debug.Assert(_currentContent is not null);
                    AppendToken(TokenType.Indent, _currentContent[match.Index..group.Index]);
                }
                else
                {
                    while (_indentationLevels.Peek() > indentationLevel)
                    {
                        _ = _indentationLevels.Pop();
                        var pos = new TokenPosition(_lineno, 0);
                        AppendToken(TokenType.Dedent, string.Empty, pos, pos, string.Empty);
                    }

                    if (indentationLevel != _indentationLevels.Peek())
                    {
                        PyVirtualMachine.RaiseIndentationError("unindent does not match any outer indentation level");
                        throw new PyRuntimeException(PyVirtualMachine.CurrentException);
                    }
                }
            }
        }

        if (IsStrictMatch(LexerRegexes.PseudoExtras, group))
        {
            if (group.Value.StartsWith('\\'))
            {
                _explicitLineJoining = true;
                _needSetNewLine = true;
            }
            else if (IsStrictMatch(LexerRegexes.Comment, group))
            {
                _offset = group.Index;
                AppendToken(TokenType.Comment, group.Value);
            }
            else
            {
                _isRawString = group.Value.ContainsAny('r', 'R');
                _wrapper = group.Value[^1];
                _offset = group.Index;
                _stringStart = new TokenPosition(_lineno, _offset - _offsetOfPreviousLine);
                _preString = group.Value;
                CurrentState = LexerState.TokenizingTripleString;
            }
        }
        else if (IsStrictMatch(LexerRegexes.Number, group))
        {
            _offset = group.Index;
            AppendToken(TokenType.Number, group.Value);
        }
        else if (IsStrictMatch(LexerRegexes.Funny, group))
        {
            if (group.Value is "\r\n" or "\n")
            {
                AppendNewLineToken(group.Value);
                _needSetNewLine = true;
            }
            else
            {
                _offset = group.Index;
                AppendToken(TokenType.Operator, group.Value);
                if (group.Value is "(" or "[" or "{")
                    _parenLevel++;
                else if (group.Value is ")" or "]" or "}")
                    _parenLevel--;
            }
        }
        else if (IsStrictMatch(LexerRegexes.ContStr, group))
        {
            var prefix = GetStringPrefix(group.Value, out _wrapper);

            if (prefix.ContainsAny('f', 'F'))
            {
                TokenizeSingleLineSingleOrDoubleFString(prefix, group);
            }
            else
            {
                _isRawString = prefix.ContainsAny('r', 'R');
                _offset = group.Index;
                if (group.Value.EndsWith("\\\r\n") || group.Value.EndsWith("\\\n"))
                {
                    _stringStart = new TokenPosition(_lineno, _offset - _offsetOfPreviousLine);
                    _preString = group.Value;
                    CurrentState = LexerState.TokenizingMultiLineSingleOrDoubleString;
                }
                else
                {
                    AppendToken(TokenType.String, group.Value);
                }
            }

        }
        else if (IsStrictMatch(LexerRegexes.Name, group))
        {
            _offset = group.Index;
            AppendToken(TokenType.Name, group.Value);
        }
        else
        {
            Debug.Fail($"unknown: {group.Value}");
        }
    }

    [GeneratedRegex("\r?\n")]
    private static partial Regex NewLineRegex { get; }
}
