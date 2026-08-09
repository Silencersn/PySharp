using PySharp.Compilation.Primitives;
using PySharp.Compilation.Tokenization;
using PySharp.Runtime;
using PySharp.Utility;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace PySharp.Compilation.AstNodes;

partial class Parser
{
    public static bool IsSingleTarget(AstExprNode node)
    {
        return node is NameNode or SubscriptNode or AttributeNode;
    }

    public static bool IsStarTarget(AstExprNode node, [NotNullWhen(false)] out AstExprNode? nonStarTargetNode)
    {
        nonStarTargetNode = null;

        if (IsSingleTarget(node))
            return true;

        if (node is StarredNode)
            return true;

        if (node is TupleNode tupleNode)
        {
            foreach (var elt in tupleNode.Elts)
            {
                if (!IsStarTarget(elt, out nonStarTargetNode))
                    return false;
            }

            return true;
        }

        if (node is ListNode listNode)
        {
            foreach (var elt in listNode.Elts)
            {
                if (!IsStarTarget(elt, out nonStarTargetNode))
                    return false;
            }

            return true;
        }

        nonStarTargetNode = node;
        return false;
    }

    [GrammarSyntaxRule("annotated_rhs")]
    private AstExprNode ParseAnnotatedRhs(StopPredicate predicate)
    {
        if (IsCurrentKeyword("yield"))
            return ParseYieldExpr();

        return ParseStarExpressions(predicate);
    }

    [GrammarSyntaxRule("assignment")]
    private bool TryParseAssignment([NotNullWhen(true)] out AstStmtNode? assignment, out AstExprNode? starExpressions)
    {
        var metaInfo = CreateAstMetaInfo();
        var pos = TokenPosition;
        starExpressions = null;

        if (TryParseSimpleAnnAssign(out assignment))
            return true;

        TokenPosition = pos;
        if (TryParseAnnAssignOrAugAssign(out assignment))
            return true;

        TokenPosition = pos;
        starExpressions = ParseStarExpressions(StopPredicates.UntilNewLineOrSemicolonOrEqual);

        if (CurrentTokenType is not TokenType.Equal)
        {
            if (CurrentTokenType is TokenType.Colon)
                throw SyntaxError(PySR.InvalidSyntax_Assignment_MultipleTargetsForAnnotation);

            if (IsAugOperator(CurrentTokenType))
                throw SyntaxError(PySR.InvalidSyntax_Assignment_InvalidAugAssignTarget, AstUtils.GetExprNodeName(starExpressions));

            return false;
        }

        var targets = _context.BuilderPool.Rent<AstExprNode>();

        while (CurrentTokenType is TokenType.Equal)
        {
            if (!IsStarTarget(starExpressions, out var nonStarTargetNode))
                throw SyntaxError(PySR.InvalidSyntax_InvalidTarget, AstUtils.GetExprNodeName(nonStarTargetNode));

            targets.Add(starExpressions);

            MoveNextToken();

            if (IsCurrentKeyword("yield"))
            {
                var value = ParseAnnotatedRhs(StopPredicates.UntilNewLineOrSemicolonOrEqual);
                assignment = Ast.Assign(_context.BuilderPool.ToImmutableThenReturn(targets), value).With(metaInfo);
                return true;
            }

            starExpressions = ParseStarExpressions(StopPredicates.UntilNewLineOrSemicolonOrEqual);
        }

        assignment = Ast.Assign(_context.BuilderPool.ToImmutableThenReturn(targets), starExpressions).With(metaInfo);
        return true;

        bool TryParseSimpleAnnAssign([NotNullWhen(true)] out AstStmtNode? annAssign)
        {
            annAssign = null;
            if (!IsCurrentIdentifier)
                return false;

            var name = CurrentTokenString;
            MoveNextToken();
            if (CurrentTokenType is not TokenType.Colon)
                return false;

            // if a colon appears here,
            // the statement should not be an expression.
            // so, we won't consider any exceptions that might be thrown later.

            var target = Ast.Name(name).With(metaInfo);
            annAssign = ParseAnnAssign(target, simple: true);
            return true;
        }

        bool TryParseAnnAssignOrAugAssign([NotNullWhen(true)] out AstStmtNode? annAssignOrAugAssign)
        {
            annAssignOrAugAssign = null;

            // for simple_stmt,
            // the ambiguity only exists with assignment and star_expressions.
            // so here, we directly parse star_expression
            // and then check if it's valid target,
            // without needing to catch syntax exceptions.

            var target = ParseStarExpression();
            OperatorType? op = CurrentTokenType switch
            {
                TokenType.PlusEqual => OperatorType.Add,
                TokenType.MinusEqual => OperatorType.Sub,
                TokenType.StarEqual => OperatorType.Mult,
                TokenType.AtEqual => OperatorType.MatMult,
                TokenType.SlashEqual => OperatorType.Div,
                TokenType.DoubleSlashEqual => OperatorType.FloorDiv,
                TokenType.PercentEqual => OperatorType.Mod,
                TokenType.DoubleStarEqual => OperatorType.Pow,
                TokenType.LeftShiftEqual => OperatorType.LShift,
                TokenType.RightShiftEqual => OperatorType.RShift,
                TokenType.AmpersandEqual => OperatorType.BitAnd,
                TokenType.CaretEqual => OperatorType.BitXor,
                TokenType.PipeEqual => OperatorType.BitOr,
                _ => null,
            };

            if (!IsSingleTarget(target))
            {
                if (op is not null)
                    throw SyntaxError(PySR.InvalidSyntax_Assignment_InvalidAugAssignTarget, AstUtils.GetExprNodeName(target));

                if (CurrentTokenType is TokenType.Colon)
                    throw SyntaxError(PySR.InvalidSyntax_Assignment_IllegalTargetForAnnotation);

                return false;
            }

            if (op is not null)
            {
                MoveNextToken();
                AstExprNode value = ParseAnnotatedRhs(StopPredicates.UntilNewLineOrSemicolon);
                annAssignOrAugAssign = Ast.AugAssign(target, op.Value, value).With(metaInfo);
                return true;
            }

            if (CurrentTokenType is TokenType.Colon)
            {
                annAssignOrAugAssign = ParseAnnAssign(target, simple: false);
                return true;
            }

            return false;
        }

        AnnAssignNode ParseAnnAssign(AstExprNode target, bool simple)
        {
            EnsureTokenTypeThenMove(TokenType.Colon);
            var annotation = ParseExpression();
            if (CurrentTokenType is not TokenType.Equal)
                return Ast.AnnAssign(target, annotation, value: null, simple).With(metaInfo);

            MoveNextToken();
            var value = ParseAnnotatedRhs(StopPredicates.UntilNewLineOrSemicolon);
            return Ast.AnnAssign(target, annotation, value, simple).With(metaInfo);
        }
    }

