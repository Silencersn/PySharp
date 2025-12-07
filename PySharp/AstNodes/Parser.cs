using PySharp.PyRuntime.Environments;
using PySharp.Tokenization;
using System.Collections.Frozen;
using System.Diagnostics;

namespace PySharp.AstNodes;

public sealed partial class Parser
{
    public static ModuleNode Parse(IEnumerable<TokenInfo> tokens, PyEnvironment? environment = null)
    {
        ArgumentNullException.ThrowIfNull(tokens);

        if (environment is null)
            return new Parser(OptimizationOptions.O0, tokens).ParseModuleNode();

        using var context = new PyEnvironmentContext(environment);
        var result = new Parser(environment.OptimizationOptions, tokens).ParseModuleNode();
        return result;
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

    private static bool IsKeyword(string name)
    {
        return Keywords.Contains(name);
    }
    private static bool IsAugOperator(TokenType type)
    {
        return AugOperators.Contains(type);
    }

    private readonly OptimizationOptions _options;
    private readonly TokenStream _tokenStream;
    private bool _isEnd;
    private bool _isParsingInteractiveNode;

    private int TokenStreamPosition
    {
        get => _tokenStream.Position;
        set => _tokenStream.Position = value;
    }

    private ScopeContext Context { get; } = new();
    private VariableScope CurrentScope => Context.CurrentScope;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private TokenInfo CurrentToken
    {
        get
        {
            while (_tokenStream.CurrentToken.Type is TokenType.NL or TokenType.Comment)
            {
                _tokenStream.MoveNextToken();
            }

            if (_tokenStream.CurrentToken.Type is TokenType.EndMarker)
                _isEnd = true;

            return _tokenStream.CurrentToken;
        }
    }
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private TokenType CurrentTokenType => CurrentToken.Type;

    internal Parser(TokenStream tokenStream, OptimizationOptions? options = null)
    {
        _options = options ?? OptimizationOptions.O0;
        _tokenStream = tokenStream;
        _isEnd = false;
    }
    internal Parser(OptimizationOptions options, IEnumerable<TokenInfo> tokens) : this(new TokenArrayStream(tokens), options)
    {
    }
    internal Parser(IEnumerable<TokenInfo> tokens) : this(OptimizationOptions.O0, tokens)
    {
    }

    private void MoveNextToken()
    {
        if (_isEnd)
            return;

        _tokenStream.MoveNextToken();
    }
    private void EnsureTokenType(TokenType type)
    {
        if (CurrentTokenType != type)
            throw new AstException($"expected token is {type} instead of {CurrentTokenType}");
    }
    private bool IsCurrentKeyword(string keyword)
    {
        if (CurrentTokenType is not TokenType.Name)
            return false;

        return CurrentToken.String == keyword;
    }
    private void EnsureKeywordThenMove(string keyword)
    {
        EnsureTokenType(TokenType.Name);
        if (CurrentToken.String != keyword)
            throw new AstException($"expected keyword '{keyword}' while actual is '{CurrentToken.String}'");
        MoveNextToken();
    }
    private void EnsureTokenTypeThenMove(TokenType type)
    {
        EnsureTokenType(type);
        MoveNextToken();
    }

    public ModuleNode ParseModuleNode()
    {
        EnsureTokenTypeThenMove(TokenType.Encoding);

        var module = new ModuleNode();

        while (CurrentTokenType is not TokenType.EndMarker)
        {
            module.Body.AddRange(ParseStatement());
        }

        Debug.Assert(CurrentScope.IsRoot);
        FillUnknownVariables(CurrentScope);
        FillClosureVariables(CurrentScope);

        return module.Reduce(_options);
    }

    public ExpressionNode ParseExpressionNode()
    {
        EnsureTokenTypeThenMove(TokenType.Encoding);

        var exprList = ParseExpressionList(StopPredicates.UntilNewLine, out var endsWithComma);
        var expr = UnwrapOrMakeTuple(exprList, endsWithComma);

        Debug.Assert(CurrentScope.IsRoot);
        FillUnknownVariables(CurrentScope);

        return new ExpressionNode(expr).Reduce(_options);
    }

    public InteractiveNode ParseInteractiveNode()
    {
        _isParsingInteractiveNode = true;
        var list = ParseStatement();
        _isParsingInteractiveNode = false;

        Debug.Assert(CurrentScope.IsRoot);
        FillUnknownVariables(CurrentScope);
        CurrentScope.Children.Clear();

        return new InteractiveNode(list).Reduce(_options);
    }

    private static void FillUnknownVariables(VariableScope scope)
    {
        if (!scope.IsRoot)
        {
            foreach (var pair in scope.Variables)
            {
                if (pair.Value is not PyVariableType.Unknown)
                    continue;

                scope.Variables[pair.Key] = PyVariableType.Global;

                foreach (var wrapper in EnumerateFuncDefOrLambdaToRoot(scope))
                {
                    if (wrapper.TryGetVariableType(pair.Key, out var type))
                    {
                        if (type is PyVariableType.Global)
                            break;

                        scope.Variables[pair.Key] = PyVariableType.Closure;
                    }
                }
            }
        }

        foreach (var child in scope.Children)
        {
            FillUnknownVariables(child);
        }
    }

    static IEnumerable<VariableScope> EnumerateFuncDefOrLambdaToRoot(VariableScope scope)
    {
        while (scope.Parent is not null)
        {
            scope = scope.Parent;
            if (scope.Owner is FunctionDefNode or LambdaNode)
                yield return scope;
        }
    }

    private static void FillLocalVariables(VariableScope scope)
    {
        var nodes = scope.TrackedNameNodes;
        foreach (var pair in scope.Variables)
        {
            if (pair.Value is not PyVariableType.Unknown)
                continue;

            if (nodes.Any(node => node.Ctx is ExprContext.Store or ExprContext.Del && node.Identifier == pair.Key))
                scope.Variables[pair.Key] = PyVariableType.Local;
        }
    }

    private static void FillClosureVariables(VariableScope scope)
    {
        foreach (var (name, type) in scope.Variables)
        {
            if (type is not PyVariableType.Closure)
                continue;

            bool found = false;
            foreach (var wrapper in EnumerateFuncDefOrLambdaToRoot(scope))
            {
                if (wrapper.Variables.ContainsKey(name))
                {
                    wrapper.CapturedVariables.Add(name);
                    found = true;
                    break;
                }
            }
            if (!found)
                Debug.Fail($"Closure variable '{name}' not found in enclosing scopes");
        }

        foreach (var child in scope.Children)
        {
            FillClosureVariables(child);
        }

        if (scope.Owner is not null)
        {
            scope.Owner.Variables = scope.Variables.ToFrozenDictionary();

            if (scope.Owner is IFunctionOrLambda node)
            {
                node.CapturedVariables = scope.CapturedVariables;
                node.LocalNamesCache = [.. node.Variables.Where(pair => pair.Value is PyVariableType.Local or PyVariableType.Parameter).Select(pair => pair.Key)];
            }
        }
    }
}
