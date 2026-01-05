using System.Collections.Immutable;

namespace PySharp.AstNodes;

public class AstAliasNode : AstNode
{
    public AstAliasNode(string name, string? asName)
    {
        Name = name;
        AsName = asName;
    }

    public string Name { get; }
    public string? AsName { get; }

    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        return [];
    }
}


public class AstArgNode : AstNode
{
    public string Arg { get; }

    public AstArgNode(string arg)
    {
        Arg = arg;
    }

    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        return [];
    }
}

public class AstArgumentsNode : AstNode
{
    public AstArgumentsNode()
    {
        PosonlyArgs = [];
        Args = [];
        KwonlyArgs = [];
        KwDefaults = [];
        Defaults = [];
    }

    public List<AstArgNode> PosonlyArgs { get; }
    public List<AstArgNode> Args { get; }
    public AstArgNode? VarArg { get; set; }
    public List<AstArgNode> KwonlyArgs { get; }
    public AstArgNode? KwArg { get; set; }
    public List<AstExprNode?> KwDefaults { get; }
    public List<AstExprNode> Defaults { get; }

    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        foreach (var n in PosonlyArgs) yield return n;
        foreach (var n in Args) yield return n;
        if (VarArg is not null)
            yield return VarArg;
        foreach (var n in KwonlyArgs) yield return n;
        if (KwArg is not null)
            yield return KwArg;
        foreach (var d in KwDefaults)
            if (d is not null)
                yield return d;
        foreach (var d in Defaults) yield return d;
    }
}

public class AstComprehensionNode : AstNode
{
    internal AstComprehensionNode(AstExprNode target, AstExprNode iter, ImmutableArray<AstExprNode> ifs)
    {
        Target = target;
        Iter = iter;
        Ifs = ifs;
    }

    public AstExprNode Target { get; }
    public AstExprNode Iter { get; }
    public ImmutableArray<AstExprNode> Ifs { get; }

    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        yield return Target;
        yield return Iter;
        foreach (var f in Ifs) yield return f;
    }
}


public class AstKeywordNode : AstNode
{
    internal AstKeywordNode(string arg, AstExprNode value)
    {
        Arg = arg;
        Value = value;
    }

    public string Arg { get; } // TODO: string? Arg
    public AstExprNode Value { get; }

    internal override void Dump(AstNodeDumper dumper)
    {
        dumper
            .Append("Keyword")
            .AppendFields(("arg", Arg), ("value", Value));
    }

    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        yield return Value;
    }
}