    [GrammarSyntaxRule("return_stmt")]
    private ReturnNode ParseReturnStmt()
    {
        var metaInfo = CreateAstMetaInfo();
        EnsureKeywordThenMove("return");

        if (CurrentTokenType is TokenType.NewLine or TokenType.Semicolon)
            return Ast.Return().With(metaInfo);

        var value = ParseStarExpressions(StopPredicates.UntilNewLineOrSemicolon);
        return Ast.Return(value).With(metaInfo);
    }

    [GrammarSyntaxRule("dotted_name")]
    private string ParseDottedName()
    {
        var result = ParseSomethingList(ParseNonMangledIdentifier, StopPredicates.UntilNonName, out _, separator: TokenType.Dot);
        if (!result.Builder.IsNull)
            return string.Join('.', result.MakeArray());
        return MangleIdentifier(result.First);
    }

    [GrammarSyntaxRule("dotted_as_name")]
    private AstAliasNode ParseDottedAsName()
    {
        var metaInfo = CreateAstMetaInfo();
        var name = ParseDottedName();
        if (!IsCurrentKeyword("as"))
            return Ast.Alias(name, asName: null).With(metaInfo.WithPreviousEnd());

        MoveNextToken();
        var asName = ParseIdentifier();
        return Ast.Alias(name, asName).With(metaInfo.WithPreviousEnd());
    }

    [GrammarSyntaxRule("dotted_as_names")]
    private ImmutableArray<AstAliasNode> ParseDottedAsNames()
    {
        var result = ParseSomethingList(ParseDottedAsName, StopPredicates.UntilNewLineOrSemicolon, out var endsWithComma);
        if (endsWithComma is not null)
            throw SyntaxError();
        return result.MakeArray();
    }

    [GrammarSyntaxRule("import_name")]
    private ImportNode ParseImportName()
    {
        var metaInfo = CreateAstMetaInfo();
        EnsureKeywordThenMove("import");
        var names = ParseDottedAsNames();
        return Ast.Import(names).With(metaInfo);
    }

    [GrammarSyntaxRule("import_from_as_name")]
    private AstAliasNode ParseImportFromAsName()
    {
        var metaInfo = CreateAstMetaInfo();
        var name = ParseIdentifier();
        if (!IsCurrentKeyword("as"))
            return Ast.Alias(name, asName: null).With(metaInfo.WithPreviousEnd());

        MoveNextToken();
        var asName = ParseIdentifier();
        return Ast.Alias(name, asName).With(metaInfo.WithPreviousEnd());
    }

    [GrammarSyntaxRule("import_from_as_names")]
    private ParseSomethingListResult<AstAliasNode> ParseImportFromAsNames(out Token? endsWithComma)
    {
        return ParseSomethingList(ParseImportFromAsName, StopPredicates.UntilNewLineOrSemicolonOrRightParen, out endsWithComma);
    }

