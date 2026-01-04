using PySharp.Tokenization;
using System.Diagnostics;

namespace PySharp.AstNodes;

partial class Parser
{
    /// <summary>
    /// module: (<see cref="ParseIdentifier">identifier</see> ".")* <see cref="ParseIdentifier">identifier</see>
    /// </summary>
    /// <returns></returns>
    private string ParseModule()
    {
        List<string> modulePaths = [ParseIdentifier()];
        while (CurrentTokenType is TokenType.Dot)
        {
            MoveNextToken();
            modulePaths.Add(ParseIdentifier());
        }
        return string.Join('.', modulePaths);
    }

    /// <summary>
    /// relative_module:  "."* <see cref="ParseModule">module</see> | "."+
    /// </summary>
    /// <returns></returns>
    private (string? Module, int Level) ParseRelativeModule()
    {
        string? module = null;
        int level = 0;

        while (CurrentTokenType is TokenType.Dot)
        {
            level++;
            MoveNextToken();
        }

        if (CurrentTokenType is TokenType.Name && !IsKeyword(CurrentToken.String))
            module = ParseModule();

        if (module is null && level is 0)
            throw _context.ThrowableSyntaxError("invalid syntax");

        return (module, level);
    }

    /// <summary>
    /// import_stmt: "import" <see cref="ParseModule">module</see> ["as" <see cref="ParseIdentifier">identifier</see>] ("," <see cref="ParseModule">module</see> ["as" <see cref="ParseIdentifier">identifier</see>])*
    /// <br/>      | "from" <see cref="ParseRelativeModule">relative_module</see> "import" <see cref="ParseIdentifier">identifier</see> ["as" <see cref="ParseIdentifier">identifier</see>] ("," <see cref="ParseIdentifier">identifier</see> ["as" <see cref="ParseIdentifier">identifier</see>])*
    /// <br/>      | "from" <see cref="ParseRelativeModule">relative_module</see> "import" "(" <see cref="ParseIdentifier">identifier</see> ["as" <see cref="ParseIdentifier">identifier</see>]  ("," <see cref="ParseIdentifier">identifier</see> ["as" <see cref="ParseIdentifier">identifier</see>])* [","] ")"
    /// <br/>      | "from" <see cref="ParseRelativeModule">relative_module</see> "import" "*"
    /// </summary>
    /// <returns></returns>
    private AstStmtNode ParseImportStmt()
    {
        var metaInfo = CreateMetaInfo();
        if (IsCurrentKeyword("import"))
        {
            MoveNextToken();
            var importNode = new ImportNode();
            importNode.Names.Add(ParseAlias());

            while (CurrentTokenType is TokenType.Comma)
            {
                MoveNextToken();
                importNode.Names.Add(ParseAlias());
            }

            if (IsCurrentKeyword("from"))
                throw _context.ThrowableSyntaxError("Did you mean to use 'from ... import ...' instead?");

            importNode.MetaInfo = metaInfo;
            return importNode;

            AstAliasNode ParseAlias()
            {
                var module = ParseModule();
                var id = null as string;
                if (IsCurrentKeyword("as"))
                {
                    MoveNextToken();
                    id = ParseIdentifier();
                }
                return new AstAliasNode(module, id);
            }
        }
        else
        {
            EnsureKeywordThenMove("from");
            var (module, level) = ParseRelativeModule();
            EnsureKeywordThenMove("import");

            if (CurrentTokenType is TokenType.Star)
            {
                MoveNextToken();
                return new ImportFromNode(module, [new("*", null)], level).With(metaInfo);
            }
            else if (CurrentTokenType is TokenType.LeftParen)
            {
                MoveNextToken();

                List<AstAliasNode> names = [ParseAlias()];

                while (CurrentTokenType is TokenType.Comma)
                {
                    MoveNextToken();
                    if (CurrentTokenType is TokenType.RightParen)
                        break;
                    names.Add(ParseAlias());
                }

                EnsureTokenTypeThenMove(TokenType.RightParen);
                return new ImportFromNode(module, names, level).With(metaInfo);
            }
            else
            {
                List<AstAliasNode> names = [ParseAlias()];

                while (CurrentTokenType is TokenType.Comma)
                {
                    MoveNextToken();
                    names.Add(ParseAlias());
                }

                return new ImportFromNode(module, names, level).With(metaInfo);
            }

            AstAliasNode ParseAlias()
            {
                var name = ParseIdentifier();
                var asName = null as string;
                if (IsCurrentKeyword("as"))
                {
                    MoveNextToken();
                    asName = ParseIdentifier();
                }
                return new AstAliasNode(name, asName);
            }
        }
    }

