using PySharp.Runtime;
using System.Diagnostics;
using System.Reflection.Emit;

namespace PySharp.Compilation.AstNodes;

internal sealed partial class Reducer
{
    public static AstModNode Reduce(AstModNode node)
    {
        return ReduceMod(node);
    }

    private static AstModNode ReduceMod(AstModNode node)
    {
        switch (node)
        {
            case ModuleNode n:
                {
                    var body = ReduceStmts(n.Body, out var changed);
                    return changed ? Ast.Module(body).With(node.MetaInfo) : node;
                }

            case ExpressionNode n:
                {
                    var body = ReduceExpr(n.Body, out var changed);
                    return changed ? Ast.Expression(body).With(node.MetaInfo) : node;
                }

            case InteractiveNode n:
                {
                    var body = ReduceStmts(n.Body, out var changed);
                    return changed ? Ast.Interactive(body).With(node.MetaInfo) : node;
                }

            default:
                throw new NotImplementedException();
        }
    }
}