    [GrammarSyntaxRule("import_from_targets")]
    private ImmutableArray<AstAliasNode> ParseImportFromTargets()
    {
        if (CurrentTokenType is TokenType.Star)
        {
            var target = Ast.Alias("*", asName: null).With(CreateAstMetaInfo());
            MoveNextToken();
            return [target];
        }

        if (CurrentTokenType is TokenType.LeftParen)
        {
            MoveNextToken();
            var result = ParseImportFromAsNames(out _);
            EnsureTokenTypeThenMove(TokenType.RightParen);
            return result.MakeArray();
        }
        else
        {
            var result = ParseImportFromAsNames(out var endsWithComma);
            if (endsWithComma is not null)
                throw SyntaxError();
            return result.MakeArray();
        }
    }

    [GrammarSyntaxRule("import_stmt")]
    private AstStmtNode ParseImportStmt()
    {
        var metaInfo = CreateAstMetaInfo();
        if (IsCurrentKeyword("import"))
            return ParseImportName();

        EnsureKeywordThenMove("from");

        var level = ParseLevel();
        var module = IsCurrentKeyword("import") ? null : ParseDottedName();
        if (module is null && level is 0)
            throw SyntaxError();

        EnsureKeywordThenMove("import");
        var names = ParseImportFromTargets();
        return Ast.ImportFrom(module, names, level).With(metaInfo);

        int ParseLevel()
        {
            var level = 0;
            while (true)
            {
                if (CurrentTokenType is TokenType.Dot)
                    level++;
                else if (CurrentTokenType is TokenType.Ellipsis)
                    // '...' is tokenized as Ellipsis
                    level += 3;
                else
                    break;

                MoveNextToken();
            }
            return level;
        }
    }

    [GrammarSyntaxRule("raise_stmt")]
    private RaiseNode ParseRaiseStmt()
    {
        var metaInfo = CreateAstMetaInfo();
        EnsureKeywordThenMove("raise");

        if (CurrentTokenType is TokenType.NewLine or TokenType.Semicolon)
            return Ast.Raise().With(metaInfo);

        var exc = ParseExpression();

        if (!IsCurrentKeyword("from"))
            return Ast.Raise(exc).With(metaInfo);

        MoveNextToken();
        var cause = ParseExpression();
        return Ast.Raise(exc, cause).With(metaInfo);
    }

    [GrammarSyntaxRule("pass_stmt")]
    private PassNode ParsePassStmt()
    {
        var metaInfo = CreateAstMetaInfo();
        EnsureKeywordThenMove("pass");
        return Ast.Pass().With(metaInfo);
    }

    [GrammarSyntaxRule("del_target")]
    private AstExprNode ParseDelTarget()
    {
        var target = ParseStarTarget();
        CheckNoStarred(target);
        return target;

        void CheckNoStarred(AstExprNode node)
        {
            if (node is StarredNode)
                throw SyntaxError(PySR.InvalidSyntax_DelStmt_CannotDeleteStarred);

            if (node is TupleNode tupleNode)
            {
                foreach (var elt in tupleNode.Elts)
                    CheckNoStarred(elt);
            }

            if (node is ListNode listNode)
            {
                foreach (var elt in listNode.Elts)
                    CheckNoStarred(elt);
            }
        }
    }

    [GrammarSyntaxRule("del_targets")]
    private ParseSomethingListResult<AstExprNode> ParseDelTargets()
    {
        return ParseSomethingList(ParseDelTarget, StopPredicates.UntilNewLineOrSemicolon, out _);
    }

    [GrammarSyntaxRule("del_stmt")]
    private DeleteNode ParseDelStmt()
    {
        var metaInfo = CreateAstMetaInfo();
        EnsureKeywordThenMove("del");
        var targets = ParseDelTargets().MakeArray();
        return Ast.Delete(targets).With(metaInfo);
    }

    [GrammarSyntaxRule("yield_stmt")]
    private ExprNode ParseYieldStmt()
    {
        var metaInfo = CreateAstMetaInfo();
        var expr = ParseYieldExpr();
        return Ast.Expr(expr).With(metaInfo);
    }

    [GrammarSyntaxRule("assert_stmt")]
    private AssertNode ParseAssertStmt()
    {
        var metaInfo = CreateAstMetaInfo();
        EnsureKeywordThenMove("assert");

        var test = ParseExpression();

        if (CurrentTokenType is not TokenType.Comma)
            return Ast.Assert(test).With(metaInfo);

        MoveNextToken();
        var msg = ParseExpression();
        return Ast.Assert(test, msg).With(metaInfo);
    }

    [GrammarSyntaxRule("break_stmt")]
    private BreakNode ParseBreakStmt()
    {
        var metaInfo = CreateAstMetaInfo();
        EnsureKeywordThenMove("break");
        return Ast.Break().With(metaInfo);
    }

    [GrammarSyntaxRule("continue_stmt")]
    private ContinueNode ParseContinueStmt()
    {
        var metaInfo = CreateAstMetaInfo();
        EnsureKeywordThenMove("continue");
        return Ast.Continue().With(metaInfo);
    }

    [GrammarSyntaxRule("global_stmt")]
    private GlobalNode ParseGlobalStmt()
    {
        var metaInfo = CreateAstMetaInfo();
        EnsureKeywordThenMove("global");
        var names = ParseIdentifiers().MakeArray();
        return Ast.Global(names).With(metaInfo);
    }

