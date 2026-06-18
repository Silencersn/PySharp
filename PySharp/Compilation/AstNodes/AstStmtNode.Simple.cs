using PySharp.Compilation.Primitives;
using System.Collections.Immutable;

namespace PySharp.Compilation.AstNodes;

public abstract class AstStmtNode : AstNode;

public sealed class ExprNode : AstStmtNode
{
    public AstExprNode Value { get; }

    internal ExprNode(AstExprNode value)
    {
        Value = value;
    }
}

public sealed class AssignNode : AstStmtNode
{
    internal AssignNode(ImmutableArray<AstExprNode> targets, AstExprNode value)
    {
        Targets = targets;
        Value = value;
    }

    public ImmutableArray<AstExprNode> Targets { get; }
    public AstExprNode Value { get; }
}

public sealed class AugAssignNode : AstStmtNode
{
    internal AugAssignNode(AstExprNode target, OperatorType op, AstExprNode value)
    {
        Target = target;
        Op = op;
        Value = value;
    }

    public AstExprNode Target { get; }
    public OperatorType Op { get; }
    public AstExprNode Value { get; }
}

public sealed class AnnAssignNode : AstStmtNode
{
    internal AnnAssignNode(AstExprNode target, AstExprNode annotation, AstExprNode? value, bool simple)
    {
        Target = target;
        Annotation = annotation;
        Value = value;
        Simple = simple;
    }

    public AstExprNode Target { get; }
    public AstExprNode Annotation { get; }
    public AstExprNode? Value { get; }
    public bool Simple { get; }
}

public sealed class AssertNode : AstStmtNode
{
    internal AssertNode(AstExprNode test, AstExprNode? msg)
    {
        Test = test;
        Msg = msg;
    }

    public AstExprNode Test { get; }
    public AstExprNode? Msg { get; }

}

public sealed class PassNode : AstStmtNode
{
    internal PassNode()
    {
    }
}

public sealed class DeleteNode : AstStmtNode
{
    public ImmutableArray<AstExprNode> Targets { get; }

    internal DeleteNode(ImmutableArray<AstExprNode> targets)
    {
        Targets = targets;
    }
}

public sealed class ReturnNode : AstStmtNode
{
    public AstExprNode? Value { get; }

    internal ReturnNode(AstExprNode? value)
    {
        Value = value;
    }

}

public sealed class RaiseNode : AstStmtNode
{
    internal RaiseNode(AstExprNode? exc, AstExprNode? cause)
    {
        Exc = exc;
        Cause = cause;
    }

    public AstExprNode? Exc { get; }
    public AstExprNode? Cause { get; }

}

public sealed class BreakNode : AstStmtNode
{
    internal BreakNode()
    {
    }
}

public sealed class ContinueNode : AstStmtNode
{
    internal ContinueNode()
    {
    }
}

public sealed class ImportNode : AstStmtNode
{
    public ImmutableArray<AstAliasNode> Names { get; }

    internal ImportNode(ImmutableArray<AstAliasNode> names)
    {
        Names = names;
    }

}

public sealed class ImportFromNode : AstStmtNode
{
    internal ImportFromNode(string? module, ImmutableArray<AstAliasNode> names, int level)
    {
        Module = module;
        Names = names;
        Level = level;
    }

    public string? Module { get; }
    public ImmutableArray<AstAliasNode> Names { get; }
    public int Level { get; }

    internal bool IsImportStar()
    {
        return Names.Length is 1 && Names[0].Name is "*";
    }

}

public sealed class GlobalNode : AstStmtNode
{
    internal GlobalNode(ImmutableArray<string> names)
    {
        Names = names;
    }

    public ImmutableArray<string> Names { get; }

}

public sealed class NonlocalNode : AstStmtNode
{
    internal NonlocalNode(ImmutableArray<string> names)
    {
        Names = names;
    }

    public ImmutableArray<string> Names { get; }

}

public sealed class TypeAliasNode : AstStmtNode
{
    internal TypeAliasNode(string name, ImmutableArray<AstTypeParamNode> typeParams, AstExprNode value)
    {
        Name = name;
        TypeParams = typeParams;
        Value = value;
    }

    public string Name { get; }
    public ImmutableArray<AstTypeParamNode> TypeParams { get; }
    public AstExprNode Value { get; }
}
