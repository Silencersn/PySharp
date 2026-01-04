using System.Collections.Immutable;

namespace PySharp.AstNodes;

partial class AstNodeFactory
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
        targetsArray.CheckValidTargetThenSetContext(ExprContext.Store);

        return new AssignNode(targetsArray, value);
    }

    public static DeleteNode Delete(params IEnumerable<AstExprNode> targets)
    {
        ArgumentNullException.ThrowIfNull(targets);

        var targetsArray = targets.ToImmutableArray(true);
        targetsArray.CheckValidTargetThenSetContext(ExprContext.Del);

        return new DeleteNode(targetsArray);
    }

    public static AugAssignNode AugAssign(AstExprNode target, AstOperatorNode op, AstExprNode value)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(op);
        ArgumentNullException.ThrowIfNull(value);

        target.CheckValidTargetThenSetContext(ExprContext.Store, true);

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
}