    [GrammarSyntaxRule("nonlocal_stmt")]
    private NonlocalNode ParseNonlocalStmt()
    {
        var metaInfo = CreateAstMetaInfo();
        EnsureKeywordThenMove("nonlocal");
        var names = ParseIdentifiers().MakeArray();
        return Ast.Nonlocal(names).With(metaInfo);
    }

    [GrammarSyntaxRule("simple_stmt")]
    private AstStmtNode ParseSimpleStmt()
    {
        var metaInfo = CreateAstMetaInfo();

        if (CurrentTokenType is TokenType.Name)
        {
            AstStmtNode? stmt = CurrentTokenStringAsSpan switch
            {
                "type" => TryParseTypeAliasStmt(),
                "return" => ParseReturnStmt(),
                "import" or "from" => ParseImportStmt(),
                "raise" => ParseRaiseStmt(),
                "pass" => ParsePassStmt(),
                "del" => ParseDelStmt(),
                "yield" => ParseYieldStmt(),
                "assert" => ParseAssertStmt(),
                "break" => ParseBreakStmt(),
                "continue" => ParseContinueStmt(),
                "global" => ParseGlobalStmt(),
                "nonlocal" => ParseNonlocalStmt(),
                _ => null,
            };
            if (stmt is not null)
                return stmt;
        }

        if (TryParseAssignment(out var assignment, out var starExpressions))
            return assignment;

        starExpressions ??= ParseStarExpressions(StopPredicates.UntilNewLineOrSemicolonOrEqual);
        return Ast.Expr(starExpressions).With(metaInfo);
    }

    private ParseSomethingListResult<string> ParseIdentifiers()
    {
        return ParseSomethingList(ParseIdentifier, StopPredicates.UntilNewLineOrSemicolon, out _);
    }

    [GrammarSyntaxRule("simple_stmts")]
    private ImmutableArrayBuilderOf<AstStmtNode> ParseSimpleStmts()
    {
        var result = ParseSomethingList(ParseSimpleStmt, StopPredicates.UntilNewLine, out _, separator: TokenType.Semicolon);
        EnsureTokenTypeThenMove(TokenType.NewLine);
        return result.GetBuilder();
    }

    [GrammarSyntaxRule("statement")]
    private AstStmtNode? ParseStatement(out ImmutableArrayBuilderOf<AstStmtNode> simples)
    {
        simples = ImmutableArrayBuilderOf<AstStmtNode>.Null;
        if (TryParseCompoundStmt(out var compoundStmt))
            return compoundStmt;

        simples = ParseSimpleStmts();
        return null;
    }

    [GrammarSyntaxRule("statements")]
    private ImmutableArray<AstStmtNode> ParseStatements()
    {
        var stmts = _context.BuilderPool.Rent<AstStmtNode>();
        while (CurrentTokenType is not (TokenType.Dedent or TokenType.EndMarker))
        {
            var compound = ParseStatement(out var simples);
            if (compound is not null)
            {
                stmts.Add(compound);
            }
            else
            {
                Debug.Assert(!simples.IsNull);
                stmts.AddRange(simples);
                _context.BuilderPool.Return(simples);
            }
        }
        return _context.BuilderPool.ToImmutableThenReturn(stmts);
    }

    [GrammarSyntaxRule("statement_newline")]
    private ImmutableArray<AstStmtNode> ParseStatementNewLine()
    {
        if (CurrentTokenType is TokenType.NewLine or TokenType.EndMarker)
        {
            MoveNextToken();
            return [];
        }

        if (TryParseCompoundStmt(out var compoundStmt))
        {
            EnsureTokenTypeThenMove(TokenType.NewLine);
            return [compoundStmt];
        }

        return _context.BuilderPool.ToImmutableThenReturn(ParseSimpleStmts());
    }

    [GrammarSyntaxRule("decorators")]
    private List<AstExprNode> ParseDecorators()
    {
        EnsureTokenType(TokenType.At);
        List<AstExprNode> decorators = [];
        do
        {
            MoveNextToken();
            var decorator = ParseNamedExpression();
            decorators.Add(decorator);
            EnsureTokenTypeThenMove(TokenType.NewLine);
        } while (CurrentTokenType is TokenType.At);
        return decorators;
    }


