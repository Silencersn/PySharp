using PySharp.Runtime;
using PySharp.Runtime.Calls;

namespace PySharp.Compilation.AstNodes;

internal sealed partial class Reducer
{
    public static AstExprNode Fold(AstExprNode node)
    {
        return FoldExpr(node, out _);
    }

    public static bool? ToBool(AstExprNode node)
    {
        ConstantNode? constant = node as ConstantNode;
        constant ??= Fold(node) as ConstantNode;

        if (constant is null)
            return null;

        var value = constant.Value;
        var boolResult = PySpecialMethods.Bool(PyCallContext.NonContextDependency, value);
        if (boolResult.IsError)
            return null;

        return boolResult.Value.BoolValue;
    }
}
