using PySharp.PyObjects.Builtins;
using PySharp.PyRuntime;

namespace PySharp.AstNodes;

public abstract class AstModNode : AstNode;

public class ModuleNode : AstModNode
{
    internal ModuleNode()
    {
    }
    internal ModuleNode(List<AstStmtNode> body)
    {
        Body = body;
    }

    public List<AstStmtNode> Body { get; } = [];

    public override void Execute(PyFrame frame)
    {
        foreach (var stmt in Body)
        {
            stmt.Execute(frame);
        }
    }

    public override ModuleNode Reduce(OptimizationOptions options)
    {
        if (options.NoOptimization)
            return this;

        return new ModuleNode([.. Body.Reduce(options)]);
    }

    internal override void Dump(AstNodeDumper dumper)
    {
        dumper
            .Append("Module")
            .AppendFields(("body", Body));
    }

    public override void EnumerateNodes(Action<AstNode> action)
    {
        base.EnumerateNodes(action);
        Body.EnumerateNodes(action);
    }
}

public class ExpressionNode : AstModNode
{
    public AstExprNode Body { get; }

    public ExpressionNode(AstExprNode body)
    {
        Body = body;
    }

    public override void Execute(PyFrame frame)
    {
        _ = Body.GetExprValue(frame);
    }

    public override ExpressionNode Reduce(OptimizationOptions options)
    {
        if (options.NoOptimization)
            return this;

        return new ExpressionNode(Body.Reduce(options));
    }
}

public class InteractiveNode : AstModNode
{
    public List<AstStmtNode> Body { get; }

    public InteractiveNode(List<AstStmtNode> body)
    {
        Body = body;
    }

    public override void Execute(PyFrame frame)
    {
        foreach (var stmt in Body)
        {
            stmt.Execute(frame);
        }
    }

    public override InteractiveNode Reduce(OptimizationOptions options)
    {
        if (options.NoOptimization)
            return this;

        return new InteractiveNode([.. Body.Reduce(options)]);
    }
}