    [GrammarSyntaxRule("compound_stmt")]
    private bool TryParseCompoundStmt([NotNullWhen(true)] out AstStmtNode? compoundStmt)
    {
        List<AstExprNode>? decorators = null;
        if (CurrentTokenType is TokenType.At)
            decorators = ParseDecorators();

        if (CurrentTokenType is not TokenType.Name)
        {
            if (decorators?.Count > 0)
                throw SyntaxError();

            compoundStmt = null;
            return false;
        }

        if (decorators?.Count > 0 && !(IsCurrentKeyword("def") || IsCurrentKeyword("class") || IsMatchKeywordsSequence("async", "def")))
            throw SyntaxError();

        compoundStmt = CurrentTokenStringAsSpan switch
        {
            "def" => ParseFunctionDef(decorators),
            "if" => ParseIfStmt("if"),
            "class" => ParseClassDef(decorators),
            "with" => ParseWithStmt(),
            "for" => ParseForStmt(),
            "try" => ParseTryStmt(),
            "while" => ParseWhileStmt(),
            "match" when TestIsMatchStmt() => ParseMatchStmt(),
            "async" => ParseAsyncStmt(decorators),
            _ => null,
        };

        return compoundStmt is not null;

        bool TestIsMatchStmt()
        {
            var pos = TokenPosition;
            var isMatchStmt = TestIsMatchStmtFast();
            TokenPosition = pos;
            if (isMatchStmt is not null)
                return isMatchStmt.Value;

            try
            {
                MoveNextToken();
                _ = ParseSubjectExpr();

                if (CurrentTokenType is not TokenType.Colon)
                    return false;

                // only check colon is not enough.
                // this example stmt is invalid,
                // but it actually not match_stmt.
                // it should raise 'illegal target for annotation'.
                //
                // match + 1: int
                //
                MoveNextToken();
                return CurrentTokenType is TokenType.NewLine;
            }
            catch (PyRuntimeException)
            {
                return false;
            }
            finally
            {
                TokenPosition = pos;
            }

            bool? TestIsMatchStmtFast()
            {
                if (!IsCurrentKeyword("match"))
                    return false;

                MoveNextToken();

                if (CurrentTokenType is TokenType.NewLine or TokenType.Semicolon)
                    // end of simple_stmt
                    return false;

                if (CurrentTokenType is TokenType.Equal or TokenType.Colon or TokenType.ColonEqual)
                    // assignment
                    return false;

                if (IsAugOperator(CurrentTokenType))
                    // augassign
                    return false;

                if (CurrentTokenType is TokenType.Comma)
                    // a part of tuple
                    return false;

                if (CurrentTokenType is TokenType.Dot)
                    // attribute
                    return false;

                if (CurrentTokenType is TokenType.Tilde)
                    // unary op
                    return true;

                if (BinaryOperators.Contains(CurrentTokenType))
                {
                    if (CurrentTokenType is TokenType.Plus or TokenType.Minus or TokenType.Star)
                        // plus and minus may be unary op (match +1: ...)
                        // star may be unpacking (match *[],: ...)
                        return null;

                    return false;
                }
                else if (IsCurrentKeyword("is") || IsCurrentKeyword("in"))
                {
                    return false;
                }
                else if (IsCurrentKeyword("not"))
                {
                    MoveNextToken();

                    // 'not' is unary op
                    // 'not in' is binary op
                    return !IsCurrentKeyword("in");
                }

                return null;
            }
        }
    }

    [GrammarSyntaxRule("block")]
    private ImmutableArray<AstStmtNode> ParseBlock(string keyword)
    {
        if (CurrentTokenType is not TokenType.NewLine)
            return _context.BuilderPool.ToImmutableThenReturn(ParseSimpleStmts());

        var pos = TokenPosition;
        MoveNextToken();
        if (CurrentTokenType is not TokenType.Indent)
        {
            var statementName = keyword switch
            {
                "def" => "function definition",
                "class" => "class definition",
                _ => $"'{keyword}' statement"
            };
            var lineno = _tokenSequence[pos].GetStart(_codeSource).Line;
            throw _context.IndentationError(PySR.InvalidSyntax_Indentation_ExpectedForBlock, statementName, lineno);
        }
        MoveNextToken();

        var stmts = ParseStatements();
        EnsureTokenTypeThenMove(TokenType.Dedent);
        return [.. stmts];
    }

    [GrammarSyntaxRule("else_block")]
    private ImmutableArray<AstStmtNode> ParseElseBlock()
    {
        EnsureKeywordThenMove("else");
        EnsureTokenTypeThenMove(TokenType.Colon);
        return ParseBlock("else");
    }

    [GrammarSyntaxRule("if_stmt")]
    [GrammarSyntaxRule("elif_stmt")]
    private IfNode ParseIfStmt(string ifOrElif)
    {
        var metaInfo = CreateAstMetaInfo();
        EnsureKeywordThenMove(ifOrElif);
        var test = ParseNamedExpression();
        EnsureTokenTypeThenMove(TokenType.Colon);
        var body = ParseBlock(ifOrElif);
        IEnumerable<AstStmtNode> orElse = [];
        if (IsCurrentKeyword("elif"))
            orElse = [ParseIfStmt("elif")];
        else if (IsCurrentKeyword("else"))
            orElse = ParseElseBlock();
        return Ast.If(test, body, orElse).With(metaInfo);
    }

    [GrammarSyntaxRule("while_stmt")]
    private WhileNode ParseWhileStmt()
    {
        var metaInfo = CreateAstMetaInfo();
        EnsureKeywordThenMove("while");
        var test = ParseNamedExpression();
        EnsureTokenTypeThenMove(TokenType.Colon);
        var body = ParseBlock("while");
        IEnumerable<AstStmtNode> orElse = IsCurrentKeyword("else") ? ParseElseBlock() : [];
        return Ast.While(test, body, orElse).With(metaInfo);
    }

