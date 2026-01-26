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
}