    private AstStmtNode ParseSimpleStmt()
    {
        var metaInfo = CreateMetaInfo();
        if (CurrentTokenType is TokenType.Name && IsKeyword(CurrentToken.String))
        {
            var keyword = CurrentToken.String;
            if (keyword is "break")
            {
                MoveNextToken();
                return new BreakNode().With(metaInfo);
            }
            else if (keyword is "continue")
            {
                MoveNextToken();
                return new ContinueNode().With(metaInfo);
            }
            else if (keyword is "raise")
            {
                MoveNextToken();
                if (CurrentTokenType is not (TokenType.NewLine or TokenType.Semicolon))
                {
                    var exc = ParseExpression();

                    if (CurrentTokenType is not (TokenType.NewLine or TokenType.Semicolon))
                    {
                        EnsureKeywordThenMove("from");

                        var cause = ParseExpression();

                        return new RaiseNode(exc, cause).With(metaInfo);
                    }

                    return new RaiseNode(exc, null).With(metaInfo);
                }

                return new RaiseNode(null, null).With(metaInfo);
            }
            else if (keyword is "return")
            {
                MoveNextToken();
                if (CurrentTokenType is TokenType.NewLine or TokenType.Semicolon)
                    return new ReturnNode().With(metaInfo);

                var list = ParseExpressionList(StopPredicates.UntilNewLineOrSemicolon, out var comma);
                return new ReturnNode(UnwrapOrMakeTuple(list, comma)).With(metaInfo);
            }
            else if (keyword is "pass")
            {
                MoveNextToken();
                return new PassNode().With(metaInfo);
            }
            else if (keyword is "del")
            {
                MoveNextToken();
                var targets = ParseTargetList(StopPredicates.UntilNewLineOrSemicolon, out _);
                return AstNodeFactory.Delete(targets).With(metaInfo);
            }
            else if (keyword is "import" or "from")
            {
                return ParseImportStmt();
            }
            else if (keyword is "assert")
            {
                MoveNextToken();
                var test = ParseExpression();
                AssertNode node;
                if (CurrentTokenType is TokenType.Comma)
                {
                    MoveNextToken();
                    var msg = ParseExpression();
                    node = AstNodeFactory.Assert(test, msg);
                }
                else
                {
                    node = AstNodeFactory.Assert(test);
                }
                node.MetaInfo = metaInfo;
                return node;
            }
            else if (keyword is "global")
            {
                MoveNextToken();
                var names = ParseIdentifiers();
                var node = AstNodeFactory.Global(names);
                node.MetaInfo = metaInfo;
                return node;
            }
            else if (keyword is "nonlocal")
            {
                MoveNextToken();
                var names = ParseIdentifiers();
                var node = AstNodeFactory.Nonlocal(names);
                node.MetaInfo = metaInfo;
                return node;
            }
            else if (keyword is "yield")
            {
                var yieldExpr = ParseYieldExpression();
                return AstNodeFactory.Expr(yieldExpr).With(metaInfo);
            }
        }


        var exprList = ParseExpressionList(StopPredicates.UntilNewLineOrSemicolonOrEqual, out var endsWithComma);

        if (CurrentTokenType is TokenType.Equal)
        {
            var allTargets = exprList.All(AstUtils.IsValidTarget);
            List<AstExprNode> targets = [];
            while (CurrentTokenType is TokenType.Equal)
            {
                if (!allTargets)
                    throw _context.ThrowableSyntaxError("illegal expression on left side of =");

                targets.Add(UnwrapOrMakeTuple(exprList, endsWithComma));
                MoveNextToken();
                exprList = ParseStarredExpressionList(StopPredicates.UntilNewLineOrSemicolonOrEqual, out endsWithComma);
                allTargets = exprList.All(AstUtils.IsValidTarget);
            }

            var node = AstNodeFactory.Assign(targets, UnwrapOrMakeTuple(exprList, endsWithComma));
            node.MetaInfo = metaInfo;
            return node;
        }

        if (IsAugOperator(CurrentTokenType))
        {
            var target = UnwrapOrMakeTuple(exprList, endsWithComma);

            if (!AstUtils.IsValidAugtarget(target))
                throw _context.ThrowableSyntaxError($"'{AstUtils.GetExprNodeName(target)}' is an illegal expression for augmented assignment");
            AstUtils.SetContext(target, ExprContext.Store);

            AstOperatorNode op = CurrentTokenType switch
            {
                TokenType.PlusEqual => AddNode.Shared,
                TokenType.MinusEqual => SubNode.Shared,
                TokenType.StarEqual => MulNode.Shared,
                TokenType.AtEqual => throw new NotImplementedException(),
                TokenType.SlashEqual => DivNode.Shared,
                TokenType.DoubleSlashEqual => FloorDivNode.Shared,
                TokenType.PercentEqual => ModNode.Shared,
                TokenType.DoubleStarEqual => PowNode.Shared,
                TokenType.RightShiftEqual => LShiftNode.Shared,
                TokenType.LeftShiftEqual => RShiftNode.Shared,
                TokenType.AmpersandEqual => BitAndNode.Shared,
                TokenType.CaretEqual => BitXorNode.Shared,
                TokenType.PipeEqual => BitOrNode.Shared,

                _ => throw new UnreachableException(),
            };
            MoveNextToken();
            var list = ParseExpressionList(StopPredicates.UntilNewLineOrSemicolon, out var comma);
            var value = UnwrapOrMakeTuple(list, comma);
            return AstNodeFactory.AugAssign(target, op, value).With(metaInfo);
        }

        return AstNodeFactory.Expr(UnwrapOrMakeTuple(exprList, endsWithComma)).With(metaInfo);
    }

