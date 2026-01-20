namespace PySharp.AstNodes;

partial class Ast
{
    public static AssertNode Assert(AstExprNode test, AstExprNode? msg = null)
    {
        ArgumentNullException.ThrowIfNull(test);

        return new AssertNode(test, msg);
    }

    public static AssignNode Assign(IEnumerable<AstExprNode> targets, AstExprNode value)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(targets);

        var targetsArray = targets.ToImmutableArray(true);
        targetsArray.CheckValidTargetThenSetContext(ExprContextType.Store);

        return new AssignNode(targetsArray, value);
    }

    public static AnnAssignNode AnnAssign(AstExprNode target, AstExprNode annotation, AstExprNode? value, bool simple)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(annotation);

        target.CheckValidTargetThenSetContext(ExprContextType.Store);

        return new AnnAssignNode(target, annotation, value, simple);
    }

    public static DeleteNode Delete(params IEnumerable<AstExprNode> targets)
    {
        ArgumentNullException.ThrowIfNull(targets);

        var targetsArray = targets.ToImmutableArray(true);
        targetsArray.CheckValidTargetThenSetContext(ExprContextType.Del);

        return new DeleteNode(targetsArray);
    }

    public static AugAssignNode AugAssign(AstExprNode target, OperatorType op, AstExprNode value)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(value);

        target.CheckValidTargetThenSetContext(ExprContextType.Store, true);

        return new AugAssignNode(target, op, value);
    }

    public static ExprNode Expr(AstExprNode value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return new ExprNode(value);
    }

    public static BreakNode Break()
    {
        return new BreakNode();
    }

    public static ContinueNode Continue()
    {
        return new ContinueNode();
    }

    public static ReturnNode Return(AstExprNode? value = null)
    {
        return new ReturnNode(value);
    }

    public static PassNode Pass()
    {
        return new PassNode();
    }

    public static RaiseNode Raise(AstExprNode? exc = null, AstExprNode? cause = null)
    {
        if (cause is not null && exc is null)
            throw new InvalidOperationException();

        return new RaiseNode(exc, cause);
    }

    public static GlobalNode Global(params IEnumerable<string> names)
    {
        ArgumentNullException.ThrowIfNull(names);

        return new GlobalNode(names.ToImmutableArray(true));
    }

    public static NonlocalNode Nonlocal(params IEnumerable<string> names)
    {
        ArgumentNullException.ThrowIfNull(names);

        return new NonlocalNode(names.ToImmutableArray(true));
    }

    public static IfNode If(AstExprNode test, IEnumerable<AstStmtNode> body, IEnumerable<AstStmtNode> orElse)
    {
        ArgumentNullException.ThrowIfNull(test);
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(orElse);

        return new IfNode(test, body.ToImmutableArray(true), orElse.ToImmutableArray(true));
    }

    public static ForNode For(AstExprNode target, AstExprNode iter, IEnumerable<AstStmtNode> body, IEnumerable<AstStmtNode> orElse)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(iter);
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(orElse);

        return new ForNode(target, iter, body.ToImmutableArray(true), orElse.ToImmutableArray(true));
    }

    public static WhileNode While(AstExprNode test, IEnumerable<AstStmtNode> body, IEnumerable<AstStmtNode> orElse)
    {
        ArgumentNullException.ThrowIfNull(test);
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(orElse);

        return new WhileNode(test, body.ToImmutableArray(true), orElse.ToImmutableArray(true));
    }

    public static TryNode Try(IEnumerable<AstStmtNode> body, IEnumerable<ExceptHandlerNode> exceptors, IEnumerable<AstStmtNode> orElse, IEnumerable<AstStmtNode> finalBody)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(exceptors);
        ArgumentNullException.ThrowIfNull(orElse);
        ArgumentNullException.ThrowIfNull(finalBody);

        return new TryNode(body.ToImmutableArray(true), exceptors.ToImmutableArray(true), orElse.ToImmutableArray(true), finalBody.ToImmutableArray(true));
    }

    public static TryStarNode TryStar(IEnumerable<AstStmtNode> body, IEnumerable<ExceptHandlerNode> exceptors, IEnumerable<AstStmtNode> orElse, IEnumerable<AstStmtNode> finalBody)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(exceptors);
        ArgumentNullException.ThrowIfNull(orElse);
        ArgumentNullException.ThrowIfNull(finalBody);

        var handlers = exceptors.ToImmutableArray(true);
        foreach (var handler in handlers)
            ArgumentNullException.ThrowIfNull(handler.Type);

        return new TryStarNode(body.ToImmutableArray(true), handlers, orElse.ToImmutableArray(true), finalBody.ToImmutableArray(true));
    }

    public static ImportNode Import(IEnumerable<AstAliasNode> names)
    {
        ArgumentNullException.ThrowIfNull(names);

        return new ImportNode(names.ToImmutableArray(true));
    }

    public static ImportFromNode ImportFrom(string? module, IEnumerable<AstAliasNode> names, int level)
    {
        ArgumentNullException.ThrowIfNull(names);
        ArgumentOutOfRangeException.ThrowIfNegative(level);

        return new ImportFromNode(module, names.ToImmutableArray(true), level);
    }

    public static FunctionDefNode FunctionDef(string name, AstArgumentsNode args, IEnumerable<AstStmtNode> body, IEnumerable<AstExprNode> decoratorList, AstExprNode? returns, IEnumerable<AstTypeParamNode> typeParams)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(decoratorList);
        ArgumentNullException.ThrowIfNull(typeParams);

        return new FunctionDefNode(name, args, body.ToImmutableArray(true), decoratorList.ToImmutableArray(true), returns, typeParams.ToImmutableArray(true));
    }

    public static ClassDefNode ClassDef(string name, IEnumerable<AstExprNode> bases, IEnumerable<AstKeywordNode> keywords, IEnumerable<AstStmtNode> body, IEnumerable<AstExprNode> decoratorList, IEnumerable<AstTypeParamNode> typeParams)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(bases);
        ArgumentNullException.ThrowIfNull(keywords);
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(decoratorList);
        ArgumentNullException.ThrowIfNull(typeParams);

        return new ClassDefNode(name, bases.ToImmutableArray(true), keywords.ToImmutableArray(true), body.ToImmutableArray(true), decoratorList.ToImmutableArray(true), typeParams.ToImmutableArray(true));
    }

    public static WithNode With(IEnumerable<AstWithItemNode> items, IEnumerable<AstStmtNode> body)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(body);

        return new WithNode(items.ToImmutableArray(true), body.ToImmutableArray(true));
    }

    public static MatchNode Match(AstExprNode subject, IEnumerable<AstMatchCaseNode> cases)
    {
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(cases);

        return new MatchNode(subject, cases.ToImmutableArray(true));
    }
}