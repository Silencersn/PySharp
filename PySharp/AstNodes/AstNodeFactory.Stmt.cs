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
}
