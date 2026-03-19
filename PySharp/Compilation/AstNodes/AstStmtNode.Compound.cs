using System.Collections.Immutable;

namespace PySharp.Compilation.AstNodes;

public sealed class IfNode : AstStmtNode
{
    public AstExprNode Test { get; }
    public ImmutableArray<AstStmtNode> Body { get; }
    public ImmutableArray<AstStmtNode> OrElse { get; }

    internal IfNode(AstExprNode test, ImmutableArray<AstStmtNode> body, ImmutableArray<AstStmtNode> orElse)
    {
        Test = test;
        Body = body;
        OrElse = orElse;
    }

    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        yield return Test;
        foreach (var stmt in Body) yield return stmt;
        foreach (var stmt in OrElse) yield return stmt;
    }
}

public sealed class WhileNode : AstStmtNode
{
    public AstExprNode Test { get; }
    public ImmutableArray<AstStmtNode> Body { get; }
    public ImmutableArray<AstStmtNode> OrElse { get; }

    public WhileNode(AstExprNode test, ImmutableArray<AstStmtNode> body, ImmutableArray<AstStmtNode> orElse)
    {
        Test = test;
        Body = body;
        OrElse = orElse;
    }

    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        yield return Test;
        foreach (var stmt in Body) yield return stmt;
        foreach (var stmt in OrElse) yield return stmt;
    }
}

public sealed class ForNode : AstStmtNode
{
    internal ForNode(AstExprNode target, AstExprNode iter, ImmutableArray<AstStmtNode> body, ImmutableArray<AstStmtNode> orElse)
    {
        Target = target;
        Iter = iter;
        Body = body;
        OrElse = orElse;
    }

    public AstExprNode Target { get; }
    public AstExprNode Iter { get; }
    public ImmutableArray<AstStmtNode> Body { get; }
    public ImmutableArray<AstStmtNode> OrElse { get; }
    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        yield return Target;
        yield return Iter;
        foreach (var stmt in Body) yield return stmt;
        foreach (var stmt in OrElse) yield return stmt;
    }
}

public sealed class TryNode : AstStmtNode
{
    internal TryNode(ImmutableArray<AstStmtNode> body, ImmutableArray<ExceptHandlerNode> exceptors, ImmutableArray<AstStmtNode> orElse, ImmutableArray<AstStmtNode> finalBody)
    {
        Body = body;
        Exceptors = exceptors;
        OrElse = orElse;
        FinalBody = finalBody;
    }

    public ImmutableArray<AstStmtNode> Body { get; }
    public ImmutableArray<ExceptHandlerNode> Exceptors { get; }
    public ImmutableArray<AstStmtNode> OrElse { get; }
    public ImmutableArray<AstStmtNode> FinalBody { get; }
    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        foreach (var stmt in Body) yield return stmt;
        foreach (var ex in Exceptors) yield return ex;
        foreach (var stmt in OrElse) yield return stmt;
        foreach (var stmt in FinalBody) yield return stmt;
    }
}

public sealed class TryStarNode : AstStmtNode
{
    internal TryStarNode(ImmutableArray<AstStmtNode> body, ImmutableArray<ExceptHandlerNode> exceptors, ImmutableArray<AstStmtNode> orElse, ImmutableArray<AstStmtNode> finalBody)
    {
        Body = body;
        Exceptors = exceptors;
        OrElse = orElse;
        FinalBody = finalBody;
    }

    public ImmutableArray<AstStmtNode> Body { get; }
    public ImmutableArray<ExceptHandlerNode> Exceptors { get; }
    public ImmutableArray<AstStmtNode> OrElse { get; }
    public ImmutableArray<AstStmtNode> FinalBody { get; }

    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        foreach (var stmt in Body) yield return stmt;
        foreach (var ex in Exceptors) yield return ex;
        foreach (var stmt in OrElse) yield return stmt;
        foreach (var stmt in FinalBody) yield return stmt;
    }
}

public sealed class WithNode : AstStmtNode
{
    internal WithNode(ImmutableArray<AstWithItemNode> items, ImmutableArray<AstStmtNode> body)
    {
        Items = items;
        Body = body;
    }

    public ImmutableArray<AstWithItemNode> Items { get; }
    public ImmutableArray<AstStmtNode> Body { get; }

    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        foreach (var item in Items)
            yield return item;
        foreach (var stmt in Body)
            yield return stmt;
    }
}

public sealed class MatchNode : AstStmtNode
{
    internal MatchNode(AstExprNode subject, ImmutableArray<AstMatchCaseNode> cases)
    {
        Subject = subject;
        Cases = cases;
    }

    public AstExprNode Subject { get; }
    public ImmutableArray<AstMatchCaseNode> Cases { get; }

    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        yield return Subject;
        foreach (var c in Cases)
            yield return c;
    }
}

internal interface IScopedSubNodesProvider
{
    IEnumerable<AstNode> EnumerateSubNodesOuterScope();
    IEnumerable<AstNode> EnumerateSubNodesInnerScope();
}


