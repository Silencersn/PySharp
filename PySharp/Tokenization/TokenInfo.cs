using PySharp.CodeAnalysis;
using System.Collections.Frozen;
using System.Diagnostics;
using System.Text;

namespace PySharp.Tokenization;

public sealed record class TokenInfo
{
    public static readonly FrozenDictionary<string, TokenType> ExactTokenTypes = new Dictionary<string, TokenType>
    {
        ["!"] = TokenType.Exclamation,
        ["!="] = TokenType.NotEqual,
        ["%"] = TokenType.Percent,
        ["%="] = TokenType.PercentEqual,
        ["&"] = TokenType.Ampersand,
        ["&="] = TokenType.AmpersandEqual,
        ["("] = TokenType.LeftParen,
        [")"] = TokenType.RightParen,
        ["*"] = TokenType.Star,
        ["**"] = TokenType.DoubleStar,
        ["**="] = TokenType.DoubleStarEqual,
        ["*="] = TokenType.StarEqual,
        ["+"] = TokenType.Plus,
        ["+="] = TokenType.PlusEqual,
        [","] = TokenType.Comma,
        ["-"] = TokenType.Minus,
        ["-="] = TokenType.MinusEqual,
        ["->"] = TokenType.RightArrow,
        ["."] = TokenType.Dot,
        ["..."] = TokenType.Ellipsis,
        ["/"] = TokenType.Slash,
        ["//"] = TokenType.DoubleSlash,
        ["//="] = TokenType.DoubleSlashEqual,
        ["/="] = TokenType.SlashEqual,
        [":"] = TokenType.Colon,
        [":="] = TokenType.ColonEqual,
        [";"] = TokenType.Semicolon,
        ["<"] = TokenType.Less,
        ["<<"] = TokenType.LeftShift,
        ["<<="] = TokenType.LeftShiftEqual,
        ["<="] = TokenType.LessEqual,
        ["="] = TokenType.Equal,
        ["=="] = TokenType.DoubleEqual,
        [">"] = TokenType.Greater,
        [">="] = TokenType.GreaterEqual,
        [">>"] = TokenType.RightShift,
        [">>="] = TokenType.RightShiftEqual,
        ["@"] = TokenType.At,
        ["@="] = TokenType.AtEqual,
        ["["] = TokenType.LeftSquareBracket,
        ["]"] = TokenType.RightSquareBracket,
        ["^"] = TokenType.Caret,
        ["^="] = TokenType.CaretEqual,
        ["{"] = TokenType.LeftBrace,
        ["|"] = TokenType.Pipe,
        ["|="] = TokenType.PipeEqual,
        ["}"] = TokenType.RightBrace,
        ["~"] = TokenType.Tilde
    }.ToFrozenDictionary();

    private CodeSource Source { get; }
    public TokenType Type { get; }
    public string String { get; }
    public CodeTextPosition Start { get; }
    public CodeTextPosition End { get; }
    public string Line
    {
        get
        {
            var builder = new StringBuilder();
            for (int i = Start.Line; i <= End.Line; i++)
            {
                if (Source.Code.TryGetLine(i, true, out var line))
                    builder.Append(line);
            }
            return builder.ToString();
        }
    }

    internal TokenInfo(TokenType type, string str, CodeTextPosition start, CodeTextPosition end, CodeSource source)
    {
        Debug.Assert((uint)type < (uint)TokenType.Count);
        Debug.Assert(str is not null);
        Debug.Assert(source is not null);

        Source = source;
        if (type is TokenType.Operator)
            type = ExactTokenTypes[str];

        Type = type;
        String = str;
        Start = start;
        End = end;
    }

    public override string ToString()
    {
        var builder = new StringBuilder()
            .Append(nameof(TokenInfo))
            .Append('(')

            .Append(nameof(Type))
            .Append('=')
            .Append(Type)

            .Append(", ")

            .Append(nameof(String))
            .Append('=')
            .Append(ToLiteral(String))

            .Append(", ")

            .Append(nameof(Start))
            .Append('=')
            .AppendFormat("({0}, {1})", Start.Line, Start.Offset)

            .Append(", ")

            .Append(nameof(End))
            .Append('=')
            .AppendFormat("({0}, {1})", End.Line, End.Offset)

            .Append(", ")
            .Append(nameof(Line))
            .Append('=')
            .Append(ToLiteral(Line))

            .Append(')');

        return builder.ToString();
    }

    private static string ToLiteral(string input)
    {
        var builder = new StringBuilder();
        builder.Append('"');
        foreach (char c in input)
        {
            switch (c)
            {
                case '\\': builder.Append("\\\\"); break;
                case '\"': builder.Append("\\\""); break;
                case '\n': builder.Append("\\n"); break;
                case '\r': builder.Append("\\r"); break;
                case '\t': builder.Append("\\t"); break;
                case '\0': builder.Append("\\0"); break;
                default: builder.Append(c); break;
            }
        }
        builder.Append('"');
        return builder.ToString();
    }
}