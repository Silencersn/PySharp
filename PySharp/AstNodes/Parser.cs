using PySharp.CodeAnalysis;
using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;
using PySharp.Resources;
using PySharp.Tokenization;
using System.Collections.Frozen;
namespace PySharp.AstNodes;

public sealed partial class Parser : ICodeMetaInfoProvider
{
    public static ModuleNode ParseModule(PyCallContext context, CodeSource codeSource, IEnumerable<TokenInfo> tokens)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(codeSource);
        ArgumentNullException.ThrowIfNull(tokens);

        return new Parser(context, codeSource, tokens).ParseFile();
    }
    public static ExpressionNode ParseExpression(PyCallContext context, CodeSource codeSource, IEnumerable<TokenInfo> tokens)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(codeSource);
        ArgumentNullException.ThrowIfNull(tokens);

        return new Parser(context, codeSource, tokens).ParseEval();
    }

    public static InteractiveNode ParseInteractive(PyCallContext context, CodeSource codeSource, IEnumerable<TokenInfo> tokens)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(codeSource);
        ArgumentNullException.ThrowIfNull(tokens);

        return new Parser(context, codeSource, tokens).ParseInteractive();
    }

    private static readonly FrozenSet<string> Keywords = [
        "False", "None", "True", "and", "as", "assert",
        "async", "await", "break", "class", "continue",
        "def", "del", "elif", "else", "except", "finally",
        "for", "from", "global", "if", "import", "in", "is",
        "lambda", "nonlocal", "not", "or", "pass", "raise",
        "return", "try", "while", "with", "yield"];

    private static readonly FrozenSet<TokenType> AugOperators = [
        TokenType.PlusEqual, TokenType.MinusEqual, TokenType.StarEqual, TokenType.AtEqual,
        TokenType.SlashEqual, TokenType.DoubleSlashEqual, TokenType.PercentEqual, TokenType.DoubleStarEqual,
        TokenType.RightShiftEqual, TokenType.LeftShiftEqual, TokenType.AmpersandEqual, TokenType.CaretEqual,
        TokenType.PipeEqual
        ];

    private static readonly FrozenSet<TokenType> BinaryOperators = [
        TokenType.Plus, TokenType.Minus, TokenType.Star, TokenType.Slash,
        TokenType.DoubleSlash, TokenType.Percent, TokenType.DoubleStar, TokenType.Pipe,
        TokenType.Ampersand, TokenType.Caret, TokenType.LeftShift, TokenType.RightShift,
        TokenType.Less, TokenType.Greater, TokenType.LessEqual, TokenType.GreaterEqual,
        TokenType.DoubleEqual, TokenType.NotEqual, TokenType.At,
    ];

    private static bool IsKeyword(string name)
    {
        return Keywords.Contains(name);
    }
    private static bool IsAugOperator(TokenType type)
    {
        return AugOperators.Contains(type);
    }

    private readonly PyCallContext _context;
    private readonly CodeSource _codeSource;
    private readonly OptimizationOptions _options;
    private readonly TokenStream _tokenStream;

    private int TokenStreamPosition
    {
        get { SkipUselessToken(); return _tokenStream.Position; }
        set => _tokenStream.Position = value;
    }

    internal TokenInfo CurrentToken
    {
        get
        {
            SkipUselessToken();
            return _tokenStream.CurrentToken;
        }
    }

    private TokenType CurrentTokenType => CurrentToken.Type;

    CodeMetaInfo? ICodeMetaInfoProvider.MetaInfo => CreateAstMetaInfo();

    internal Parser(PyCallContext context, CodeSource codeSource, TokenStream tokenStream)
    {
        _context = context;
        _options = _context.PyEnvironment.OptimizationOptions;
        _tokenStream = tokenStream;
        _codeSource = codeSource;
        _context.CurrentFrame.MetaInfoProvider = this;
    }
    internal Parser(PyCallContext context, CodeSource codeSource, IEnumerable<TokenInfo> tokens) : this(context, codeSource, new TokenArrayStream(tokens))
    {
    }

    private bool IsCurrentTypeTokenAnyOf(params ReadOnlySpan<TokenType> expectedTypes)
    {
        return expectedTypes.Contains(CurrentTokenType);
    }

    public PyRuntimeException SyntaxError(string message = PySR.InvalidSyntax, params ReadOnlySpan<object?> args)
    {
        if (args.Length > 0)
            message = PySR.Format(message, args);
        return _context.ThrowableSyntaxError(message);
    }

    private static bool IsUselessToken(TokenInfo tokenInfo)
    {
        return tokenInfo.Type is TokenType.NL or TokenType.Comment;
    }
    private void SkipUselessToken()
    {
        while (IsUselessToken(_tokenStream.CurrentToken))
            _tokenStream.MoveNextToken();
    }

    private void MoveNextToken()
    {
        if (_tokenStream.CurrentToken.Type is TokenType.EndMarker)
            return;

        _tokenStream.MoveNextToken();
        SkipUselessToken();
    }
    private void EnsureTokenType(TokenType type, string message = PySR.InvalidSyntax)
    {
        if (CurrentTokenType != type)
            throw SyntaxError(message);
    }
    private bool IsCurrentKeyword(string keyword)
    {
        if (CurrentTokenType is not TokenType.Name)
            return false;

        return CurrentToken.StringAsSpan.Equals(keyword, StringComparison.Ordinal);
    }
    private void EnsureKeywordThenMove(string keyword, string message = PySR.InvalidSyntax)
    {
        EnsureTokenType(TokenType.Name);
        if (!IsCurrentKeyword(keyword))
            throw SyntaxError(message);
        MoveNextToken();
    }
    private void EnsureTokenTypeThenMove(TokenType type, string message = PySR.InvalidSyntax)
    {
        EnsureTokenType(type, message);
        MoveNextToken();
    }
}