public sealed class FunctionDefNode : AstStmtNode, IScopedSubNodesProvider
{
    internal FunctionDefNode(string name, AstArgumentsNode args, ImmutableArray<AstStmtNode> body, ImmutableArray<AstExprNode> decoratorList, AstExprNode? returns, ImmutableArray<AstTypeParamNode> typeParams)
    {
        Name = name;
        Args = args;
        Body = body;
        DecoratorList = decoratorList;
        Returns = returns;
        TypeParams = typeParams;
    }

    public string Name { get; }
    public AstArgumentsNode Args { get; }
    public ImmutableArray<AstStmtNode> Body { get; }
    public ImmutableArray<AstExprNode> DecoratorList { get; }
    public AstExprNode? Returns { get; }
    public ImmutableArray<AstTypeParamNode> TypeParams { get; }

    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        foreach (var d in DecoratorList)
            yield return d;

        yield return Args;

        foreach (var stmt in Body)
            yield return stmt;
    }

    IEnumerable<AstNode> IScopedSubNodesProvider.EnumerateSubNodesOuterScope()
    {
        foreach (var d in DecoratorList)
            yield return d;

        foreach (var d in Args.KwDefaults)
            if (d is not null)
                yield return d;

        foreach (var d in Args.Defaults)
            yield return d;
    }

    IEnumerable<AstNode> IScopedSubNodesProvider.EnumerateSubNodesInnerScope()
    {
        foreach (var n in Args.PosonlyArgs)
            yield return n;

        foreach (var n in Args.Args)
            yield return n;

        if (Args.VarArg is not null)
            yield return Args.VarArg;

        foreach (var n in Args.KwonlyArgs)
            yield return n;

        if (Args.KwArg is not null)
            yield return Args.KwArg;

        foreach (var stmt in Body)
            yield return stmt;
    }
}

public sealed class AsyncFunctionDefNode : AstStmtNode, IScopedSubNodesProvider
{
    internal AsyncFunctionDefNode(string name, AstArgumentsNode args, ImmutableArray<AstStmtNode> body, ImmutableArray<AstExprNode> decoratorList, AstExprNode? returns, ImmutableArray<AstTypeParamNode> typeParams)
    {
        Name = name;
        Args = args;
        Body = body;
        DecoratorList = decoratorList;
        Returns = returns;
        TypeParams = typeParams;
    }

    public string Name { get; }
    public AstArgumentsNode Args { get; }
    public ImmutableArray<AstStmtNode> Body { get; }
    public ImmutableArray<AstExprNode> DecoratorList { get; }
    public AstExprNode? Returns { get; }
    public ImmutableArray<AstTypeParamNode> TypeParams { get; }

    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        foreach (var d in DecoratorList)
            yield return d;

        yield return Args;

        foreach (var stmt in Body)
            yield return stmt;
    }

    IEnumerable<AstNode> IScopedSubNodesProvider.EnumerateSubNodesOuterScope()
    {
        foreach (var d in DecoratorList)
            yield return d;

        foreach (var d in Args.KwDefaults)
            if (d is not null)
                yield return d;

        foreach (var d in Args.Defaults)
            yield return d;
    }

    IEnumerable<AstNode> IScopedSubNodesProvider.EnumerateSubNodesInnerScope()
    {
        foreach (var n in Args.PosonlyArgs)
            yield return n;

        foreach (var n in Args.Args)
            yield return n;

        if (Args.VarArg is not null)
            yield return Args.VarArg;

        foreach (var n in Args.KwonlyArgs)
            yield return n;

        if (Args.KwArg is not null)
            yield return Args.KwArg;

        foreach (var stmt in Body)
            yield return stmt;
    }
}

public sealed class ClassDefNode : AstStmtNode, IScopedSubNodesProvider
{
    internal ClassDefNode(string name, ImmutableArray<AstExprNode> bases, ImmutableArray<AstKeywordNode> keywords, ImmutableArray<AstStmtNode> body, ImmutableArray<AstExprNode> decoratorList, ImmutableArray<AstTypeParamNode> typeParams)
    {
        Name = name;
        Bases = bases;
        Keywords = keywords;
        Body = body;
        DecoratorList = decoratorList;
        TypeParams = typeParams;
    }

    public string Name { get; }
    public ImmutableArray<AstExprNode> Bases { get; }
    public ImmutableArray<AstKeywordNode> Keywords { get; }
    public ImmutableArray<AstStmtNode> Body { get; }
    public ImmutableArray<AstExprNode> DecoratorList { get; }
    public ImmutableArray<AstTypeParamNode> TypeParams { get; }

    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        foreach (var d in DecoratorList)
            yield return d;

        foreach (var b in Bases)
            yield return b;

        foreach (var k in Keywords)
            yield return k;

        foreach (var stmt in Body)
            yield return stmt;
    }

    IEnumerable<AstNode> IScopedSubNodesProvider.EnumerateSubNodesOuterScope()
    {
        foreach (var d in DecoratorList)
            yield return d;

        foreach (var b in Bases)
            yield return b;

        foreach (var k in Keywords)
            yield return k;
    }

    IEnumerable<AstNode> IScopedSubNodesProvider.EnumerateSubNodesInnerScope()
    {
        foreach (var stmt in Body)
            yield return stmt;
    }
}