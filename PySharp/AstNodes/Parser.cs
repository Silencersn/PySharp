using PySharp.CodeAnalysis;
using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;
using PySharp.Tokenization;
using System.Collections.Frozen;
using System.Diagnostics;
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
    private int _comprehensionDepth;

    private int TokenStreamPosition
    {
        get => _tokenStream.Position;
        set => _tokenStream.Position = value;
    }

    private ScopeContext Context { get; } = new();
    private VariableScope CurrentScope => Context.CurrentScope;

    internal TokenInfo CurrentToken
    {
        get
        {
            while (_tokenStream.CurrentToken.Type is TokenType.NL or TokenType.Comment)
            {
                _tokenStream.MoveNextToken();
            }

            return _tokenStream.CurrentToken;
        }
    }

    private TokenType CurrentTokenType => CurrentToken.Type;

    bool ICodeMetaInfoProvider.OnlyStartInfo => true;
    CodeMetaInfo? ICodeMetaInfoProvider.MetaInfo => CreateMetaInfo();

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

    private void MoveNextToken()
    {
        if (_tokenStream.CurrentToken.Type is TokenType.EndMarker)
            return;

        _tokenStream.MoveNextToken();
    }
    private void EnsureTokenType(TokenType type)
    {
        if (CurrentTokenType != type)
            throw _context.ThrowableSyntaxError("invalid syntax");
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

        return CurrentToken.String == keyword;
    }
    private void EnsureKeywordThenMove(string keyword)
    {
        EnsureTokenType(TokenType.Name);
        if (CurrentToken.String != keyword)
            throw _context.ThrowableSyntaxError("invalid syntax");
        MoveNextToken();
    }
    private void EnsureTokenTypeThenMove(TokenType type)
    {
        EnsureTokenType(type);
        MoveNextToken();
    }
    private void EnsureTokenTypeThenMoveForTest(TokenType type, AstExprNode? testExpr)
    {
        EnsureTokenTypeForTest(type, testExpr);
        MoveNextToken();
    }
    private CodeMetaInfo CreateMetaInfo()
    {
        return new CodeMetaInfo()
        {
            Source = _codeSource,
            Start = CurrentToken.Start,
            End = CurrentToken.End,
        };
    }

    private CodeMetaInfo CopyThenMarkCrucial(CodeMetaInfo metaInfo)
    {
        return new CodeMetaInfo
        {
            Source = metaInfo.Source,
            Start = metaInfo.Start,
            End = metaInfo.End,
            CrucialStart = CurrentToken.Start,
        };
    }
    private void MarkCrucialForOneToken(CodeMetaInfo metaInfo)
    {
        metaInfo.CrucialStart = CurrentToken.Start;
        metaInfo.CrucialEnd = CurrentToken.End;
    }
    private CodeMetaInfo CopyThenMarkCrucialForOneToken(CodeMetaInfo metaInfo)
    {
        return new CodeMetaInfo
        {
            Source = metaInfo.Source,
            Start = metaInfo.Start,
            End = metaInfo.End,
            CrucialStart = CurrentToken.Start,
            CrucialEnd = CurrentToken.End
        };
    }
    private CodeMetaInfo WithAllEnd(CodeMetaInfo metaInfo)
    {
        metaInfo.End = CurrentToken.End;
        metaInfo.CrucialEnd = CurrentToken.End;
        return metaInfo;
    }
    private static CodeMetaInfo? WithEndOfOtherNode(CodeMetaInfo metaInfo, AstNode otherNode)
    {
        if (otherNode.MetaInfo is null)
            return null;

        metaInfo.End = otherNode.MetaInfo.End;
        return metaInfo;
    }
    private CodeMetaInfo CopyThenWithEnd(CodeMetaInfo metaInfo)
    {
        return new CodeMetaInfo
        {
            Source = metaInfo.Source,
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
        SemanticAnalysis(CurrentScope);

        return module;
    }

    public ExpressionNode ParseExpressionNode()
    {
        EnsureTokenTypeThenMove(TokenType.Encoding);

        var metaInfo = CreateMetaInfo();
        var exprList = ParseExpressionList(StopPredicates.UntilNewLine, out var endsWithComma);
        var expr = UnwrapOrMakeTuple(exprList, endsWithComma);

        Debug.Assert(CurrentScope.IsRoot);
        SemanticAnalysis(CurrentScope);

        return new ExpressionNode(expr) { MetaInfo = metaInfo };
    }

    public InteractiveNode ParseInteractiveNode()
    {
        EnsureTokenTypeThenMove(TokenType.Encoding);

        var metaInfo = CreateMetaInfo();
        _isParsingInteractiveNode = true;
        var list = ParseStatement();
        _isParsingInteractiveNode = false;

        Debug.Assert(CurrentScope.IsRoot);
        SemanticAnalysis(CurrentScope);
        CurrentScope.Children.Clear();

        return new InteractiveNode(list) { MetaInfo = metaInfo };
    }

    private void SemanticAnalysis(VariableScope scope)
    {
        FillUnknownVariables(scope);
        FillCapturedVariables(scope);
        FillNodeProperties(scope);
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

    private void FillCapturedVariables(VariableScope scope)
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
                throw _context.ThrowableSyntaxError($"no binding for nonlocal '{name}' found");
        }

        foreach (var child in scope.Children)
        {
            FillCapturedVariables(child);
        }
    }

    private static void FillNodeProperties(VariableScope scope)
    {
        foreach (var child in scope.Children)
            FillNodeProperties(child);

        if (scope.Owner is null)
            return;

        SetCapturedVariableTypes(scope);

        scope.Owner.Variables = scope.Variables.ToFrozenDictionary();

        if (scope.Owner is IFunctionOrLambda node)
        {
            node.CapturedVariables = [.. scope.CapturedVariables];
            node.LocalVariables = [.. node.Variables
                    .Where(pair => pair.Value is PyVariableType.Local or PyVariableType.Parameter)
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

                if (scope.Variables.TryGetValue("super", out var type) && type is not (PyVariableType.Parameter or PyVariableType.CapturedParameter))
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

    private static void SetCapturedVariableTypes(VariableScope scope)
    {
        foreach (var capturedVariable in scope.CapturedVariables)
        {
            var variableType = scope.Variables[capturedVariable];
            Debug.Assert(variableType is PyVariableType.Local or PyVariableType.Parameter);
            scope.Variables[capturedVariable] = variableType is PyVariableType.Local ? PyVariableType.CapturedLocal : PyVariableType.CapturedParameter;
        }
    }
}
