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

    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        yield return Value;
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

    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        foreach (var t in Targets) yield return t;
        yield return Value;
    }
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

    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        yield return Target;
        yield return Value;
    }
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

    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        yield return Target;

        // TODO: if EnumerateSubNodes is called by SemanticAnalyzer, it should not enumerate Annotation
        //yield return Annotation;

        if (Value is not null)
            yield return Value;
    }
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

    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        yield return Test;
        if (Msg is not null)
            yield return Msg;
    }
}

public sealed class PassNode : AstStmtNode
{
    internal PassNode()
    {
    }
    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        return [];
    }
}

public sealed class DeleteNode : AstStmtNode
{
    public ImmutableArray<AstExprNode> Targets { get; }

    internal DeleteNode(ImmutableArray<AstExprNode> targets)
    {
        Targets = targets;
    }
    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        foreach (var t in Targets) yield return t;
    }
}

public sealed class ReturnNode : AstStmtNode
{
    public AstExprNode? Value { get; }

    internal ReturnNode(AstExprNode? value)
    {
        Value = value;
    }

    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        if (Value is not null)
            yield return Value;
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

    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        if (Exc is not null)
            yield return Exc;
        if (Cause is not null)
            yield return Cause;
    }
}

public sealed class BreakNode : AstStmtNode
{
    internal BreakNode()
    {
    }
    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        return [];
    }
}

public sealed class ContinueNode : AstStmtNode
{
    internal ContinueNode()
    {
    }
    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        return [];
    }
}

public sealed class ImportNode : AstStmtNode
{
    public ImmutableArray<AstAliasNode> Names { get; }

    internal ImportNode(ImmutableArray<AstAliasNode> names)
    {
        Names = names;
    }

    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        foreach (var n in Names) yield return n;
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

    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        return Names;
    }
}

public sealed class GlobalNode : AstStmtNode
{
    internal GlobalNode(ImmutableArray<string> names)
    {
        Names = names;
    }

    public ImmutableArray<string> Names { get; }

    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        return [];
    }
}

public sealed class NonlocalNode : AstStmtNode
{
    internal NonlocalNode(ImmutableArray<string> names)
    {
        Names = names;
    }

    public ImmutableArray<string> Names { get; }

    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        return [];
    }
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

    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        yield return Value;
    }
}
