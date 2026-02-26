using PySharp.Runtime;
using System.Diagnostics;
using System.Reflection.Emit;

namespace PySharp.Compilation.AstNodes;

internal sealed partial class Reducer
{
    public static AstExprNode Fold(AstExprNode node)
    {
        return FoldExpr(node, out _);
    }
}