    [GrammarSyntaxRule("try_stmt")]
    private AstStmtNode ParseTryStmt()
    {
        var metaInfo = CreateAstMetaInfo();
        EnsureKeywordThenMove("try");
        EnsureTokenTypeThenMove(TokenType.Colon);
        var body = ParseBlock("try");
        if (!IsCurrentKeyword("except") && !IsCurrentKeyword("finally"))
            throw SyntaxError(PySR.InvalidSyntax_TryStmt_ExpectedExceptOrFinally);
        var exceptors = _context.BuilderPool.Rent<ExceptHandlerNode>();
        IEnumerable<AstStmtNode> orElse = [];
        IEnumerable<AstStmtNode> finalBody = [];
        bool? isStar = null;
        if (IsCurrentKeyword("except"))
        {
            while (IsCurrentKeyword("except"))
            {
                if (isStar is null)
                {
                    var pos = TokenPosition;
                    MoveNextToken();
                    isStar = CurrentTokenType is TokenType.Star;
                    TokenPosition = pos;
                }
                exceptors.Add(ParseExceptBlock(isStar.Value));
            }
            if (IsCurrentKeyword("else"))
                orElse = ParseElseBlock();
        }
        if (IsCurrentKeyword("finally"))
            finalBody = ParseFinallyBlock();

        var exceptorsArray = _context.BuilderPool.ToImmutableThenReturn(exceptors);
        return isStar ?? false
            ? Ast.TryStar(body, exceptorsArray, orElse, finalBody).With(metaInfo)
            : Ast.Try(body, exceptorsArray, orElse, finalBody).With(metaInfo);
    }

    [GrammarSyntaxRule("except_block")]
    [GrammarSyntaxRule("except_star_block")]
    private ExceptHandlerNode ParseExceptBlock(bool isStar)
    {
        var metaInfo = CreateAstMetaInfo();
        EnsureKeywordThenMove("except");

        if (isStar)
            EnsureTokenTypeThenMove(TokenType.Star, PySR.InvalidSyntax_TryStmt_BothExceptAndExceptStar);
        else if (CurrentTokenType is TokenType.Star)
            throw SyntaxError(PySR.InvalidSyntax_TryStmt_BothExceptAndExceptStar);

        AstExprNode? type = null;
        string? name = null;

        if (CurrentTokenType is not TokenType.Colon)
        {
            var exprs = ParseExpressions(StopPredicates.UntilColon, out var endsWithComma);

            if (IsCurrentKeyword("as"))
            {
                if (!exprs.Builder.IsNull)
                    throw SyntaxError(PySR.InvalidSyntax_TryStmt_MultipleExceptionTypesUsingAs);

                if (endsWithComma is not null)
                    throw SyntaxError();

                MoveNextToken();
                name = ParseIdentifier();
            }

            type = UnwrapOrMakeTuple(exprs, endsWithComma);
        }
        else if (isStar)
        {
            throw SyntaxError(PySR.InvalidSyntax_TryStmt_ExpectedExceptionTypes);
        }

        EnsureTokenTypeThenMove(TokenType.Colon);

        var body = ParseBlock("except");
        return Ast.ExceptHandler(type, name, body).With(metaInfo);
    }

    [GrammarSyntaxRule("finally_block")]
    private ImmutableArray<AstStmtNode> ParseFinallyBlock()
    {
        EnsureKeywordThenMove("finally");
        EnsureTokenTypeThenMove(TokenType.Colon);
        return ParseBlock("finally");
    }

    [GrammarSyntaxRule("for_stmt")]
    private ForNode ParseForStmt()
    {
        var metaInfo = CreateAstMetaInfo();
        EnsureKeywordThenMove("for");
        var target = ParseStarTargets(StopPredicates.UntilKeywordIn(this));
        AstUtils.SetContext(target, ExprContextType.Store);
        EnsureKeywordThenMove("in");
        var iter = ParseStarExpressions(StopPredicates.UntilColon);
        EnsureTokenTypeThenMove(TokenType.Colon);
        var body = ParseBlock("for");
        IEnumerable<AstStmtNode> orElse = IsCurrentKeyword("else") ? ParseElseBlock() : [];
        return Ast.For(target, iter, body, orElse).With(metaInfo);
    }

    [GrammarSyntaxRule("with_stmt")]
    private WithNode ParseWithStmt()
    {
        var metaInfo = CreateAstMetaInfo();
        EnsureKeywordThenMove("with");

        ImmutableArray<AstWithItemNode> items;
        if (CurrentTokenType is TokenType.LeftParen)
        {
            MoveNextToken();
            items = ParseSomethingList(ParseWithItem, StopPredicates.UntilRightParen, out _).MakeArray();
            EnsureTokenTypeThenMove(TokenType.RightParen);
        }
        else
        {
            items = ParseSomethingList(ParseWithItem, StopPredicates.UntilColon, out _).MakeArray();
        }
        EnsureTokenTypeThenMove(TokenType.Colon);

        var body = ParseBlock("with");

        return Ast.With(items, body).With(metaInfo);
    }

