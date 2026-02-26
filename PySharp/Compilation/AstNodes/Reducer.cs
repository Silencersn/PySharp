using System.Diagnostics;

namespace PySharp.Compilation.AstNodes;

internal sealed partial class Reducer
{
    public static AstModNode Reduce(AstModNode node)
    {
        return ReduceMod(node);
    }

    private static AstModNode ReduceMod(AstModNode node)
    {
        AstModNode reduced = node switch
        {
            ModuleNode n => Ast.Module(ReduceStmts(n.Body)),
            ExpressionNode n => Ast.Expression(ReduceExpr(n.Body)),
            InteractiveNode n => Ast.Interactive(ReduceStmts(n.Body)),
            _ => throw new UnreachableException()
        };
        return reduced.With(node.MetaInfo);
    }
}
