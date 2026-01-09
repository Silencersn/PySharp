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
        var metaInfo = CreateAstMetaInfo();
        if (IsCurrentKeyword("import"))
        {
            MoveNextToken();
            List<AstAliasNode> names = [ParseAlias()];

            while (CurrentTokenType is TokenType.Comma)
            {
                MoveNextToken();
                names.Add(ParseAlias());
            }

            if (IsCurrentKeyword("from"))
                throw _context.ThrowableSyntaxError("Did you mean to use 'from ... import ...' instead?");

            return Ast.Import(names).With(metaInfo);

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
                return Ast.ImportFrom(module, [new("*", null)], level).With(metaInfo);
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
                return Ast.ImportFrom(module, names, level).With(metaInfo);
            }
            else
            {
                List<AstAliasNode> names = [ParseAlias()];

                while (CurrentTokenType is TokenType.Comma)
                {
                    MoveNextToken();
                    names.Add(ParseAlias());
                }

                return Ast.ImportFrom(module, names, level).With(metaInfo);
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
        var metaInfo = CreateAstMetaInfo();
        if (CurrentTokenType is TokenType.Name && IsKeyword(CurrentToken.String))
        {
            var keyword = CurrentToken.String;
            if (keyword is "break")
            {
                MoveNextToken();
                return Ast.Break().With(metaInfo);
            }
            else if (keyword is "continue")
            {
                MoveNextToken();
                return Ast.Continue().With(metaInfo);
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

                        return Ast.Raise(exc, cause).With(metaInfo);
                    }

                    return Ast.Raise(exc).With(metaInfo);
                }

                return Ast.Raise().With(metaInfo);
            }
            else if (keyword is "return")
            {
                MoveNextToken();
                if (CurrentTokenType is TokenType.NewLine or TokenType.Semicolon)
                    return Ast.Return().With(metaInfo);

                var list = ParseExpressionList(StopPredicates.UntilNewLineOrSemicolon, out var comma);
                return Ast.Return(UnwrapOrMakeTuple(list, comma)).With(metaInfo);
            }
            else if (keyword is "pass")
            {
                MoveNextToken();
                return Ast.Pass().With(metaInfo);
            }
            else if (keyword is "del")
            {
                MoveNextToken();
                var targets = ParseTargetList(StopPredicates.UntilNewLineOrSemicolon, out _);
                return Ast.Delete(targets).With(metaInfo);
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
                    node = Ast.Assert(test, msg);
                }
                else
                {
                    node = Ast.Assert(test);
                }
                node.MetaInfo = metaInfo;
                return node;
            }
            else if (keyword is "global")
            {
                MoveNextToken();
                var names = ParseIdentifiers();
                var node = Ast.Global(names);
                node.MetaInfo = metaInfo;
                return node;
            }
            else if (keyword is "nonlocal")
            {
                MoveNextToken();
                var names = ParseIdentifiers();
                var node = Ast.Nonlocal(names);
                node.MetaInfo = metaInfo;
                return node;
            }
            else if (keyword is "yield")
            {
                var yieldExpr = ParseYieldExpression();
                return Ast.Expr(yieldExpr).With(metaInfo);
            }
        }


        var startIndex = TokenStreamPosition;
        var exprList = ParseExpressionList(StopPredicates.UntilNewLineOrSemicolonOrEqual, out var endsWithComma);

        // assignment_stmt
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

            var node = Ast.Assign(targets, UnwrapOrMakeTuple(exprList, endsWithComma));
            node.MetaInfo = metaInfo;
            return node;
        }

        // augmented_assignment_stmt
        if (IsAugOperator(CurrentTokenType))
        {
            var target = UnwrapOrMakeTuple(exprList, endsWithComma);

            if (!AstUtils.IsValidAugTarget(target))
                throw _context.ThrowableSyntaxError($"'{AstUtils.GetExprNodeName(target)}' is an illegal expression for augmented assignment");

            OperatorType op = CurrentTokenType switch
            {
                TokenType.PlusEqual => OperatorType.Add,
                TokenType.MinusEqual => OperatorType.Sub,
                TokenType.StarEqual => OperatorType.Mult,
                TokenType.AtEqual => throw new NotImplementedException(),
                TokenType.SlashEqual => OperatorType.Div,
                TokenType.DoubleSlashEqual => OperatorType.FloorDiv,
                TokenType.PercentEqual => OperatorType.Mod,
                TokenType.DoubleStarEqual => OperatorType.Pow,
                TokenType.LeftShiftEqual => OperatorType.LShift,
                TokenType.RightShiftEqual => OperatorType.RShift,
                TokenType.AmpersandEqual => OperatorType.BitAnd,
                TokenType.CaretEqual => OperatorType.BitXor,
                TokenType.PipeEqual => OperatorType.BitOr,

                _ => throw new UnreachableException(),
            };
            MoveNextToken();

            AstExprNode value;
            if (IsCurrentKeyword("yield"))
            {
                value = ParseYieldExpression();
            }
            else
            {
                var list = ParseExpressionList(StopPredicates.UntilNewLineOrSemicolon, out var comma);
                value = UnwrapOrMakeTuple(list, comma);
            }
            return Ast.AugAssign(target, op, value).With(metaInfo);
        }

        // annotated_assignment_stmt
        if (CurrentTokenType is TokenType.Colon)
        {
            var target = UnwrapOrMakeTuple(exprList, endsWithComma);

            if (!AstUtils.IsValidAugTarget(target))
                throw _context.ThrowableSyntaxError(null /* TODO */);

            MoveNextToken();
            var annotation = ParseExpression();
            var value = null as AstExprNode;

            // simple:
            // a: int
            // non-simple:
            // (a): int
            var simple = target is NameNode && _tokenStream.GetTokenAt(startIndex).Type is TokenType.Name;

            if (CurrentTokenType is TokenType.Equal)
            {
                MoveNextToken();
                if (IsCurrentKeyword("yield"))
                {
                    value = ParseYieldExpression();
                }
                else
                {
                    var list = ParseStarredExpressionList(StopPredicates.UntilNewLineOrSemicolon, out var comma);
                    value = UnwrapOrMakeTuple(list, comma);
                }
            }

            return Ast.AnnAssign(target, annotation, value, simple);
        }

        return Ast.Expr(UnwrapOrMakeTuple(exprList, endsWithComma)).With(metaInfo);
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
        var metaInfo = CreateAstMetaInfo();
        EnsureKeywordThenMove(startsWithKeyword);
        var test = ParseAssignmentExpression();
        EnsureTokenTypeThenMoveForTest(TokenType.Colon, test);
        var body = ParseSuite(startsWithKeyword);
        IEnumerable<AstStmtNode> orElse = [];
        if (IsCurrentKeyword("elif"))
        {
            orElse = [ParseIfStmt("elif")];
        }
        else if (IsCurrentKeyword("else"))
        {
            MoveNextToken();
            EnsureTokenTypeThenMove(TokenType.Colon);
            orElse = ParseSuite("else");
        }
        return Ast.If(test, body, orElse).With(metaInfo);
    }

    private WhileNode ParseWhileStmt()
    {
        var metaInfo = CreateAstMetaInfo();
        EnsureKeywordThenMove("while");
        var test = ParseAssignmentExpression();
        EnsureTokenTypeThenMoveForTest(TokenType.Colon, test);
        var body = ParseSuite("while");
        IEnumerable<AstStmtNode> orElse = [];
        if (IsCurrentKeyword("else"))
        {
            MoveNextToken();
            EnsureTokenTypeThenMove(TokenType.Colon);
            orElse = ParseSuite("else");
        }
        return Ast.While(test, body, orElse).With(metaInfo);
    }

    private TryNode ParseTryStmt()
    {
        var metaInfo = CreateAstMetaInfo();
        EnsureKeywordThenMove("try");
        EnsureTokenTypeThenMove(TokenType.Colon);
        var body = ParseSuite("try");
        if (!IsCurrentKeyword("except") && !IsCurrentKeyword("finally"))
            throw _context.ThrowableSyntaxError("expected 'except' or 'finally' block");
        List<ExceptHandlerNode> exceptors = [];
        IEnumerable<AstStmtNode> orElse = [];
        IEnumerable<AstStmtNode> finalBody = [];
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

                var exceptHandlerBody = ParseSuite("except");
                var exceptHandler = Ast.ExceptHandler(expr, id, exceptHandlerBody);
                exceptors.Add(exceptHandler);
            }
            if (IsCurrentKeyword("else"))
            {
                MoveNextToken();
                EnsureTokenTypeThenMove(TokenType.Colon);
                orElse = ParseSuite("else");
            }
        }
        if (IsCurrentKeyword("finally"))
        {
            MoveNextToken();
            EnsureTokenTypeThenMove(TokenType.Colon);
            finalBody = ParseSuite("finally");
        }
        return Ast.Try(body, exceptors, orElse, finalBody).With(metaInfo);
    }

    private ForNode ParseForStmt()
    {
        var metaInfo = CreateAstMetaInfo();
        EnsureKeywordThenMove("for");
        var targetList = ParseTargetList(StopPredicates.UntilKeywordIn, out var endsWithComma);
        var target = UnwrapOrMakeTuple(targetList, endsWithComma);
        AstUtils.SetContext(target, ExprContextType.Store);
        EnsureKeywordThenMove("in");
        var items = ParseStarredExpressionList(StopPredicates.UntilColon, out endsWithComma);
        var iter = UnwrapOrMakeTuple(items, endsWithComma);
        EnsureTokenTypeThenMove(TokenType.Colon);
        var body = ParseSuite("for");
        IEnumerable<AstStmtNode> orElse = [];
        if (IsCurrentKeyword("else"))
        {
            MoveNextToken();
            EnsureTokenTypeThenMove(TokenType.Colon);
            orElse = ParseSuite("else");
        }
        return Ast.For(target, iter, body, orElse).With(metaInfo);
    }

    private FunctionDefNode ParseFuncDef(IEnumerable<AstExprNode> decorators)
    {
        var metaInfo = CreateAstMetaInfo();
        EnsureKeywordThenMove("def");
        var name = ParseIdentifier();
        EnsureTokenTypeThenMove(TokenType.LeftParen);
        var args = CurrentTokenType is TokenType.RightParen ? new() : ParseParameterList(StopPredicates.UntilRightParen, allowAnnotation: true);
        EnsureTokenTypeThenMove(TokenType.RightParen);

        var returns = null as AstExprNode;
        if (CurrentTokenType is TokenType.RightArrow)
        {
            MoveNextToken();
            returns = ParseExpression();
        }

        EnsureTokenTypeThenMove(TokenType.Colon);
        var body = ParseSuite("def");
        return Ast.FunctionDef(name, args, body, decorators, returns).With(metaInfo);
    }

    private ClassDefNode ParseClassDef(IEnumerable<AstExprNode> decorators)
    {
        var metaInfo = CreateAstMetaInfo();
        EnsureKeywordThenMove("class");
        var name = ParseIdentifier();
        var bases = new List<AstExprNode>();
        var keywords = new List<AstKeywordNode>();

        if (CurrentTokenType is TokenType.LeftParen)
        {
            MoveNextToken();
            (bases, keywords) = ParseArgumentList();
            EnsureTokenTypeThenMove(TokenType.RightParen);
        }

        EnsureTokenTypeThenMove(TokenType.Colon);

        var body = ParseSuite("class");
        return Ast.ClassDef(name, bases, keywords, body, decorators).With(metaInfo);
    }
}
