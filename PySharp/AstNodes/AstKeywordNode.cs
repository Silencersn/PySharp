namespace PySharp.AstNodes;

public class AstKeywordNode : AstNode
{
    internal AstKeywordNode(string arg, AstExprNode value)
    {
        Arg = arg;
        Value = value;
    }

    public string Arg { get; }
    public AstExprNode Value { get; }

    internal override void Dump(AstNodeDumper dumper)
    {
        dumper
            .Append("Keyword")
            .AppendFields(("arg", Arg), ("value", Value));
    }
}
