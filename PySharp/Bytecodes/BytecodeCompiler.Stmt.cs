using PySharp.AstNodes;
using System;
using System.Collections.Generic;
using System.Text;

namespace PySharp.Bytecodes;

partial class BytecodeCompiler
{
    private void CompileStmt(AstStmtNode node)
    {
        switch (node)
        {
            case ExprNode n: CompileExpr(n); break;
            case AssignNode n: CompileAssign(n); break;
            case IfNode n: CompileIf(n); break;
            default: throw new NotImplementedException();
        }
    }

    private void CompileExpr(ExprNode node)
    {
        LoadExpr(node.Value);
        Generator.Emit(OpCode.PopTop);
    }

    private void CompileAssign(AssignNode node)
    {
        LoadExpr(node.Value);
        for (int i = 0; i < node.Targets.Length; i++)
        {
            if (i < node.Targets.Length - 1)
                Generator.Emit(OpCode.Copy);
            StoreExpr(node.Targets[i]);
        }
    }

    private void CompileIf(IfNode node)
    {
        var elseBlockLabel = Generator.DefineLabel();
        var ifStmtEndLabel = Generator.DefineLabel();

        LoadExpr(node.Test);
        Generator.Emit(OpCode.ToBool);
        Generator.Emit(OpCode.PopJumpIfFalse, elseBlockLabel);

        foreach (var stmt in node.Body)
            CompileStmt(stmt);
        Generator.Emit(OpCode.Jump, ifStmtEndLabel);

        Generator.MarkLabel(elseBlockLabel);
        foreach (var stmt in node.OrElse)
            CompileStmt(stmt);

        Generator.MarkLabel(ifStmtEndLabel);
    }
}
