using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

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
}


public class AstArgNode : AstNode
{
    public string Arg { get; }

    public AstArgNode(string arg)
    {
        Arg = arg;
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
}


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