    private List<string> ParseIdentifiers()
    {
        List<string> identifiers = [ParseIdentifier()];
        while (CurrentTokenType is TokenType.Comma)
        {
            MoveNextToken();
            identifiers.Add(ParseIdentifier());
        }
        return identifiers;
    }

    private List<AstStmtNode> ParseStmtList()
    {
        var simpleStmts = new List<AstStmtNode>();
        var simpleStmt = ParseSimpleStmt();
        simpleStmts.Add(simpleStmt);

        while (CurrentTokenType is TokenType.Semicolon)
        {
            MoveNextToken();
            if (CurrentTokenType is TokenType.NewLine)
                break;
            simpleStmt = ParseSimpleStmt();
            simpleStmts.Add(simpleStmt);
        }

        EnsureTokenTypeThenMove(TokenType.NewLine);
        return simpleStmts;
    }

    private List<AstStmtNode> ParseStatement()
    {
        List<AstExprNode> decorators = [];
        while (CurrentTokenType is TokenType.At)
        {
            MoveNextToken();
            var decorator = ParseAssignmentExpression();
            decorators.Add(decorator);
            EnsureTokenTypeThenMove(TokenType.NewLine);
        }

        if (decorators.Count > 0 && (CurrentTokenType is not TokenType.Name || CurrentToken.String is not ("def" or "class")))
            throw _context.ThrowableSyntaxError("invalid syntax");

        if (CurrentTokenType is TokenType.Name && CompoundStmtStartsWith.Contains(CurrentToken.String))
            return [ParseCompoundStmt(decorators)];

        return ParseStmtList();
    }

