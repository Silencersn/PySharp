namespace PySharp.Compilation.Tokenization;

public enum TokenType : byte
{
    None = 0,

    EndMarker = None,
    Name,
    Number,
    String,
    NewLine,
    Indent,
    Dedent,
    /// <summary>
    /// (
    /// </summary>
    LeftParen,
    /// <summary>
    /// )
    /// </summary>
    RightParen,
    /// <summary>
    /// [
    /// </summary>
    LeftSquareBracket,
    /// <summary>
    /// ]
    /// </summary>
    RightSquareBracket,
    /// <summary>
    /// :
    /// </summary>
    Colon,
    /// <summary>
    /// ,
    /// </summary>
    Comma,
    /// <summary>
    /// ;
    /// </summary>
    Semicolon,
    /// <summary>
    /// +
    /// </summary>
    Plus,
    /// <summary>
    /// -
    /// </summary>
    Minus,
    /// <summary>
    /// *
    /// </summary>
    Star,
    /// <summary>
    /// /
    /// </summary>
    Slash,
    /// <summary>
    /// |
    /// </summary>
    Pipe,
    /// <summary>
    /// &
    /// </summary>
    Ampersand,
    /// <summary>
    /// <
    /// </summary>
    Less,
    /// <summary>
    /// >
    /// </summary>
    Greater,
    /// <summary>
    /// =
    /// </summary>
    Equal,
    /// <summary>
    /// .
    /// </summary>
    Dot,
    /// <summary>
    /// %
    /// </summary>
    Percent,
    /// <summary>
    /// {
    /// </summary>
    LeftBrace,
    /// <summary>
    /// }
    /// </summary>
    RightBrace,
    /// <summary>
    /// ==
    /// </summary>
    DoubleEqual,
    /// <summary>
    /// !=
    /// </summary>
    NotEqual,
    /// <summary>
    /// <=
    /// </summary>
    LessEqual,
    /// <summary>
    /// >=
    /// </summary>
    GreaterEqual,
    /// <summary>
    /// ~
    /// </summary>
    Tilde,
    /// <summary>
    /// ^
    /// </summary>
    Caret,
    /// <summary>
    /// <<
    /// </summary>
    LeftShift,
    /// <summary>
    /// >>
    /// </summary>
    RightShift,
    /// <summary>
    /// **
    /// </summary>
    DoubleStar,
    /// <summary>
    /// +=
    /// </summary>
    PlusEqual,
    /// <summary>
    /// -=
    /// </summary>
    MinusEqual,
    /// <summary>
    /// *=
    /// </summary>
    StarEqual,
    /// <summary>
    /// /=
    /// </summary>
    SlashEqual,
    /// <summary>
    /// %=
    /// </summary>
    PercentEqual,
    /// <summary>
    /// &=
    /// </summary>
    AmpersandEqual,
    /// <summary>
    /// |=
    /// </summary>
    PipeEqual,
    /// <summary>
    /// ^=
    /// </summary>
    CaretEqual,
    /// <summary>
    /// <<=
    /// </summary>
    LeftShiftEqual,
    /// <summary>
    /// >>= 
    /// </summary>
    RightShiftEqual,
    /// <summary>
    /// **=
    /// </summary>
    DoubleStarEqual,
    /// <summary>
    /// //
    /// </summary>
    DoubleSlash,
    /// <summary>
    /// //=
    /// </summary>
    DoubleSlashEqual,
    /// <summary>
    /// @
    /// </summary>
    At,
    /// <summary>
    /// @=
    /// </summary>
    AtEqual,
    /// <summary>
    /// ->
    /// </summary>
    RightArrow,
    /// <summary>
    /// ...
    /// </summary>
    Ellipsis,
    /// <summary>
    /// :=
    /// </summary>
    ColonEqual,
    /// <summary>
    /// !
    /// </summary>
    Exclamation,
    Operator,
    TypeIgnore,
    TypeComment,
    SoftKeyword,
    FStringStart,
    FStringMiddle,
    FStringEnd,
    TStringStart,
    TStringMiddle,
    TStringEnd,
    Comment,
    NL,
    ErrorToken,
    Encoding,

    Count,
}