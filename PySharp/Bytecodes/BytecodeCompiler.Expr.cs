using PySharp.AstNodes;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Text;

namespace PySharp.Bytecodes;

partial class BytecodeCompiler
{
    private void LoadExpr(AstExprNode node) => CompileExpr(node, ExprContextType.Load);
    private void StoreExpr(AstExprNode node) => CompileExpr(node, ExprContextType.Store);
    private void DeleteExpr(AstExprNode node) => CompileExpr(node, ExprContextType.Del);

    private void CompileExpr(AstExprNode node, ExprContextType ctx)
    {
        switch (node)
        {
            case ConstantNode n: CompileConstant(n); break;
            case NameNode n: CompileName(n, ctx); break;
            case CallNode n: CompileCall(n); break;
            case BinOpNode n: CompileBinOp(n); break;
            case UnaryOpNode n: CompileUnaryOp(n); break;
            default: throw new NotImplementedException();
        }
    }

    private void CompileConstant(ConstantNode node)
    {
        Generator.Emit(OpCode.LoadConst, node.Value);
    }

    private void CompileName(NameNode node, ExprContextType ctx)
    {
        if (CurrentScope is RootVariableScope)
        {
            var opCode = ctx switch
            {
                ExprContextType.Load => OpCode.LoadGlobal,
                ExprContextType.Store => OpCode.StoreGlobal,
                ExprContextType.Del => OpCode.DeleteGlobal,

                _ => throw new InvalidOperationException()
            };

            Generator.Emit(opCode, node.Id);
        }
        else if (CurrentScope is ClassVariableScope)
        {
            var opCode = ctx switch
            {
                ExprContextType.Load => OpCode.LoadName,
                ExprContextType.Store => OpCode.StoreName,
                ExprContextType.Del => OpCode.DeleteName,

                _ => throw new InvalidOperationException()
            };

            Generator.Emit(opCode, node.Id);
        }

        else if (CurrentScope is CallableVariableScope callableVariableScope)
        {
            var opCode = ctx switch
            {
                ExprContextType.Load => OpCode.LoadFast,
                ExprContextType.Store => OpCode.StoreFast,
                ExprContextType.Del => OpCode.DeleteFast,

                _ => throw new InvalidOperationException()
            };

            // TODO: DEREF

            var nameIndex = callableVariableScope.LocalsTable[node.Id];
            Generator.Emit(opCode, nameIndex);
        }
    }

    private void CompileCall(CallNode node)
    {
        if (node.Keywords.Length > 0)
            throw new NotImplementedException();

        LoadExpr(node.Func);

        foreach (var arg in node.Args)
            LoadExpr(arg);

        Generator.Emit(OpCode.Call, node.Args.Length);
    }

    private void CompileBinOp(BinOpNode node)
    {
        LoadExpr(node.Left);
        LoadExpr(node.Right);
        Generator.Emit(OpCode.BinaryOp, (int)node.Operator);
    }

    private void CompileUnaryOp(UnaryOpNode node)
    {
        LoadExpr(node.Operand);
        Generator.Emit(OpCode._UnaryOp, (int)node.Op);
    }
}
