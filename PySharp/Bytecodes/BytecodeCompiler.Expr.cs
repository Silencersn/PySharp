using PySharp.AstNodes;
using PySharp.PyModules.Builtins;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
            case CompareNode n: CompileCompare(n); break;
            case AttributeNode n: CompileAttribute(n, ctx); break;
            case ListNode n: CompileList(n); break;
            case TupleNode n: CompileTuple(n); break;
            case SetNode n: CompileSet(n); break;
            case DictNode n: CompileDict(n); break;
            default: throw new NotImplementedException();
        }
    }

    private void CompileConstant(ConstantNode node)
    {
        Generator.Emit(OpCode.LoadConst, node.Value);
    }

    private void CompileName(NameNode node, ExprContextType ctx)
    {
        if (VariableScope is RootVariableScope)
        {
            AsGlobal();
        }
        else if (VariableScope is ClassVariableScope)
        {
            AsName();
        }

        else if (VariableScope is CallableVariableScope callableVariableScope)
        {
            if (callableVariableScope.LocalsTable.TryGetValue(node.Id, out var nameIndex))
                AsFast(nameIndex);
            else if (callableVariableScope.CellVars.Contains(node.Id) || callableVariableScope.FreeVars.Contains(node.Id))
                AsDeref();
            else
                AsGlobal();
        }

        void AsGlobal()
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

        void AsName()
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

        void AsFast(int nameIndex)
        {
            var opCode = ctx switch
            {
                ExprContextType.Load => OpCode.LoadFast,
                ExprContextType.Store => OpCode.StoreFast,
                ExprContextType.Del => OpCode.DeleteFast,

                _ => throw new InvalidOperationException()
            };

            Generator.Emit(opCode, nameIndex);
        }

        void AsDeref()
        {
            var opCode = ctx switch
            {
                ExprContextType.Load => OpCode.LoadDeref,
                ExprContextType.Store => OpCode.StoreDeref,
                ExprContextType.Del => OpCode.DeleteDeref,

                _ => throw new InvalidOperationException()
            };

            Generator.Emit(opCode, node.Id);
        }
    }

    private void CompileCall(CallNode node)
    {
        LoadExpr(node.Func);

        foreach (var arg in node.Args)
            LoadExpr(arg);

        if (node.Keywords.Length is 0)
        {
            Generator.Emit(OpCode.Call, node.Args.Length);
            return;
        }

        foreach (var kwarg in node.Keywords)
            LoadExpr(kwarg.Value);

        var tuple = PyTupleObject.CreateTuple(node.Keywords.Select(k => PyStrObject.FromString(k.Arg ?? throw new NotImplementedException("unpack"))));
        Generator.Emit(OpCode.LoadConst, tuple);

        Generator.Emit(OpCode.CallKw, node.Args.Length + node.Keywords.Length);
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

    private void CompileCompare(CompareNode node)
    {
        if (node.Comparators.Length is 1)
            CompileCompareFor2(node);
        else
            CompileCompareForN(node);

        void CompileCompareFor2(CompareNode node)
        {
            LoadExpr(node.Left);
            LoadExpr(node.Comparators[0]);
            EmitOp(node.Ops[0]);
        }

        void CompileCompareForN(CompareNode node)
        {
            var fastEndLabel = Generator.DefineLabel();
            var endLabel = Generator.DefineLabel();

            LoadExpr(node.Left);

            for (int i = 0; i < node.Comparators.Length - 1; i++)
            {
                // [a]
                LoadExpr(node.Comparators[i]); // -> [a, b]
                Generator.Emit(OpCode.Swap, 2); // -> [b, a]
                Generator.Emit(OpCode.Copy, 2); // -> [b, a, b]
                EmitOp(node.Ops[i]); // -> [b, a op b]
                Generator.Emit(OpCode.Copy, 1); // -> [b, a op b, a op b]
                Generator.Emit(OpCode.ToBool); // -> [b, a op b, bool(a op b)]
                Generator.Emit(OpCode.PopJumpIfFalse, fastEndLabel); // -> [b, a op b]
                Generator.Emit(OpCode.PopTop); // -> [b]
            }

            // [a]
            LoadExpr(node.Comparators[^1]); // -> [a, b]
            EmitOp(node.Ops[^1]); // -> [a op b]
            Generator.Emit(OpCode.Jump, endLabel);

            Generator.MarkLabel(fastEndLabel); // [b, a op b]
            Generator.Emit(OpCode.Swap, 2); // -> [a op b, b]
            Generator.Emit(OpCode.PopTop); // -> [a op b]

            Generator.MarkLabel(endLabel);
        }

        void EmitOp(CmpopType op)
        {
            if (op is CmpopType.Is or CmpopType.IsNot)
                Generator.Emit(OpCode.IsOp, op is CmpopType.Is ? 0 : 1);
            else if (op is CmpopType.In or CmpopType.NotIn)
                Generator.Emit(OpCode.ContainsOp, op is CmpopType.In ? 0 : 1);
            else
                Generator.Emit(OpCode.CompareOp, (int)op);
        }
    }

    private void CompileAttribute(AttributeNode node, ExprContextType ctx)
    {
        LoadExpr(node.Value);

        if (ctx is ExprContextType.Load)
            Generator.Emit(OpCode.LoadAttr, node.Identifier);
        else if (ctx is ExprContextType.Store)
            Generator.Emit(OpCode.StoreAttr, node.Identifier);
        else if (ctx is ExprContextType.Del)
            Generator.Emit(OpCode.DeleteAttr, node.Identifier);
        else
            throw new UnreachableException();
    }

    private void CompileList(ListNode node)
    {
        // TODO: star expr
        foreach (var elt in node.Elts)
            LoadExpr(elt);
        Generator.Emit(OpCode.BuildList, node.Elts.Length);
    }

    private void CompileTuple(TupleNode node)
    {
        // TODO: star expr
        foreach (var elt in node.Elts)
            LoadExpr(elt);
        Generator.Emit(OpCode.BuildTuple, node.Elts.Length);
    }

    private void CompileSet(SetNode node)
    {
        // TODO: star expr
        foreach (var elt in node.Elts)
            LoadExpr(elt);
        Generator.Emit(OpCode.BuildSet, node.Elts.Length);
    }

    private void CompileDict(DictNode node)
    {
        for (int i = 0; i < node.Keys.Length; i++)
        {
            LoadExpr(node.Keys[i] ?? throw new NotSupportedException("unpack"));
            LoadExpr(node.Values[i]);
        }
        Generator.Emit(OpCode.BuildMap, node.Keys.Length);
    }
}
