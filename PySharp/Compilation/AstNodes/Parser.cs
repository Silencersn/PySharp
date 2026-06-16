using PySharp.Compilation.CodeAnalysis;
using PySharp.Compilation.Tokenization;
using PySharp.Modules.Builtins;
using PySharp.Runtime;
using PySharp.Runtime.Calls;
using System.Collections.Frozen;
using System.Diagnostics;
using System.Text;
namespace PySharp.Compilation.AstNodes;

public sealed partial class Parser : ICodeMetaInfoProvider
{
    public static ModuleNode ParseModule(PyCallContext context, CodeSource codeSource, TokenSequence tokens, bool enableNameMangling = true)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(codeSource);
        ArgumentNullException.ThrowIfNull(tokens);

        return new Parser(context, codeSource, tokens, enableNameMangling).ParseFile();
    }

    public static ExpressionNode ParseExpression(PyCallContext context, CodeSource codeSource, TokenSequence tokens, bool enableNameMangling = true)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(codeSource);
        ArgumentNullException.ThrowIfNull(tokens);

        return new Parser(context, codeSource, tokens, enableNameMangling).ParseEval();
    }

    public static InteractiveNode ParseInteractive(PyCallContext context, CodeSource codeSource, TokenSequence tokens, bool enableNameMangling = true)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(codeSource);
        ArgumentNullException.ThrowIfNull(tokens);

        return new Parser(context, codeSource, tokens, enableNameMangling).ParseInteractive();
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
    private readonly TokenSequence _tokenSequence;
    private readonly int _optimizationLevel;
    private readonly bool _enableNameMangling;
    private readonly Stack<string> _classNameTrimmedStack = [];
    private int _position;

    private int TokenPosition
    {
        get => _position;
        set
        {
            _position = value;
            CurrentToken = _tokenSequence[_position];
        }
    }

    internal Token CurrentToken { get; private set; }

    internal TokenType CurrentTokenType => CurrentToken.Type;
    private ReadOnlySpan<char> CurrentTokenStringAsSpan => _codeSource.Code.GetString(CurrentToken.StringSpan);
    private HashSet<string>? _stringPool;

    private string CurrentTokenString => GetOrAddFromPool(CurrentTokenStringAsSpan);

    private bool IsCurrentIdentifier => CurrentTokenType is TokenType.Name && !IsKeyword(CurrentTokenStringAsSpan);

    // there should be no reentrancy risk where SharedBuilder is used
    private StringBuilder SharedBuilder => field ??= new StringBuilder();

    CodeMetaInfo? ICodeMetaInfoProvider.MetaInfo => CreateAstMetaInfo();

    internal Parser(PyCallContext context, CodeSource codeSource, TokenSequence tokens, bool enableNameMangling = true)
    {
        _context = context;
        // LiteralParser.LiteralEval:
        // context may be PyCallContext.NonContextDependency whose PyEnvironment is null
        _optimizationLevel = _context.Interpreter?.PyEnvironment.OptimizationLevel ?? 0;
        _tokenSequence = tokens;
        _codeSource = codeSource;
        _enableNameMangling = enableNameMangling;
        SkipUselessToken();
    }

    private string GetOrAddFromPool(ReadOnlySpan<char> span)
    {
        if (PyStrObject.InternPool.TryGetStaticInternedString(span, out var value))
            return value;

        _stringPool ??= [];

        if (_stringPool.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue(span, out value))
            return value;

        value = span.ToString();
        _stringPool.Add(value);
        return value;
    }

    private string MangleIdentifier(string identifier)
    {
        Debug.Assert(!identifier.Contains('.'));

        if (!_enableNameMangling)
            return identifier;

        if (!_classNameTrimmedStack.TryPeek(out var className))
            return identifier;

        if (className.Length is 0)
            return identifier;

        if (!identifier.StartsWith("__", StringComparison.Ordinal))
            return identifier;

        if (identifier.EndsWith("__", StringComparison.Ordinal))
            return identifier;

        Span<char> mangledName = stackalloc char[1 + className.Length + identifier.Length];
        mangledName[0] = '_';
        className.CopyTo(mangledName[1..]);
        identifier.CopyTo(mangledName[^identifier.Length..]);
        return GetOrAddFromPool(mangledName);
    }

    private bool IsCurrentTypeTokenAnyOf(params ReadOnlySpan<TokenType> expectedTypes)
    {
        return expectedTypes.Contains(CurrentTokenType);
    }

    public PyRuntimeException SyntaxError(string message = PySR.InvalidSyntax, params ReadOnlySpan<object?> args)
    {
        return _context.SyntaxError(this, message, args);
    }

    private static bool IsUselessToken(Token tokenInfo)
    {
        return tokenInfo.Type is TokenType.NL or TokenType.Comment;
    }
    private void SkipUselessToken()
    {
        var span = _tokenSequence.AsSpan();
        CurrentToken = span[_position];
        while (IsUselessToken(CurrentToken))
            CurrentToken = span[++_position];
    }

    internal void MoveNextToken()
    {
        if (CurrentTokenType is TokenType.EndMarker)
            return;

        _position++;
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
    private bool IsMatchKeywordsSequence(params ReadOnlySpan<string> keywords)
    {
        var pos = TokenPosition;
        var result = true;
        foreach (var keyword in keywords)
        {
            if (!IsKeyword(keyword))
            {
                result = false;
                break;
            }

            MoveNextToken();
        }
        TokenPosition = pos;
        return result;
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