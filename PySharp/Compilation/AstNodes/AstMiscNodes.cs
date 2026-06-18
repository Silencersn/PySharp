using PySharp.Modules.Builtins;
using System.Collections.Immutable;

namespace PySharp.Compilation.AstNodes;

public class AstAliasNode : AstNode
{
    internal AstAliasNode(string name, string? asName)
    {
        Name = name;
        AsName = asName;
    }

    public string Name { get; }
    public string? AsName { get; }

    internal string GetLocalName()
    {
        if (AsName is not null)
            return AsName;

        var index = Name.IndexOf('.');
        if (index is -1)
            return Name;
        return Name[..index];
    }
}


public class AstArgNode : AstNode
{
    public string Arg { get; }
    public AstExprNode? Annotation { get; }

    internal AstArgNode(string arg, AstExprNode? annotation = null)
    {
        Arg = arg;
        Annotation = annotation;
    }

}

public class AstArgumentsNode : AstNode
{
    internal static AstArgumentsNode Empty { get; } = new([], [], null, [], null, [], []);
    internal static AstArgumentsNode GeneratorExp { get; } = new([Ast.Arg(".0", null)], [], null, [], null, [], []);

    internal AstArgumentsNode(ImmutableArray<AstArgNode> posonlyArgs, ImmutableArray<AstArgNode> args, AstArgNode? varArg, ImmutableArray<AstArgNode> kwonlyArgs, AstArgNode? kwArg, ImmutableArray<AstExprNode?> kwDefaults, ImmutableArray<AstExprNode> defaults)
    {
        PosonlyArgs = posonlyArgs;
        Args = args;
        VarArg = varArg;
        KwonlyArgs = kwonlyArgs;
        KwArg = kwArg;
        KwDefaults = kwDefaults;
        Defaults = defaults;
    }

    public ImmutableArray<AstArgNode> PosonlyArgs { get; }
    public ImmutableArray<AstArgNode> Args { get; }
    public AstArgNode? VarArg { get; }
    public ImmutableArray<AstArgNode> KwonlyArgs { get; }
    public AstArgNode? KwArg { get; }
    public ImmutableArray<AstExprNode?> KwDefaults { get; }
    public ImmutableArray<AstExprNode> Defaults { get; }

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
    internal AstKeywordNode(string? arg, AstExprNode value)
    {
        Arg = arg;
        Value = value;
    }

    public string? Arg { get; }
    public AstExprNode Value { get; }
}

public sealed class ExceptHandlerNode : AstNode
{
    internal ExceptHandlerNode(AstExprNode? type, string? name, ImmutableArray<AstStmtNode> body)
    {
        Type = type;
        Name = name;
        Body = body;
    }

    public AstExprNode? Type { get; }
    public string? Name { get; }
    public ImmutableArray<AstStmtNode> Body { get; }

}

public sealed class AstWithItemNode : AstNode
{
    internal AstWithItemNode(AstExprNode contextExpr, AstExprNode? optionalVars)
    {
        ContextExpr = contextExpr;
        OptionalVars = optionalVars;
    }

    public AstExprNode ContextExpr { get; }
    public AstExprNode? OptionalVars { get; }

}

public sealed class AstMatchCaseNode : AstNode
{
    internal AstMatchCaseNode(AstPatternNode pattern, AstExprNode? guard, ImmutableArray<AstStmtNode> body)
    {
        Pattern = pattern;
        Guard = guard;
        Body = body;
    }

    public AstPatternNode Pattern { get; }
    public AstExprNode? Guard { get; }
    public ImmutableArray<AstStmtNode> Body { get; }
}

public abstract class AstPatternNode : AstNode
{
    public abstract IEnumerable<AstPatternNode> EnumerateSubPatterns();
}

public sealed class MatchValueNode : AstPatternNode
{
    internal MatchValueNode(AstExprNode value)
    {
        Value = value;
    }

    public AstExprNode Value { get; }

    public override IEnumerable<AstPatternNode> EnumerateSubPatterns()
    {
        return [];
    }
}

public sealed class MatchSingletonNode : AstPatternNode
{
    internal MatchSingletonNode(PyObject value)
    {
        Value = value;
    }

