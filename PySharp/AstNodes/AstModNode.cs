using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;

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

    public override void Execute(PyCallContext context, PyFrame frame)
    {
        frame.StmtMetaInfoProvider = this;
        foreach (var stmt in Body)
        {
            stmt.Execute(context, frame);
        }
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

    public override void Execute(PyCallContext context, PyFrame frame)
    {
        frame.StmtMetaInfoProvider = this;
        _ = Body.GetExprValue(context, frame);
    }
}

public class InteractiveNode : AstModNode
{
    public List<AstStmtNode> Body { get; }

    public InteractiveNode(List<AstStmtNode> body)
    {
        Body = body;
    }

    public override void Execute(PyCallContext context, PyFrame frame)
    {
        frame.StmtMetaInfoProvider = this;
        foreach (var stmt in Body)
        {
            stmt.Execute(context, frame);
        }
    }
}