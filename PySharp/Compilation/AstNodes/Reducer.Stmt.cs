using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.Calls.Extensions;
using System.Collections.Immutable;
using System.Diagnostics;

namespace PySharp.Compilation.AstNodes;

partial class Reducer
{
    private static ImmutableArray<AstStmtNode> ReduceStmts(ImmutableArray<AstStmtNode> stmts, out bool changed)
    {
        ImmutableArray<AstStmtNode>.Builder? builder = null;

        for (int i = 0; i < stmts.Length; i++)
        {
            var stmt = stmts[i];
            var reduced = ReduceStmt(stmt);

            if (!ReferenceEquals(reduced, stmt) && builder is null)
            {
                builder = ImmutableArray.CreateBuilder<AstStmtNode>(stmts.Length);
                builder.AddRange(stmts.AsSpan()[..i]);
            }
            builder?.Add(reduced);
        }

        changed = builder is not null;

        if (builder is null)
            return stmts;

        Debug.Assert(builder.Count == builder.Capacity);
        return builder.MoveToImmutable();
    }

    private static AstStmtNode ReduceStmt(AstStmtNode node)
    {
        AstStmtNode reduced = (node switch
        {
            AssertNode n => ReduceAssert(n),
            AssignNode n => ReduceAssign(n),
            AnnAssignNode n => ReduceAnnAssign(n),
            DeleteNode n => ReduceDelete(n),
            AugAssignNode n => ReduceAugAssign(n),
            ExprNode n => ReduceExpr(n),
            BreakNode n => ReduceBreak(n),
            ContinueNode n => ReduceContinue(n),
            ReturnNode n => ReduceReturn(n),
            PassNode n => ReducePass(n),
            RaiseNode n => ReduceRaise(n),
            GlobalNode n => ReduceGlobal(n),
            NonlocalNode n => ReduceNonlocal(n),
            IfNode n => ReduceIf(n),
            ForNode n => ReduceFor(n),
            WhileNode n => ReduceWhile(n),
            TryNode n => ReduceTry(n),
            TryStarNode n => ReduceTryStar(n),
            ImportNode n => ReduceImport(n),
            ImportFromNode n => ReduceImportFrom(n),
            FunctionDefNode n => ReduceFunctionDef(n),
            ClassDefNode n => ReduceClassDef(n),
            WithNode n => ReduceWith(n),
            MatchNode n => ReduceMatch(n),
            _ => throw new UnreachableException()
        });
        return reduced.With(node.MetaInfo);
    }

    private static AssertNode ReduceAssert(AssertNode node)
    {
        return node;
    }
    private static AssignNode ReduceAssign(AssignNode node)
    {
        return node;
    }
    private static AnnAssignNode ReduceAnnAssign(AnnAssignNode node)
    {
        return node;
    }
    private static DeleteNode ReduceDelete(DeleteNode node)
    {
        return node;
    }
    private static AugAssignNode ReduceAugAssign(AugAssignNode node)
    {
        return node;
    }
    private static ExprNode ReduceExpr(ExprNode node)
    {
        return node;
    }
    private static BreakNode ReduceBreak(BreakNode node)
    {
        return node;
    }
    private static ContinueNode ReduceContinue(ContinueNode node)
    {
        return node;
    }
    private static ReturnNode ReduceReturn(ReturnNode node)
    {
        return node;
    }
    private static PassNode ReducePass(PassNode node)
    {
        return node;
    }
    private static RaiseNode ReduceRaise(RaiseNode node)
    {
        return node;
    }
    private static GlobalNode ReduceGlobal(GlobalNode node)
    {
        return node;
    }
    private static NonlocalNode ReduceNonlocal(NonlocalNode node)
    {
        return node;
    }
    private static IfNode ReduceIf(IfNode node)
    {
        var test = ReduceExpr(node.Test, out var testChanged);
        var body = ReduceStmts(node.Body, out var bodyChanged);
        var orElse = ReduceStmts(node.OrElse, out var orElseChanged);
        if (testChanged || bodyChanged || orElseChanged)
            return Ast.If(test, body, orElse);
        return node;
    }
    private static ForNode ReduceFor(ForNode node)
    {
        return node;
    }
    private static WhileNode ReduceWhile(WhileNode node)
    {
        return node;
    }
    private static TryNode ReduceTry(TryNode node)
    {
        return node;
    }
    private static TryStarNode ReduceTryStar(TryStarNode node)
    {
        return node;
    }
    private static ImportNode ReduceImport(ImportNode node)
    {
        return node;
    }
    private static ImportFromNode ReduceImportFrom(ImportFromNode node)
    {
        return node;
    }
    private static FunctionDefNode ReduceFunctionDef(FunctionDefNode node)
    {
        return node;
    }
    private static ClassDefNode ReduceClassDef(ClassDefNode node)
    {
        return node;
    }
    private static WithNode ReduceWith(WithNode node)
    {
        return node;
    }
    private static MatchNode ReduceMatch(MatchNode node)
    {
        return node;
    }
}
