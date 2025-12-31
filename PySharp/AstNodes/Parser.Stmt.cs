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
                CurrentScope.TrySetLocalIfNotExistsOrUnknown(id ?? module);
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
                return new ImportFromNode(module, [new("*", null)], level) { MetaInfo = metaInfo };
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
                return new ImportFromNode(module, names, level) { MetaInfo = metaInfo };
            }
            else
            {
                List<AstAliasNode> names = [ParseAlias()];

                while (CurrentTokenType is TokenType.Comma)
                {
                    MoveNextToken();
                    names.Add(ParseAlias());
                }

                return new ImportFromNode(module, names, level) { MetaInfo = metaInfo };
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
                CurrentScope.TrySetLocalIfNotExistsOrUnknown(asName ?? name);
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
                if (!CurrentScope.IsInLoop)
                    throw _context.ThrowableSyntaxError("'break' outside loop");

                MoveNextToken();
                return new BreakNode() { MetaInfo = metaInfo };
            }
            else if (keyword is "continue")
            {
                if (!CurrentScope.IsInLoop)
                    throw _context.ThrowableSyntaxError("'continue' outside loop");

                MoveNextToken();
                return new ContinueNode() { MetaInfo = metaInfo };
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

                        return new RaiseNode(exc, cause) { MetaInfo = metaInfo };
                    }

                    return new RaiseNode(exc, null) { MetaInfo = metaInfo };
                }

                return new RaiseNode(null, null) { MetaInfo = metaInfo };
            }
            else if (keyword is "return")
            {
                if (!CurrentScope.IsCurrentFuncDef)
                    throw _context.ThrowableSyntaxError("'return' outside function");

                MoveNextToken();
                if (CurrentTokenType is TokenType.NewLine or TokenType.Semicolon)
                    return new ReturnNode() { MetaInfo = metaInfo };

                var list = ParseExpressionList(StopPredicates.UntilNewLineOrSemicolon, out var comma);
                if (list.Count is 1 && !comma)
                    return new ReturnNode(list[0]) { MetaInfo = metaInfo };

                return new ReturnNode(AstNode.Tuple(list)) { MetaInfo = metaInfo };
            }
            else if (keyword is "pass")
            {
                MoveNextToken();
                return new PassNode() { MetaInfo = metaInfo };
            }
            else if (keyword is "del")
            {
                MoveNextToken();
                var targetList = ParseTargetList(StopPredicates.UntilNewLineOrSemicolon, out _);
                foreach (var target in targetList)
                    TrySetTargetContext(target, ExprContext.Del);
                return new DeleteNode([.. targetList]) { MetaInfo = metaInfo };
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
                    node = AstNode.Assert(test, msg);
                }
                else
                {
                    node = AstNode.Assert(test);
                }
                node.MetaInfo = metaInfo;
                return node;
            }
            else if (keyword is "global")
            {
                MoveNextToken();
                var names = ParseIdentifiers();
                foreach (var name in names)
                {
                    if (CurrentScope.TryGetVariableType(name, out var variableType))
                    {
                        if (variableType is PyVariableType.Parameter)
                            throw _context.ThrowableSyntaxError($"name '{name}' is parameter and global");

                        if (variableType is PyVariableType.Nonlocal)
                            throw _context.ThrowableSyntaxError($"name '{name}' is nonlocal and global");

                        if (variableType is PyVariableType.Local)
                            throw _context.ThrowableSyntaxError($"name '{name}' is assigned to before nonlocal declaration");

                        foreach (var nameNode in CurrentScope.TrackedNameNodes)
                        {
                            if (nameNode.Identifier == name)
                            {
                                if (nameNode.Ctx is ExprContext.Load)
                                    throw _context.ThrowableSyntaxError($"name '{name}' is used prior to global declaration");
                                else
                                    throw _context.ThrowableSyntaxError($"name '{name}' is assigned to before global declaration");
                            }
                        }
                    }

                    CurrentScope.SetGlobal(name);
                }
                var node = AstNode.Global(names);
                node.MetaInfo = metaInfo;
                return node;
            }
            else if (keyword is "nonlocal")
            {
                if (!CurrentScope.IsInFuncDef)
                    throw _context.ThrowableSyntaxError("nonlocal declaration not allowed at module level");

                MoveNextToken();
                var names = ParseIdentifiers();
                foreach (var name in names)
                {
                    if (CurrentScope.TryGetVariableType(name, out var variableType))
                    {
                        if (variableType is PyVariableType.Parameter)
                            throw _context.ThrowableSyntaxError($"name '{name}' is parameter and nonlocal");

                        if (variableType is PyVariableType.Global)
                            throw _context.ThrowableSyntaxError($"name '{name}' is nonlocal and global");

                        if (variableType is PyVariableType.Local)
                            throw _context.ThrowableSyntaxError($"name '{name}' is assigned to before nonlocal declaration");

                        foreach (var nameNode in CurrentScope.TrackedNameNodes)
                        {
                            if (nameNode.Identifier == name)
                            {
                                if (nameNode.Ctx is ExprContext.Load)
                                    throw _context.ThrowableSyntaxError($"name '{name}' is used prior to nonlocal declaration");
                                else
                                    throw _context.ThrowableSyntaxError($"name '{name}' is assigned to before nonlocal declaration");
                            }
                        }
                    }

                    CurrentScope.SetNonlocal(name);
                }
                var node = AstNode.Nonlocal(names);
                node.MetaInfo = metaInfo;
                return node;
            }
            else if (keyword is "yield")
            {
                var yieldExpr = ParseYieldExpression();
                return new ExprNode(yieldExpr) { MetaInfo = metaInfo };
            }
        }


        var exprList = ParseExpressionList(StopPredicates.UntilNewLineOrSemicolonOrEqual, out var endsWithComma);

        if (CurrentTokenType is TokenType.Equal)
        {
            var allTargets = exprList.All(IsValidTarget);
            List<AstExprNode> targets = [];
            while (CurrentTokenType is TokenType.Equal)
            {
                if (!allTargets)
                    throw _context.ThrowableSyntaxError("illegal expression on left side of =");

                targets.Add(UnwrapOrMakeTuple(exprList, endsWithComma));
                MoveNextToken();
                exprList = ParseExpressionList(StopPredicates.UntilNewLineOrSemicolonOrEqual, out endsWithComma);
                allTargets = exprList.All(IsValidTarget);
            }

            var node = AstNode.Assign(UnwrapOrMakeTuple(exprList, endsWithComma), targets);
            node.MetaInfo = metaInfo;
            return node;
        }

        if (IsAugOperator(CurrentTokenType))
        {
            var target = UnwrapOrMakeTuple(exprList, endsWithComma);

            if (!IsValidAugtarget(target))
                throw _context.ThrowableSyntaxError($"'{target.GetType().Name /* TODO: using 'list' instead of 'ListNode' */ }' is an illegal expression for augmented assignment");

            TrySetTargetContext(target, ExprContext.Store);

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
            return new AugAssignNode(target, op, value) { MetaInfo = metaInfo };
        }

        return new ExprNode(UnwrapOrMakeTuple(exprList, endsWithComma)) { MetaInfo = metaInfo };
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
        var ifNode = new IfNode(ParseExpression()) { MetaInfo = metaInfo };
        EnsureTokenTypeThenMove(TokenType.Colon);
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
        var whileNode = new WhileNode(ParseExpression()) { MetaInfo = metaInfo };
        EnsureTokenTypeThenMove(TokenType.Colon);
        CurrentScope.EnterLoop();
        whileNode.Body.AddRange(ParseSuite("while"));
        CurrentScope.ExitLoop();
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
        var tryNode = new TryNode() { MetaInfo = metaInfo };
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
                        CurrentScope.TrySetLocalIfNotExistsOrUnknown(id);
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
        TrySetTargetContext(target, ExprContext.Store);
        EnsureKeywordThenMove("in");
        var iter = ParseExpression();
        EnsureTokenTypeThenMove(TokenType.Colon);
        var forNode = new ForNode(target, iter) { MetaInfo = metaInfo };
        CurrentScope.EnterLoop();
        forNode.Body.AddRange(ParseSuite("for"));
        CurrentScope.ExitLoop();
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
        StartParsingFuncDef();
        funcDef.Body.AddRange(ParseSuite("def"));
        EndParsingFuncDef();
        funcDef.MetaInfo = metaInfo;
        return funcDef;

        void StartParsingFuncDef()
        {
            CurrentScope.TrySetLocalIfNotExistsOrUnknown(name);
            Context.EnterScope(funcDef);
            CurrentScope.AddParameters(args);
        }

        void EndParsingFuncDef()
        {
            var scope = Context.ExitScope();
            FillLocalVariables(scope);
        }
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
        StartParsingClassDef();
        classDefNode.Body.AddRange(ParseSuite("class"));
        EndParsingClassDef();
        return classDefNode;

        void StartParsingClassDef()
        {
            CurrentScope.TrySetLocalIfNotExistsOrUnknown(name);
            Context.EnterScope(classDefNode);
        }

        void EndParsingClassDef()
        {
            var scope = Context.ExitScope();
            FillLocalVariables(scope);
        }
    }
}
