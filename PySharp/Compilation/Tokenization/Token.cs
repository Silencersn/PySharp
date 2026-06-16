using PySharp.Compilation.CodeAnalysis;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PySharp.Compilation.Tokenization;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly record struct Token
{
    internal static TokenType GetExactTokenType(ReadOnlySpan<char> str)
    {
        return str switch
        {
            "!" => TokenType.Exclamation,
            "!=" => TokenType.NotEqual,
            "%" => TokenType.Percent,
            "%=" => TokenType.PercentEqual,
            "&" => TokenType.Ampersand,
            "&=" => TokenType.AmpersandEqual,
            "(" => TokenType.LeftParen,
            ")" => TokenType.RightParen,
            "*" => TokenType.Star,
            "**" => TokenType.DoubleStar,
            "**=" => TokenType.DoubleStarEqual,
            "*=" => TokenType.StarEqual,
            "+" => TokenType.Plus,
            "+=" => TokenType.PlusEqual,
            "," => TokenType.Comma,
            "-" => TokenType.Minus,
            "-=" => TokenType.MinusEqual,
            "->" => TokenType.RightArrow,
            "." => TokenType.Dot,
            "..." => TokenType.Ellipsis,
            "/" => TokenType.Slash,
            "//" => TokenType.DoubleSlash,
            "//=" => TokenType.DoubleSlashEqual,
            "/=" => TokenType.SlashEqual,
            ":" => TokenType.Colon,
            ":=" => TokenType.ColonEqual,
            ";" => TokenType.Semicolon,
            "<" => TokenType.Less,
            "<<" => TokenType.LeftShift,
            "<<=" => TokenType.LeftShiftEqual,
            "<=" => TokenType.LessEqual,
            "=" => TokenType.Equal,
            "==" => TokenType.DoubleEqual,
            ">" => TokenType.Greater,
            ">=" => TokenType.GreaterEqual,
            ">>" => TokenType.RightShift,
            ">>=" => TokenType.RightShiftEqual,
            "@" => TokenType.At,
            "@=" => TokenType.AtEqual,
            "[" => TokenType.LeftSquareBracket,
            "]" => TokenType.RightSquareBracket,
            "^" => TokenType.Caret,
            "^=" => TokenType.CaretEqual,
            "{" => TokenType.LeftBrace,
            "|" => TokenType.Pipe,
            "|=" => TokenType.PipeEqual,
            "}" => TokenType.RightBrace,
            "~" => TokenType.Tilde,

            _ => throw new UnreachableException()
        };
    }

    public CodeTextSpan StringSpan { get; }
    public TokenType Type { get; }

    internal Token(TokenType type, CodeTextSpan span, CodeSource source)
    {
        Debug.Assert((uint)type < (uint)TokenType.Count);
        Debug.Assert(source is not null);

        StringSpan = span;

        if (type is TokenType.Operator)
            type = GetExactTokenType(source.Code.GetString(StringSpan));

        Type = type;
    }

    public CodeTextPosition GetStart(CodeSource source)
    {
        return source.Code.OffsetToPosition(StringSpan.Start);
    }
    public CodeTextPosition GetEnd(CodeSource source)
    {
        return source.Code.OffsetToPosition(StringSpan.End);
    }
    public ReadOnlySpan<char> GetLine(CodeSource source)
    {
        var startLine = GetStart(source).Line;
        var endLine = GetStart(source).Line;

        if (startLine < 1 || startLine > source.Code.LineCount)
            return [];
        if (endLine < 1 || endLine > source.Code.LineCount)
            return [];

        Debug.Assert(startLine <= endLine);
        return source.Code.GetMultiLines(startLine, endLine);
    }
}