    public PyObject Value { get; }

    public override IEnumerable<AstPatternNode> EnumerateSubPatterns()
    {
        return [];
    }
}

public sealed class MatchSequenceNode : AstPatternNode
{
    internal MatchSequenceNode(ImmutableArray<AstPatternNode> patterns)
    {
        Patterns = patterns;
    }

    public ImmutableArray<AstPatternNode> Patterns { get; }

    public override IEnumerable<AstPatternNode> EnumerateSubPatterns()
    {
        foreach (var p in Patterns)
            yield return p;
    }
}

public sealed class MatchMappingNode : AstPatternNode
{
    internal MatchMappingNode(ImmutableArray<AstExprNode> keys, ImmutableArray<AstPatternNode> patterns, string? rest)
    {
        Keys = keys;
        Patterns = patterns;
        Rest = rest;
    }

    public ImmutableArray<AstExprNode> Keys { get; }
    public ImmutableArray<AstPatternNode> Patterns { get; }
    public string? Rest { get; }

    public override IEnumerable<AstPatternNode> EnumerateSubPatterns()
    {
        foreach (var p in Patterns)
            yield return p;
    }
}

public sealed class MatchClassNode : AstPatternNode
{
    internal MatchClassNode(AstExprNode cls, ImmutableArray<AstPatternNode> patterns, ImmutableArray<string> kwdAttrs, ImmutableArray<AstPatternNode> kwdPatterns)
    {
        Cls = cls;
        Patterns = patterns;
        KwdAttrs = kwdAttrs;
        KwdPatterns = kwdPatterns;
    }

    public AstExprNode Cls { get; }
    public ImmutableArray<AstPatternNode> Patterns { get; }
    public ImmutableArray<string> KwdAttrs { get; }
    public ImmutableArray<AstPatternNode> KwdPatterns { get; }

    public override IEnumerable<AstPatternNode> EnumerateSubPatterns()
    {
        foreach (var p in Patterns)
            yield return p;
        foreach (var kp in KwdPatterns)
            yield return kp;
    }
}

public sealed class MatchStarNode : AstPatternNode
{
    internal MatchStarNode(string? name)
    {
        Name = name;
    }

    public string? Name { get; }

    public override IEnumerable<AstPatternNode> EnumerateSubPatterns()
    {
        return [];
    }
}

public sealed class MatchAsNode : AstPatternNode
{
    internal MatchAsNode(AstPatternNode? pattern, string? name)
    {
        Pattern = pattern;
        Name = name;
    }

    public AstPatternNode? Pattern { get; }
    public string? Name { get; }

    public override IEnumerable<AstPatternNode> EnumerateSubPatterns()
    {
        if (Pattern is not null)
            yield return Pattern;
    }
}

public sealed class MatchOrNode : AstPatternNode
{
    internal MatchOrNode(ImmutableArray<AstPatternNode> patterns)
    {
        Patterns = patterns;
    }

    public ImmutableArray<AstPatternNode> Patterns { get; }

    public override IEnumerable<AstPatternNode> EnumerateSubPatterns()
    {
        foreach (var p in Patterns)
            yield return p;
    }
}

public abstract class AstTypeParamNode : AstNode;

public sealed class TypeVarNode : AstTypeParamNode
{
    internal TypeVarNode(string name, AstExprNode? bound, AstExprNode? defaultValue)
    {
        Name = name;
        Bound = bound;
        DefaultValue = defaultValue;
    }

    public string Name { get; }
    public AstExprNode? Bound { get; }
    public AstExprNode? DefaultValue { get; }

}

public sealed class ParamSpecNode : AstTypeParamNode
{
    internal ParamSpecNode(string name, AstExprNode? defaultValue)
    {
        Name = name;
        DefaultValue = defaultValue;
    }

    public string Name { get; }
    public AstExprNode? DefaultValue { get; }

}

public sealed class TypeVarTupleNode : AstTypeParamNode
{
    internal TypeVarTupleNode(string name, AstExprNode? defaultValue)
    {
        Name = name;
        DefaultValue = defaultValue;
    }

    public string Name { get; }
    public AstExprNode? DefaultValue { get; }

}
