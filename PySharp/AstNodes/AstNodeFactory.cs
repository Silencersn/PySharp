using System.Collections.Immutable;

namespace PySharp.AstNodes;

public static partial class AstNodeFactory
{
    public static AndNode And => AndNode.Shared;
    public static OrNode Or => OrNode.Shared;
    public static EqNode Eq => EqNode.Shared;
    public static NotEqNode NotEq => NotEqNode.Shared;
    public static LtNode Lt => LtNode.Shared;
    public static LtENode LtE => LtENode.Shared;
    public static GtNode Gt => GtNode.Shared;
    public static GtENode GtE => GtENode.Shared;
    public static IsNode Is => IsNode.Shared;
    public static IsNotNode IsNot => IsNotNode.Shared;
    public static InNode In => InNode.Shared;
    public static NotInNode NotIn => NotInNode.Shared;
    public static AddNode Add => AddNode.Shared;
    public static SubNode Sub => SubNode.Shared;
    public static MulNode Mul => MulNode.Shared;
    public static DivNode Div => DivNode.Shared;
    public static FloorDivNode FloorDiv => FloorDivNode.Shared;
    public static ModNode Mod => ModNode.Shared;
    public static PowNode Pow => PowNode.Shared;
    public static LShiftNode LShift => LShiftNode.Shared;
    public static RShiftNode RShift => RShiftNode.Shared;
    public static BitOrNode BitOr => BitOrNode.Shared;
    public static BitXorNode BitXor => BitXorNode.Shared;
    public static BitAndNode BitAnd => BitAndNode.Shared;
    public static NotNode Not => NotNode.Shared;
    public static InvertNode Invert => InvertNode.Shared;
    public static UAddNode UAdd => UAddNode.Shared;
    public static USubNode USub => USubNode.Shared;

    public static AstComprehensionNode Comprehension(AstExprNode target, AstExprNode iter, IEnumerable<AstExprNode> ifs)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(iter);
        ArgumentNullException.ThrowIfNull(ifs);

        target.CheckValidTargetThenSetContext(ExprContext.Store);

        return new AstComprehensionNode(target, iter, ifs.ToImmutableArray(true));
    }

    public static AstKeywordNode Keyword(string arg, AstExprNode value)
    {
        ArgumentNullException.ThrowIfNull(arg);
        ArgumentNullException.ThrowIfNull(value);

        return new AstKeywordNode(arg, value);
    }
}
