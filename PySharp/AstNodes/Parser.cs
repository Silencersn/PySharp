using PySharp.PyRuntime.Calls;
using PySharp.PyRuntime.Metadata;
using PySharp.Tokenization;
using System.Collections.Frozen;
using System.Diagnostics;

namespace PySharp.AstNodes;

public sealed partial class Parser
{
    public static ModuleNode Parse(string sourceName, IEnumerable<TokenInfo> tokens, PyCallContext context)
    {
        ArgumentNullException.ThrowIfNull(sourceName);
        ArgumentNullException.ThrowIfNull(tokens);

        var result = new Parser(context, sourceName, context.PyEnvironment.OptimizationOptions, tokens).ParseModuleNode();
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

    private readonly PyCallContext _context;
    private readonly string _sourceName;
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

    internal Parser(PyCallContext context, string sourceName, TokenStream tokenStream, OptimizationOptions? options = null)
    {
        _context = context;
        _options = options ?? OptimizationOptions.O0;
        _tokenStream = tokenStream;
        _isEnd = false;
        _sourceName = sourceName;
    }
    internal Parser(PyCallContext context, string sourceName, OptimizationOptions options, IEnumerable<TokenInfo> tokens) : this(context, sourceName, new TokenArrayStream(tokens), options)
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
    private MetaInfo CreateMetaInfo()
    {
        return new MetaInfo()
        {
            SourceName = _sourceName,
            FirstLine = CurrentToken.Line,
            Start = CurrentToken.Start,
            End = CurrentToken.End,
        };
    }
    private void SetMetaInfoEnd(MetaInfo metaInfo)
    {
        metaInfo.End = CurrentToken.End;
    }
    private MetaInfo CopyThenMarkCrucial(MetaInfo metaInfo)
    {
        return new MetaInfo
        {
            SourceName = metaInfo.SourceName,
            FirstLine = metaInfo.FirstLine,
            Start = metaInfo.Start,
            End = metaInfo.End,
            CrucialStart = CurrentToken.Start,
        };
    }
    private void MarkCrucialForOneToken(MetaInfo metaInfo)
    {
        metaInfo.CrucialStart = CurrentToken.Start;
        metaInfo.CrucialEnd = CurrentToken.End;
    }
    private MetaInfo CopyThenMarkCrucialForOneToken(MetaInfo metaInfo)
    {
        return new MetaInfo
        {
            SourceName = metaInfo.SourceName,
            FirstLine = metaInfo.FirstLine,
            Start = metaInfo.Start,
            End = metaInfo.End,
            CrucialStart = CurrentToken.Start,
            CrucialEnd = CurrentToken.End
        };
    }
    private MetaInfo WithAllEnd(MetaInfo metaInfo)
    {
        metaInfo.End = CurrentToken.End;
        metaInfo.CrucialEnd = CurrentToken.End;
        return metaInfo;
    }
    private static MetaInfo? WithEndOfOtherNode(MetaInfo metaInfo, AstNode otherNode)
    {
        if (otherNode.MetaInfo is null)
            return null;

        metaInfo.End = otherNode.MetaInfo.End;
        return metaInfo;
    }
    private MetaInfo CopyThenWithEnd(MetaInfo metaInfo)
    {
        return new MetaInfo
        {
            SourceName = metaInfo.SourceName,
            FirstLine = metaInfo.FirstLine,
            Start = metaInfo.Start,
            End = CurrentToken.End,
        };
    }

    public ModuleNode ParseModuleNode()
    {
        EnsureTokenTypeThenMove(TokenType.Encoding);

        var module = new ModuleNode() { MetaInfo = CreateMetaInfo() };

        while (CurrentTokenType is not TokenType.EndMarker)
        {
            module.Body.AddRange(ParseStatement());
        }

        Debug.Assert(CurrentScope.IsRoot);
        FillUnknownVariables(CurrentScope);
        FillClosureVariables(CurrentScope);

        return module;
    }

    public ExpressionNode ParseExpressionNode()
    {
        EnsureTokenTypeThenMove(TokenType.Encoding);

        var metaInfo = CreateMetaInfo();
        var exprList = ParseExpressionList(StopPredicates.UntilNewLine, out var endsWithComma);
        var expr = UnwrapOrMakeTuple(exprList, endsWithComma);

        Debug.Assert(CurrentScope.IsRoot);
        FillUnknownVariables(CurrentScope);
        FillClosureVariables(CurrentScope);

        return new ExpressionNode(expr) { MetaInfo = metaInfo };
    }

    public InteractiveNode ParseInteractiveNode()
    {
        var metaInfo = CreateMetaInfo();
        _isParsingInteractiveNode = true;
        var list = ParseStatement();
        _isParsingInteractiveNode = false;

        Debug.Assert(CurrentScope.IsRoot);
        FillUnknownVariables(CurrentScope);
        FillClosureVariables(CurrentScope);
        CurrentScope.Children.Clear();

        return new InteractiveNode(list) { MetaInfo = metaInfo };
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
                if (wrapper.Variables.TryGetValue(name, out var variableType) && variableType is not PyVariableType.Closure)
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
                node.CapturedVariables = [.. scope.CapturedVariables];
                node.LocalVariables = [.. node.Variables
                    .Where(pair => pair.Value is PyVariableType.Local or PyVariableType.Parameter)
                    .Where(pair => !scope.CapturedVariables.Contains(pair.Key))
                    .Select(pair => pair.Key)];
            }

            if (scope.Owner is IFunctionOrClass functionOrClassNode)
            {
                Stack<string> names = [];
                var currentScope = scope;
                while (currentScope.Owner is not null)
                {
                    var nodeWithQualName = (IFunctionOrClass)currentScope.Owner;
                    if (!ReferenceEquals(nodeWithQualName, functionOrClassNode) && nodeWithQualName is FunctionDefNode)
                        names.Push("<locals>");
                    names.Push(nodeWithQualName.Name);

                    Debug.Assert(currentScope.Parent is not null);
                    if (currentScope.Parent.Variables[nodeWithQualName.Name] is PyVariableType.Global || currentScope.Parent.IsRoot)
                        break;
                    currentScope = currentScope.Parent;
                }
                functionOrClassNode.QualifiedName = string.Join('.', names);
            }

            if (scope.Owner is FunctionDefNode funcDefNode)
            {
                funcDefNode.IncludeSuper = IncludeSuper(scope);

                static bool IncludeSuper(VariableScope scope)
                {
                    Debug.Assert(scope.Owner is FunctionDefNode);

                    if (scope.Variables.TryGetValue("super", out var type) && type is not PyVariableType.Parameter)
                        return true;

                    foreach (var child in scope.Children)
                    {
                        if (child.Owner is FunctionDefNode && IncludeSuper(child))
                            return true;
                    }

                    return false;
                }
            }
        }
    }
}
