using PySharp.PyRuntime;

namespace PySharp.AstNodes;

internal static class AstOptiUtils
{
    public static bool? TrgGetConstantBoolValue(this AstExprNode reducedTest)
    {
        return reducedTest switch
        {
            ConstantNode testConstant => PySpecialMethods.TryGetBool(testConstant.Value, out var b) ? b.BoolValue : null,
            ListNode list when list.NoSideEffects() is true => list.Elts.Length > 0,
            TupleNode tuple when tuple.NoSideEffects() is true => tuple.Elts.Length > 0,
            DictNode dict when dict.NoSideEffects() is true => dict.Keys.Length > 0,
            SetNode set when set.NoSideEffects() is true => set.Elts.Length > 0,
            LambdaNode => true,
            _ => null
        };
    }
}