    [GrammarSyntaxRule("with_item")]
    private AstWithItemNode ParseWithItem()
    {
        var metaInfo = CreateAstMetaInfo();
        var contextExpr = ParseExpression();
        AstExprNode? optionalVars = null;
        if (IsCurrentKeyword("as"))
        {
            MoveNextToken();
            optionalVars = ParseStarTarget();
        }
        return Ast.WithItem(contextExpr, optionalVars).With(metaInfo.WithPreviousEnd());
    }

    [GrammarSyntaxRule("function_def")]
    private FunctionDefNode ParseFunctionDef(IReadOnlyList<AstExprNode>? decorators)
    {
        var metaInfo = CreateAstMetaInfo();
        EnsureKeywordThenMove("def");
        var name = ParseIdentifier();

        IEnumerable<AstTypeParamNode> typeParams = [];
        if (CurrentTokenType is TokenType.LeftSquareBracket)
            typeParams = ParseTypeParams();

        EnsureTokenTypeThenMove(TokenType.LeftParen);
        var args = CurrentTokenType is TokenType.RightParen ? Ast.Arguments() : ParseParams(isLambda: false);
        EnsureTokenTypeThenMove(TokenType.RightParen);

        var returns = null as AstExprNode;
        if (CurrentTokenType is TokenType.RightArrow)
        {
            MoveNextToken();
            returns = ParseExpression();
        }

        EnsureTokenTypeThenMove(TokenType.Colon);
        var body = ParseBlock("def");
        return Ast.FunctionDef(name, args, body, decorators ?? [], returns, typeParams).With(metaInfo);
    }

    [GrammarSyntaxRule("class_def")]
    private ClassDefNode ParseClassDef(IReadOnlyList<AstExprNode>? decorators)
    {
        var metaInfo = CreateAstMetaInfo();
        EnsureKeywordThenMove("class");
        var name = ParseIdentifier(out var className);

        IEnumerable<AstTypeParamNode> typeParams = [];
        if (CurrentTokenType is TokenType.LeftSquareBracket)
            typeParams = ParseTypeParams();

        IEnumerable<AstExprNode> bases = [];
        IEnumerable<AstKeywordNode> keywords = [];
        if (CurrentTokenType is TokenType.LeftParen)
        {
            MoveNextToken();
            (bases, keywords) = ParseArguments();
            EnsureTokenTypeThenMove(TokenType.RightParen);
        }

        EnsureTokenTypeThenMove(TokenType.Colon);
        ImmutableArray<AstStmtNode> body;
        _classNameTrimmedStack.Push(className.TrimStart('_'));
        body = ParseBlock("class");
        _classNameTrimmedStack.Pop();
        return Ast.ClassDef(name, bases, keywords, body, decorators ?? [], typeParams).With(metaInfo);
    }

    [GrammarSyntaxRule("type_params")]
    private ImmutableArray<AstTypeParamNode> ParseTypeParams()
    {
        EnsureTokenTypeThenMove(TokenType.LeftSquareBracket);
        var result = ParseTypeParamSeq(StopPredicates.UntilRightSquareBracket);
        EnsureTokenTypeThenMove(TokenType.RightSquareBracket);
        return result.MakeArray();
    }

    [GrammarSyntaxRule("type_param_seq")]
    private ParseSomethingListResult<AstTypeParamNode> ParseTypeParamSeq(StopPredicate predicate)
    {
        return ParseSomethingList(ParseTypeParam, predicate, out _);
    }

    [GrammarSyntaxRule("type_param")]
    private AstTypeParamNode ParseTypeParam()
    {
        var metaInfo = CreateAstMetaInfo();
        if (CurrentTokenType is TokenType.Name)
        {
            var name = ParseIdentifier();
            var bound = CurrentTokenType is TokenType.Colon ? ParseTypeParamBound() : null;
            var defaultValue = CurrentTokenType is TokenType.Equal ? ParseTypeParamDefault() : null;
            return Ast.TypeVar(name, bound, defaultValue).With(metaInfo.WithPreviousEnd());
        }
        else if (CurrentTokenType is TokenType.Star)
        {
            MoveNextToken();
            var name = ParseIdentifier();
            if (CurrentTokenType is TokenType.Colon)
                throw SyntaxError(PySR.InvalidSyntax_TypeParam_BoundForTypeVarTuple);
            var defaultValue = CurrentTokenType is TokenType.Equal ? ParseTypeParamStarredDefault() : null;
            return Ast.TypeVarTuple(name, defaultValue).With(metaInfo.WithPreviousEnd());
        }
        else if (CurrentTokenType is TokenType.DoubleStar)
        {
            MoveNextToken();
            var name = ParseIdentifier();
            if (CurrentTokenType is TokenType.Colon)
                throw SyntaxError(PySR.InvalidSyntax_TypeParam_BoundForParamSpec);
            var defaultValue = CurrentTokenType is TokenType.Equal ? ParseTypeParamDefault() : null;
            return Ast.ParamSpec(name, defaultValue).With(metaInfo.WithPreviousEnd());
        }

        throw SyntaxError();
    }

