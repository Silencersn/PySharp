using PySharp.Compilation.CodeAnalysis;
using PySharp.Compilation.Tokenization;
using PySharp.Runtime;
using PySharp.Runtime.Calls;
using System.Collections.Frozen;
using System.Text;
namespace PySharp.Compilation.AstNodes;

public sealed partial class Parser : ICodeMetaInfoProvider
{
    public static ModuleNode ParseModule(PyCallContext context, CodeSource codeSource, TokenStream tokens)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(codeSource);
        ArgumentNullException.ThrowIfNull(tokens);

        return new Parser(context, codeSource, tokens).ParseFile();
    }
    public static ExpressionNode ParseExpression(PyCallContext context, CodeSource codeSource, TokenStream tokens)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(codeSource);
        ArgumentNullException.ThrowIfNull(tokens);

        return new Parser(context, codeSource, tokens).ParseEval();
    }

    public static InteractiveNode ParseInteractive(PyCallContext context, CodeSource codeSource, TokenStream tokens)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(codeSource);
        ArgumentNullException.ThrowIfNull(tokens);

        return new Parser(context, codeSource, tokens).ParseInteractive();
    }

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

    private static bool IsKeyword(ReadOnlySpan<char> name)
    {
        return name is "False" or "None" or "True" or "and" or "as" or "assert"
            or "async" or "await" or "break" or "class" or "continue" or "def"
            or "del" or "elif" or "else" or "except" or "finally" or "for"
            or "from" or "global" or "if" or "import" or "in" or "is" or "lambda"
            or "nonlocal" or "not" or "or" or "pass" or "raise" or "return"
            or "try" or "while" or "with" or "yield";
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

    internal Token CurrentToken
    {
        get
        {
            SkipUselessToken();
            return _tokenStream.CurrentToken;
        }
    }

    internal TokenType CurrentTokenType => CurrentToken.Type;
    private ReadOnlySpan<char> CurrentTokenStringAsSpan => _codeSource.Code.GetString(CurrentToken.StringSpan);
    private string CurrentTokenString => CurrentTokenStringAsSpan.ToString();

    private bool IsCurrentIdentifier => CurrentTokenType is TokenType.Name && !IsKeyword(CurrentTokenStringAsSpan);
    private StringBuilder SharedBuilder => field ??= new StringBuilder();

    CodeMetaInfo? ICodeMetaInfoProvider.MetaInfo => CreateAstMetaInfo();

    internal Parser(PyCallContext context, CodeSource codeSource, TokenStream tokenStream)
    {
        _context = context;
        _options = _context.PyEnvironment.OptimizationOptions;
        _tokenStream = tokenStream;
        _codeSource = codeSource;
        _context.CurrentFrame.MetaInfoProvider = this;
    }

    private bool IsCurrentTypeTokenAnyOf(params ReadOnlySpan<TokenType> expectedTypes)
    {
        return expectedTypes.Contains(CurrentTokenType);
    }

    public PyRuntimeException SyntaxError(string message = PySR.InvalidSyntax, params ReadOnlySpan<object?> args)
    {
        return _context.SyntaxError(message, args);
    }

    private static bool IsUselessToken(Token tokenInfo)
    {
        return tokenInfo.Type is TokenType.NL or TokenType.Comment;
    }
    private void SkipUselessToken()
    {
        while (IsUselessToken(_tokenStream.CurrentToken))
            _tokenStream.MoveNextToken();
    }

    internal void MoveNextToken()
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

        return CurrentTokenStringAsSpan.Equals(keyword, StringComparison.Ordinal);
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
