using PySharp.CodeAnalysis;
using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;
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

        return new Parser(context, codeSource, tokens).ParseModuleNode();
    }
    public static ExpressionNode ParseExpression(PyCallContext context, CodeSource codeSource, IEnumerable<TokenInfo> tokens)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(codeSource);
        ArgumentNullException.ThrowIfNull(tokens);

        return new Parser(context, codeSource, tokens).ParseExpressionNode();
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
    private bool _isParsingInteractiveNode;

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
    private void EnsureTokenType(TokenType type, string message = "invalid syntax")
    {
        if (CurrentTokenType != type)
            throw _context.ThrowableSyntaxError(message);
    }
    private PyRuntimeException ThrowableSyntaxErrorCausedByInvalidEqualAfterExpr(AstExprNode expr)
    {
        if (expr is NameNode)
            return _context.ThrowableSyntaxError("invalid syntax. Maybe you meant '==' or ':=' instead of '='?");

        return _context.ThrowableSyntaxError($"cannot assign to {AstUtils.GetExprNodeName(expr)} here. Maybe you meant '==' instead of '='?");
    }
    private void EnsureTokenTypeForTest(TokenType type, AstExprNode? testExpr)
    {
        if (CurrentTokenType != type)
        {
            if (testExpr is not null && CurrentTokenType is TokenType.Equal)
                throw ThrowableSyntaxErrorCausedByInvalidEqualAfterExpr(testExpr);
            throw _context.ThrowableSyntaxError("invalid syntax");
        }
    }
    private bool IsCurrentKeyword(string keyword)
    {
        if (CurrentTokenType is not TokenType.Name)
            return false;

        return CurrentToken.StringAsSpan.Equals(keyword, StringComparison.Ordinal);
    }
    private void EnsureKeywordThenMove(string keyword, string message = "invalid syntax")
    {
        EnsureTokenType(TokenType.Name);
        if (!IsCurrentKeyword(keyword))
            throw _context.ThrowableSyntaxError(message);
        MoveNextToken();
    }
    private void EnsureTokenTypeThenMove(TokenType type, string message = "invalid syntax")
    {
        EnsureTokenType(type, message);
        MoveNextToken();
    }
    private void EnsureTokenTypeThenMoveForTest(TokenType type, AstExprNode? testExpr)
    {
        EnsureTokenTypeForTest(type, testExpr);
        MoveNextToken();
    }

    public ModuleNode ParseModuleNode()
    {
        EnsureTokenTypeThenMove(TokenType.Encoding);

        var metaInfo = CreateAstMetaInfo();

        List<AstStmtNode> body = [];
        while (CurrentTokenType is not TokenType.EndMarker)
            body.AddRange(ParseStatement());

        return Ast.Module(body).With(metaInfo);
    }

    public ExpressionNode ParseExpressionNode()
    {
        EnsureTokenTypeThenMove(TokenType.Encoding);

        var metaInfo = CreateAstMetaInfo();
        var exprList = ParseExpressions(StopPredicates.UntilNewLine, out var endsWithComma);
        var body = UnwrapOrMakeTuple(exprList, endsWithComma);

        return Ast.Expression(body).With(metaInfo);
    }

    public InteractiveNode ParseInteractiveNode()
    {
        EnsureTokenTypeThenMove(TokenType.Encoding);

        var metaInfo = CreateAstMetaInfo();
        _isParsingInteractiveNode = true;
        var body = ParseStatement();
        _isParsingInteractiveNode = false;

        return Ast.Interactive(body).With(metaInfo);
    }
}