    [GrammarSyntaxRule("type_param_bound")]
    private AstExprNode ParseTypeParamBound()
    {
        EnsureTokenTypeThenMove(TokenType.Colon);
        return ParseExpression();
    }

    [GrammarSyntaxRule("type_param_default")]
    private AstExprNode ParseTypeParamDefault()
    {
        EnsureTokenTypeThenMove(TokenType.Equal);
        return ParseExpression();
    }

    [GrammarSyntaxRule("type_param_starred_default")]
    private AstExprNode ParseTypeParamStarredDefault()
    {
        EnsureTokenTypeThenMove(TokenType.Equal);
        return ParseExpression();
    }

    private AstStmtNode ParseAsyncStmt(IReadOnlyList<AstExprNode>? decorators)
    {
        var pos = TokenPosition;
        EnsureKeywordThenMove("async");

        if (CurrentTokenType is not TokenType.Name || !IsKeyword(CurrentTokenStringAsSpan))
            throw SyntaxError();

        var keyword = CurrentTokenStringAsSpan;
        TokenPosition = pos;

        return keyword switch
        {
            "def" => ParseAsyncFunctionDef(decorators),
            "for" => ParseAsyncForStmt(),
            "with" => ParseAsyncWithStmt(),

            _ => throw new NotSupportedException()
        };
    }

    private AsyncForNode ParseAsyncForStmt()
    {
        var metaInfo = CreateAstMetaInfo();
        EnsureKeywordThenMove("async");
        EnsureKeywordThenMove("for");
        var target = ParseStarTargets(StopPredicates.UntilKeywordIn(this));
        AstUtils.SetContext(target, ExprContextType.Store);
        EnsureKeywordThenMove("in");
        var iter = ParseStarExpressions(StopPredicates.UntilColon);
        EnsureTokenTypeThenMove(TokenType.Colon);
        var body = ParseBlock("for");
        IEnumerable<AstStmtNode> orElse = IsCurrentKeyword("else") ? ParseElseBlock() : [];
        return Ast.AsyncFor(target, iter, body, orElse).With(metaInfo);
    }

    private AsyncWithNode ParseAsyncWithStmt()
    {
        var metaInfo = CreateAstMetaInfo();
        EnsureKeywordThenMove("async");
        EnsureKeywordThenMove("with");

        ImmutableArray<AstWithItemNode> items;
        if (CurrentTokenType is TokenType.LeftParen)
        {
            MoveNextToken();
            items = ParseSomethingList(ParseWithItem, StopPredicates.UntilRightParen, out _).MakeArray();
            EnsureTokenTypeThenMove(TokenType.RightParen);
        }
        else
        {
            items = ParseSomethingList(ParseWithItem, StopPredicates.UntilColon, out _).MakeArray();
        }
        EnsureTokenTypeThenMove(TokenType.Colon);

        var body = ParseBlock("with");

        return Ast.AsyncWith(items, body).With(metaInfo);
    }

    private AsyncFunctionDefNode ParseAsyncFunctionDef(IReadOnlyList<AstExprNode>? decorators)
    {
        var metaInfo = CreateAstMetaInfo();

        EnsureKeywordThenMove("async");
        EnsureKeywordThenMove("def");
        var name = ParseIdentifier();

        IEnumerable<AstTypeParamNode> typeParams = [];
        if (CurrentTokenType is TokenType.LeftSquareBracket)
            typeParams = ParseTypeParams();

        EnsureTokenTypeThenMove(TokenType.LeftParen);
        var args = CurrentTokenType is TokenType.RightParen ? Ast.Arguments() : ParseParams(isLambda: false);
        EnsureTokenTypeThenMove(TokenType.RightParen);

        var returns = null as AstExprNode;
        if (CurrentTokenType is TokenType.RightArrow)
        {
            MoveNextToken();
            returns = ParseExpression();
        }

        EnsureTokenTypeThenMove(TokenType.Colon);
        var body = ParseBlock("def");
        return Ast.AsyncFunctionDef(name, args, body, decorators ?? [], returns, typeParams).With(metaInfo);
    }

    [GrammarSyntaxRule("type_alias_stmt")]
    private TypeAliasNode? TryParseTypeAliasStmt()
    {
        var pos = TokenPosition;
        var metaInfo = CreateAstMetaInfo();

        if (IsCurrentKeyword("type"))
        {
            MoveNextToken();
            if (IsCurrentIdentifier)
            {
                var name = ParseIdentifier();
                IEnumerable<AstTypeParamNode> typeParams = [];
                if (CurrentTokenType is TokenType.LeftSquareBracket)
                    typeParams = ParseTypeParams();
                EnsureTokenTypeThenMove(TokenType.Equal);
                var value = ParseExpression();
                return Ast.TypeAlias(name, typeParams, value).With(metaInfo);
            }
        }

        TokenPosition = pos;
        return null;
    }
}
