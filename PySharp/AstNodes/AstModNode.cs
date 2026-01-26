using PySharp.PyModules.Builtins;
using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;
using System.Collections.Immutable;

namespace PySharp.AstNodes;

public abstract class AstModNode : AstNode
{
}

public class ModuleNode : AstModNode
{
    internal ModuleNode(ImmutableArray<AstStmtNode> body)
    {
        Body = body;
    }

    public ImmutableArray<AstStmtNode> Body { get; }

    public override void Execute(PyCallContext context, PyFrame frame)
    {
        if (frame.SemanticModel?.GetVariableScope<RootVariableScope>(this) is null)
            throw new InvalidOperationException();

        using var withMetaInfo = new MetaInfoProviderSetter(frame, this);
        if (AstUtils.TryGetDoc(Body, out var doc))
            frame.SetVariable(PySpecialNames.Doc, doc);
        foreach (var stmt in Body)
            stmt.Execute(context, frame);
    }

    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        return Body;
    }
}

public class ExpressionNode : AstModNode
{
    public AstExprNode Body { get; }

    internal ExpressionNode(AstExprNode body)
    {
        Body = body;
    }

    public override void Execute(PyCallContext context, PyFrame frame)
    {
        _ = GetExprValue(context, frame);
    }

    public PyObject GetExprValue(PyCallContext context, PyFrame frame)
    {
        if (frame.SemanticModel?.GetVariableScope<RootVariableScope>(this) is null)
            throw new InvalidOperationException();

        using var withMetaInfo = new MetaInfoProviderSetter(frame, this);
        return Body.GetExprValue(context, frame);
    }

    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        yield return Body;
    }
}

public class InteractiveNode : AstModNode
{
    public ImmutableArray<AstStmtNode> Body { get; }

    internal InteractiveNode(ImmutableArray<AstStmtNode> body)
    {
        Body = body;
    }

    public override void Execute(PyCallContext context, PyFrame frame)
    {
        if (frame.SemanticModel?.GetVariableScope<RootVariableScope>(this) is null)
            throw new InvalidOperationException();

        using var withMetaInfo = new MetaInfoProviderSetter(frame, this);
        foreach (var stmt in Body)
        {
            stmt.Execute(context, frame);
        }
    }

    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        return Body;
    }
}