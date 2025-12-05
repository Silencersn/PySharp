using PySharp.PyRuntime;
using PySharp.Tokenization;

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
        {
            PyVirtualMachine.RaiseSyntaxError("invalid syntax");
            throw new PyRuntimeException(PyVirtualMachine.CurrentException);
        }
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
                return new ImportFromNode(module, [new("*", null)], level);
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
                return new ImportFromNode(module, names, level);
            }
            else
            {
                List<AstAliasNode> names = [ParseAlias()];

                while (CurrentTokenType is TokenType.Comma)
                {
                    MoveNextToken();
                    names.Add(ParseAlias());
                }

                return new ImportFromNode(module, names, level);
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
        if (CurrentTokenType is TokenType.Name && IsKeyword(CurrentToken.String))
        {
            var keyword = CurrentToken.String;
            if (keyword is "break")
            {
                if (!CurrentScope.IsInLoop)
                    throw new AstException("'break' outside loop");

                MoveNextToken();
                return new BreakNode();
            }
            else if (keyword is "continue")
            {
                if (!CurrentScope.IsInLoop)
                    throw new AstException("'continue' outside loop");

                MoveNextToken();
                return new ContinueNode();
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

                        return new RaiseNode(exc, cause);
                    }

                    return new RaiseNode(exc, null);
                }

                return new RaiseNode(null, null);
            }
            else if (keyword is "return")
            {
                if (!CurrentScope.IsCurrentFuncDef)
                    throw new AstException("'return' outside function");

                MoveNextToken();
                if (CurrentTokenType is TokenType.NewLine or TokenType.Semicolon)
                    return new ReturnNode();

                var list = ParseExpressionList(StopPredicates.UntilNewLineOrSemicolon, out var comma);
                if (list.Count is 1 && !comma)
                    return new ReturnNode(list[0]);

                return new ReturnNode(AstNode.Tuple(list));
            }
            else if (keyword is "pass")
            {
                MoveNextToken();
                return new PassNode();
            }
            else if (keyword is "del")
            {
                MoveNextToken();
                var targetList = ParseTargetList(StopPredicates.UntilNewLineOrSemicolon, out _);
                return new DeleteNode([.. targetList]);
            }
            else if (keyword is "import" or "from")
            {
                return ParseImportStmt();
            }
            else if (keyword is "assert")
            {
                MoveNextToken();
                var test = ParseExpression();
                if (CurrentTokenType is TokenType.Comma)
                {
                    MoveNextToken();
                    var msg = ParseExpression();
                    return AstNode.Assert(test, msg);
                }
                return AstNode.Assert(test);
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
                            throw new AstException($"name '{name}' is parameter and global");

                        if (variableType is PyVariableType.Nonlocal)
                            throw new AstException($"name '{name}' is nonlocal and global");

                        if (variableType is PyVariableType.Local)
                            throw new AstException($"name '{name}' is assigned to before nonlocal declaration");

                        foreach (var node in CurrentScope.TrackedNameNodes)
                        {
                            if (node.Identifier == name)
                            {
                                if (node.Ctx is ExprContext.Load)
                                    throw new AstException($"name '{name}' is used prior to global declaration");
                                else
                                    throw new AstException($"name '{name}' is assigned to before global declaration");
                            }
                        }
                    }

                    CurrentScope.SetGlobal(name);
                }
                return AstNode.Global(names);

            }
            else if (keyword is "nonlocal")
            {
                if (!CurrentScope.IsInFuncDef)
                    throw new AstException("nonlocal declaration not allowed at module level");

                MoveNextToken();
                var names = ParseIdentifiers();
                foreach (var name in names)
                {
                    if (CurrentScope.TryGetVariableType(name, out var variableType))
                    {
                        if (variableType is PyVariableType.Parameter)
                            throw new AstException($"name '{name}' is parameter and nonlocal");

                        if (variableType is PyVariableType.Global)
                            throw new AstException($"name '{name}' is nonlocal and global");

                        if (variableType is PyVariableType.Local)
                            throw new AstException($"name '{name}' is assigned to before nonlocal declaration");

                        foreach (var node in CurrentScope.TrackedNameNodes)
                        {
                            if (node.Identifier == name)
                            {
                                if (node.Ctx is ExprContext.Load)
                                    throw new AstException($"name '{name}' is used prior to nonlocal declaration");
                                else
                                    throw new AstException($"name '{name}' is assigned to before nonlocal declaration");
                            }
                        }
                    }

                    CurrentScope.SetNonlocal(name);
                }
                return AstNode.Nonlocal(names);
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
                    throw new AstException("illegal expression on left side of =");

                targets.Add(UnwrapOrMakeTuple(exprList, endsWithComma));
                MoveNextToken();
                exprList = ParseExpressionList(StopPredicates.UntilNewLineOrSemicolonOrEqual, out endsWithComma);
                allTargets = exprList.All(IsValidTarget);
            }

            return AstNode.Assign(UnwrapOrMakeTuple(exprList, endsWithComma), targets);
        }

        if (IsAugOperator(CurrentTokenType))
        {
            if (exprList.Count is not 1)
                throw new AstException();

            if (!IsValidAugtarget(exprList[0]))
                throw new AstException();

            var target = exprList[0];
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

                _ => throw new AstException("why here?"),
            };
            MoveNextToken();
            var list = ParseExpressionList(StopPredicates.UntilNewLineOrSemicolon, out var comma);
            var value = UnwrapOrMakeTuple(list, comma);
            return new AugAssignNode(target, op, value);
        }

        return new ExprNode(UnwrapOrMakeTuple(exprList, endsWithComma));
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
            throw new AstException("invalid syntax");

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
                PyVirtualMachine.RaiseIndentationError($"expected an indented block after {statementName} on line {lineno}");
                throw new PyRuntimeException(PyVirtualMachine.CurrentException);
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
        EnsureKeywordThenMove(startsWithKeyword);
        var ifNode = new IfNode(ParseExpression());
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
        EnsureKeywordThenMove("while");
        var whileNode = new WhileNode(ParseExpression());
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
        EnsureKeywordThenMove("try");
        var tryNode = new TryNode();
        EnsureTokenTypeThenMove(TokenType.Colon);
        tryNode.Body.AddRange(ParseSuite("try"));
        if (!IsCurrentKeyword("except") && !IsCurrentKeyword("finally"))
            throw new AstException("should be 'except' or 'finally'");
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
        EnsureKeywordThenMove("for");
        var targetList = ParseTargetList(StopPredicates.UntilKeywordIn, out var endsWithComma);
        var target = UnwrapOrMakeTuple(targetList, endsWithComma);
        EnsureKeywordThenMove("in");
        var iter = ParseExpression();
        EnsureTokenTypeThenMove(TokenType.Colon);
        var forNode = new ForNode(target, iter);
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
        return funcDef;

        void StartParsingFuncDef()
        {
            CurrentScope.TrySetLocalIfNotExistsOrUnknown(name);
            Context.EnterScope(funcDef);
            CurrentScope.AddParameters(args.PosonlyArgs.Concat(args.Args).Concat(args.KwonlyArgs).Select(static arg => arg.Arg));
        }

        void EndParsingFuncDef()
        {
            var scope = Context.ExitScope();
            SyncVariablesToOwnerThenFillLocal(scope);
        }
    }

    private ClassDefNode ParseClassDef(IEnumerable<AstExprNode> decorators)
    {
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

        var classDefNode = new ClassDefNode(name);
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
            SyncVariablesToOwnerThenFillLocal(scope);
        }
    }
}
