namespace PySharp.AstNodes;

partial class AstNodeFactory
{
    public static AssertNode Assert(AstExprNode test, AstExprNode? msg = null)
    {
        ArgumentNullException.ThrowIfNull(test);

        return new AssertNode(test, msg);
    }

    public static AssignNode Assign(AstExprNode value, params IEnumerable<AstExprNode> targets)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(targets);

        foreach (var target in targets)
        {
            if (target is not IExprContextNode node)
                throw new InvalidOperationException();

            node.Ctx = ExprContext.Store;
        }

        return new AssignNode([.. targets], value);
    }

    public static GlobalNode Global(params IEnumerable<string> names)
    {
        ArgumentNullException.ThrowIfNull(names);

        return new GlobalNode([.. names]);
    }

    public static NonlocalNode Nonlocal(params IEnumerable<string> names)
    {
        ArgumentNullException.ThrowIfNull(names);

        return new NonlocalNode([.. names]);
    }
}
