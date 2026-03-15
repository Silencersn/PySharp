using PySharp.Compilation.Primitives;
using PySharp.Modules.Builtins;

namespace PySharp.Compilation.AstNodes;

public static partial class Ast
{
    public static AstComprehensionNode Comprehension(AstExprNode target, AstExprNode iter, IEnumerable<AstExprNode> ifs)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(iter);
        ArgumentNullException.ThrowIfNull(ifs);

        target.CheckValidTargetThenSetContext(ExprContextType.Store);

        return new AstComprehensionNode(target, iter, ifs.ToImmutableArray(true));
    }

    public static AstKeywordNode Keyword(string? arg, AstExprNode value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return new AstKeywordNode(arg, value);
    }

    public static ExceptHandlerNode ExceptHandler(AstExprNode? type, string? name, IEnumerable<AstStmtNode> body)
    {
        return new ExceptHandlerNode(type, name, body.ToImmutableArray(true));
    }

    public static AstAliasNode Alias(string name, string? asName)
    {
        ArgumentNullException.ThrowIfNull(name);

        return new AstAliasNode(name, asName);
    }

    public static AstArgNode Arg(string arg, AstExprNode? annotation)
    {
        ArgumentNullException.ThrowIfNull(arg);

        return new AstArgNode(arg, annotation);
    }

    public static AstArgumentsNode Arguments(IEnumerable<AstArgNode> posonlyArgs, IEnumerable<AstArgNode> args, AstArgNode? varArg, IEnumerable<AstArgNode> kwonlyArgs, AstArgNode? kwArg, IEnumerable<AstExprNode?> kwDefaults, IEnumerable<AstExprNode> defaults)
    {
        ArgumentNullException.ThrowIfNull(posonlyArgs);
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(kwonlyArgs);
        ArgumentNullException.ThrowIfNull(kwDefaults);
        ArgumentNullException.ThrowIfNull(defaults);

        return new AstArgumentsNode(posonlyArgs.ToImmutableArray(true), args.ToImmutableArray(true), varArg, kwonlyArgs.ToImmutableArray(true), kwArg, [.. kwDefaults], defaults.ToImmutableArray(true));
    }

    public static AstArgumentsNode Arguments()
    {
        return AstArgumentsNode.Empty;
    }

    public static AstWithItemNode WithItem(AstExprNode contextExpr, AstExprNode? optionalVars)
    {
        ArgumentNullException.ThrowIfNull(contextExpr);

        optionalVars?.CheckValidTargetThenSetContext(ExprContextType.Store);

        return new AstWithItemNode(contextExpr, optionalVars);
    }

    public static AstMatchCaseNode MatchCase(AstPatternNode pattern, AstExprNode? guard, IEnumerable<AstStmtNode> body)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        ArgumentNullException.ThrowIfNull(body);

        return new AstMatchCaseNode(pattern, guard, body.ToImmutableArray(true));
    }

    public static MatchValueNode MatchValue(AstExprNode value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return new MatchValueNode(value);
    }

    public static MatchSingletonNode MatchSingleton(PyObject value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return new MatchSingletonNode(value);
    }

    public static MatchSequenceNode MatchSequence(IEnumerable<AstPatternNode> patterns)
    {
        ArgumentNullException.ThrowIfNull(patterns);

        return new MatchSequenceNode(patterns.ToImmutableArray(true));
    }

    public static MatchMappingNode MatchMapping(IEnumerable<AstExprNode> keys, IEnumerable<AstPatternNode> patterns, string? rest)
    {
        ArgumentNullException.ThrowIfNull(keys);
        ArgumentNullException.ThrowIfNull(patterns);

        return new MatchMappingNode(keys.ToImmutableArray(true), patterns.ToImmutableArray(true), rest);
    }

    public static MatchClassNode MatchClass(AstExprNode cls, IEnumerable<AstPatternNode> patterns, IEnumerable<string> kwdAttrs, IEnumerable<AstPatternNode> kwdPatterns)
    {
        ArgumentNullException.ThrowIfNull(cls);
        ArgumentNullException.ThrowIfNull(patterns);
        ArgumentNullException.ThrowIfNull(kwdAttrs);
        ArgumentNullException.ThrowIfNull(kwdPatterns);

        return new MatchClassNode(cls, patterns.ToImmutableArray(true), kwdAttrs.ToImmutableArray(true), kwdPatterns.ToImmutableArray(true));
    }

    public static MatchStarNode MatchStar(string? name)
    {
        return new MatchStarNode(name);
    }

    public static MatchAsNode MatchAs(AstPatternNode? pattern, string? name)
    {
        return new MatchAsNode(pattern, name);
    }

    public static MatchOrNode MatchOr(IEnumerable<AstPatternNode> patterns)
    {
        ArgumentNullException.ThrowIfNull(patterns);

        return new MatchOrNode(patterns.ToImmutableArray(true));
    }

    public static TypeVarNode TypeVar(string name, AstExprNode? bound, AstExprNode? defaultValue)
    {
        ArgumentNullException.ThrowIfNull(name);

        return new TypeVarNode(name, bound, defaultValue);
    }

    public static ParamSpecNode ParamSpec(string name, AstExprNode? defaultValue)
    {
        ArgumentNullException.ThrowIfNull(name);

        return new ParamSpecNode(name, defaultValue);
    }

    public static TypeVarTupleNode TypeVarTuple(string name, AstExprNode? defaultValue)
    {
        ArgumentNullException.ThrowIfNull(name);

        return new TypeVarTupleNode(name, defaultValue);
    }
}