    private static readonly string[] CompoundStmtStartsWith = [
        "if", "while", "for", "try", "with", "match", "def", "class", "async"
        ];

    private AstStmtNode ParseCompoundStmt(List<AstExprNode> decorators)
    {
        _tokenStream._parsingCompoundStmt++;
        EnsureTokenType(TokenType.Name);
        AstStmtNode node = CurrentToken.String switch
        {
            "if" => ParseIfStmt("if"),
            "while" => ParseWhileStmt(),
            "for" => ParseForStmt(),
            "try" => ParseTryStmt(),
            "def" => ParseFuncDef(decorators),
            "class" => ParseClassDef(decorators),

            _ => throw new NotSupportedException()
        };
        _tokenStream._parsingCompoundStmt--;

        if (_tokenStream._parsingCompoundStmt is 0 && _isParsingInteractiveNode)
            EnsureTokenTypeThenMove(TokenType.NewLine);

        return node;
    }

    private List<AstStmtNode> ParseSuite(string keyword)
    {
        var lineno = CurrentToken.Start.Line;
        if (CurrentTokenType is TokenType.NewLine)
        {
            MoveNextToken();
            if (CurrentTokenType is not TokenType.Indent)
            {
                var statementName = keyword switch
                {
                    "def" => "function definition",
                    "class" => "class definition",

                    _ => $"'{keyword}' statement"
                };
                throw _context.ThrowableIndentationError($"expected an indented block after {statementName} on line {lineno}");
            }
            MoveNextToken();

            List<AstStmtNode> stmts = [];
            while (CurrentTokenType is not TokenType.Dedent)
            {
                stmts.AddRange(ParseStatement());
            }

            MoveNextToken();
            return stmts;
        }

        return ParseStmtList();
    }

    private IfNode ParseIfStmt(string startsWithKeyword)
    {
        var metaInfo = CreateMetaInfo();
        EnsureKeywordThenMove(startsWithKeyword);
        var ifNode = new IfNode(ParseExpression()).With(metaInfo);
        EnsureTokenTypeThenMoveForTest(TokenType.Colon, ifNode.Test);
        ifNode.Body.AddRange(ParseSuite(startsWithKeyword));
        if (IsCurrentKeyword("elif"))
        {
            ifNode.OrElse.Add(ParseIfStmt("elif"));
        }
        else if (IsCurrentKeyword("else"))
        {
            MoveNextToken();
            EnsureTokenTypeThenMove(TokenType.Colon);
            ifNode.OrElse.AddRange(ParseSuite("else"));
        }
        return ifNode;
    }

    private WhileNode ParseWhileStmt()
    {
        var metaInfo = CreateMetaInfo();
        EnsureKeywordThenMove("while");
        var whileNode = new WhileNode(ParseExpression()).With(metaInfo);
        EnsureTokenTypeThenMoveForTest(TokenType.Colon, whileNode.Test);
        whileNode.Body.AddRange(ParseSuite("while"));
        if (IsCurrentKeyword("else"))
        {
            MoveNextToken();
            EnsureTokenTypeThenMove(TokenType.Colon);
            whileNode.OrElse.AddRange(ParseSuite("else"));
        }
        return whileNode;
    }

