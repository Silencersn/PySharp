namespace PySharp.AstNodes;

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
        return Arguments([], [], null, [], null, [], []);
    }
}
