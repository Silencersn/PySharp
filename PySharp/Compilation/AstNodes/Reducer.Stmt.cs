using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.Calls.Extensions;
using System.Collections.Immutable;
using System.Diagnostics;

namespace PySharp.Compilation.AstNodes;

partial class Reducer
{
    public static ImmutableArray<AstStmtNode> ReduceStmts(IEnumerable<AstStmtNode> stmts)
    {
        var builder = ImmutableArray.CreateBuilder<AstStmtNode>();
        foreach (var stmt in stmts)
            ReduceStmt(stmt, builder);
        return builder.DrainToImmutable();
    }

    public static void ReduceStmt(AstStmtNode stmt, ImmutableArray<AstStmtNode>.Builder builder)
    {
        switch (stmt)
        {
            case AssertNode n: ReduceAssert(n, builder); break;
            case AssignNode n: ReduceAssign(n, builder); break;
            case AnnAssignNode n: ReduceAnnAssign(n, builder); break;
            case DeleteNode n: ReduceDelete(n, builder); break;
            case AugAssignNode n: ReduceAugAssign(n, builder); break;
            case ExprNode n: ReduceExpr(n, builder); break;
            case BreakNode n: ReduceBreak(n, builder); break;
            case ContinueNode n: ReduceContinue(n, builder); break;
            case ReturnNode n: ReduceReturn(n, builder); break;
            case PassNode n: ReducePass(n, builder); break;
            case RaiseNode n: ReduceRaise(n, builder); break;
            case GlobalNode n: ReduceGlobal(n, builder); break;
            case NonlocalNode n: ReduceNonlocal(n, builder); break;
            case IfNode n: ReduceIf(n, builder); break;
            case ForNode n: ReduceFor(n, builder); break;
            case WhileNode n: ReduceWhile(n, builder); break;
            case TryNode n: ReduceTry(n, builder); break;
            case TryStarNode n: ReduceTryStar(n, builder); break;
            case ImportNode n: ReduceImport(n, builder); break;
            case ImportFromNode n: ReduceImportFrom(n, builder); break;
            case FunctionDefNode n: ReduceFunctionDef(n, builder); break;
            case ClassDefNode n: ReduceClassDef(n, builder); break;
            case WithNode n: ReduceWith(n, builder); break;
            case MatchNode n: ReduceMatch(n, builder); break;
            default: throw new UnreachableException();
        }
    }

    public static void ReduceAssert(AssertNode node, ImmutableArray<AstStmtNode>.Builder builder)
    {
        builder.Add(node);
    }
    public static void ReduceAssign(AssignNode node, ImmutableArray<AstStmtNode>.Builder builder)
    {
        builder.Add(node);
    }
    public static void ReduceAnnAssign(AnnAssignNode node, ImmutableArray<AstStmtNode>.Builder builder)
    {
        builder.Add(node);
    }
    public static void ReduceDelete(DeleteNode node, ImmutableArray<AstStmtNode>.Builder builder)
    {
        builder.Add(node);
    }
    public static void ReduceAugAssign(AugAssignNode node, ImmutableArray<AstStmtNode>.Builder builder)
    {
        builder.Add(node);
    }
    public static void ReduceExpr(ExprNode node, ImmutableArray<AstStmtNode>.Builder builder)
    {
        builder.Add(node);
    }
    public static void ReduceBreak(BreakNode node, ImmutableArray<AstStmtNode>.Builder builder)
    {
        builder.Add(node);
    }
    public static void ReduceContinue(ContinueNode node, ImmutableArray<AstStmtNode>.Builder builder)
    {
        builder.Add(node);
    }
    public static void ReduceReturn(ReturnNode node, ImmutableArray<AstStmtNode>.Builder builder)
    {
        builder.Add(node);
    }
    public static void ReducePass(PassNode node, ImmutableArray<AstStmtNode>.Builder builder)
    {
        // do nothing
    }
    public static void ReduceRaise(RaiseNode node, ImmutableArray<AstStmtNode>.Builder builder)
    {
        builder.Add(node);
    }
    public static void ReduceGlobal(GlobalNode node, ImmutableArray<AstStmtNode>.Builder builder)
    {
        // do nothing
    }
    public static void ReduceNonlocal(NonlocalNode node, ImmutableArray<AstStmtNode>.Builder builder)
    {
        // do nothing
    }
    public static void ReduceIf(IfNode node, ImmutableArray<AstStmtNode>.Builder builder)
    {
        var test = ReduceExpr(node.Test);

        if (test is ConstantNode constantNode)
        {
            var boolValue = PySpecialMethods.Bool(PyCallContext.NonContextDependency, constantNode.Value)
                .PyUnwrap(PyCallContext.NonContextDependency).BoolValue;
            builder.AddRange(ReduceStmts(boolValue ? node.Body : node.OrElse));
            return;
        }

        var body = ReduceStmts(node.Body);
        var orElse = ReduceStmts(node.OrElse);
        var newNode = Ast.If(test, body, orElse).With(node.MetaInfo);
        builder.Add(newNode);
    }
    public static void ReduceFor(ForNode node, ImmutableArray<AstStmtNode>.Builder builder)
    {
        builder.Add(node);
    }
    public static void ReduceWhile(WhileNode node, ImmutableArray<AstStmtNode>.Builder builder)
    {
        builder.Add(node);
    }
    public static void ReduceTry(TryNode node, ImmutableArray<AstStmtNode>.Builder builder)
    {
        builder.Add(node);
    }
    public static void ReduceTryStar(TryStarNode node, ImmutableArray<AstStmtNode>.Builder builder)
    {
        builder.Add(node);
    }
    public static void ReduceImport(ImportNode node, ImmutableArray<AstStmtNode>.Builder builder)
    {
        builder.Add(node);
    }
    public static void ReduceImportFrom(ImportFromNode node, ImmutableArray<AstStmtNode>.Builder builder)
    {
        builder.Add(node);
    }
    public static void ReduceFunctionDef(FunctionDefNode node, ImmutableArray<AstStmtNode>.Builder builder)
    {
        builder.Add(node);
    }
    public static void ReduceClassDef(ClassDefNode node, ImmutableArray<AstStmtNode>.Builder builder)
    {
        builder.Add(node);
    }
    public static void ReduceWith(WithNode node, ImmutableArray<AstStmtNode>.Builder builder)
    {
        builder.Add(node);
    }
    public static void ReduceMatch(MatchNode node, ImmutableArray<AstStmtNode>.Builder builder)
    {
        builder.Add(node);
    }
}