    private TryNode ParseTryStmt()
    {
        var metaInfo = CreateMetaInfo();
        EnsureKeywordThenMove("try");
        var tryNode = new TryNode().With(metaInfo);
        EnsureTokenTypeThenMove(TokenType.Colon);
        tryNode.Body.AddRange(ParseSuite("try"));
        if (!IsCurrentKeyword("except") && !IsCurrentKeyword("finally"))
            throw _context.ThrowableSyntaxError("expected 'except' or 'finally' block");
        if (IsCurrentKeyword("except"))
        {
            while (IsCurrentKeyword("except"))
            {
                AstExprNode? expr = null;
                string? id = null;
                MoveNextToken();

                if (CurrentTokenType is not TokenType.Colon)
                {
                    expr = ParseExpression();

                    if (CurrentTokenType is not TokenType.Colon)
                    {
                        EnsureKeywordThenMove("as");
                        id = ParseIdentifier();
                    }
                }

                EnsureTokenTypeThenMove(TokenType.Colon);

                var expectHandler = new ExceptHandlerNode(expr, id);
                expectHandler.Body.AddRange(ParseSuite("except"));
                tryNode.Exceptors.Add(expectHandler);
            }
            if (IsCurrentKeyword("else"))
            {
                MoveNextToken();
                EnsureTokenTypeThenMove(TokenType.Colon);
                tryNode.OrElse.AddRange(ParseSuite("else"));
            }
        }
        if (IsCurrentKeyword("finally"))
        {
            MoveNextToken();
            EnsureTokenTypeThenMove(TokenType.Colon);
            tryNode.FinalBody.AddRange(ParseSuite("finally"));
        }
        return tryNode;
    }

    private ForNode ParseForStmt()
    {
        var metaInfo = CreateMetaInfo();
        EnsureKeywordThenMove("for");
        var targetList = ParseTargetList(StopPredicates.UntilKeywordIn, out var endsWithComma);
        var target = UnwrapOrMakeTuple(targetList, endsWithComma);
        AstUtils.SetContext(target, ExprContext.Store);
        EnsureKeywordThenMove("in");
        var iter = ParseExpression();
        EnsureTokenTypeThenMove(TokenType.Colon);
        var forNode = new ForNode(target, iter).With(metaInfo);
        forNode.Body.AddRange(ParseSuite("for"));
        if (IsCurrentKeyword("else"))
        {
            MoveNextToken();
            EnsureTokenTypeThenMove(TokenType.Colon);
            forNode.OrElse.AddRange(ParseSuite("else"));
        }
        return forNode;
    }

    private FunctionDefNode ParseFuncDef(IEnumerable<AstExprNode> decorators)
    {
        var metaInfo = CreateMetaInfo();
        EnsureKeywordThenMove("def");
        var name = ParseIdentifier();
        EnsureTokenTypeThenMove(TokenType.LeftParen);
        var args = CurrentTokenType is TokenType.RightParen ? new() : ParseParameterList(StopPredicates.UntilRightParen);
        EnsureTokenTypeThenMove(TokenType.RightParen);
        EnsureTokenTypeThenMove(TokenType.Colon);

        var funcDef = new FunctionDefNode(name, args);
        funcDef.DecoratorList.AddRange(decorators);
        funcDef.Body.AddRange(ParseSuite("def"));
        funcDef.MetaInfo = metaInfo;
        return funcDef;
    }

    private ClassDefNode ParseClassDef(IEnumerable<AstExprNode> decorators)
    {
        var metaInfo = CreateMetaInfo();
        EnsureKeywordThenMove("class");
        var name = ParseIdentifier();
        var args = new List<AstExprNode>();
        var kwargs = new List<AstKeywordNode>();

        if (CurrentTokenType is TokenType.LeftParen)
        {
            MoveNextToken();
            (args, kwargs) = ParseArgumentList();
            EnsureTokenTypeThenMove(TokenType.RightParen);
        }

        EnsureTokenTypeThenMove(TokenType.Colon);

        var classDefNode = new ClassDefNode(metaInfo, name);
        classDefNode.Bases.AddRange(args);
        classDefNode.Keywords.AddRange(kwargs);
        classDefNode.DecoratorList.AddRange(decorators);
        classDefNode.Body.AddRange(ParseSuite("class"));
        return classDefNode;
    }
}
