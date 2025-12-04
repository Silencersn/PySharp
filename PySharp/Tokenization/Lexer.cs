using PySharp.PyRuntime;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace PySharp.Tokenization;

public sealed partial class Lexer
{
    private enum LexerState
    {
        None = 0,
        TokenizingMultiLineSingleOrDoubleString,
        TokenizingTripleString
    }

    public static IReadOnlyList<TokenInfo> Tokenize(string content)
    {
        var lexer = new Lexer();
        lexer.InternalStart();
        lexer.InternalTokenize(content);
        lexer.InternalEnd();
        return lexer._tokens;
    }

    private LexerState _lexerState;

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
        _lexerState = LexerState.None;
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
            switch (_lexerState)
            {
                case LexerState.TokenizingMultiLineSingleOrDoubleString:
                    TokenizeMultiLineSingleOrDoubleString();
                    break;

                case LexerState.TokenizingTripleString:
                    TokenizeTripleString();
                    break;

                default:
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
                    break;
            }
        }
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
        _lexerState = LexerState.None;
    }

    private void TokenizeMultiLineSingleOrDoubleString()
    {
        Debug.Assert(_currentContent is not null);
        Debug.Assert(_lexerState is LexerState.TokenizingMultiLineSingleOrDoubleString);
        Debug.Assert(_wrapper is '\'' or '"');

        _lineno++;
        TokenizeMultiLineString(_wrapper is '"' ? LexerRegexes.Double : LexerRegexes.Single);
    }

    private void TokenizeTripleString()
    {
        Debug.Assert(_currentContent is not null);
        Debug.Assert(_lexerState is LexerState.TokenizingTripleString);
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
                _lexerState = LexerState.TokenizingTripleString;
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
                    _lexerState = LexerState.TokenizingMultiLineSingleOrDoubleString;
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
