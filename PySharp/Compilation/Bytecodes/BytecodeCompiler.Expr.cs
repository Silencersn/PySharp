using PySharp.Compilation.AstNodes;
using PySharp.Compilation.Bytecodes.Extensions;
using PySharp.Compilation.Primitives;
using PySharp.Modules.Builtins;
using System.Collections.Immutable;
using System.Diagnostics;

namespace PySharp.Compilation.Bytecodes;

partial class BytecodeCompiler
{
    private void LoadName(string name) => CompileName(name, ExprContextType.Load);
    private void StoreName(string name) => CompileName(name, ExprContextType.Store);
    private void DeleteName(string name) => CompileName(name, ExprContextType.Del);
    private void LoadExpr(AstExprNode node) => CompileExpr(node, ExprContextType.Load);
    private void StoreExpr(AstExprNode node) => CompileExpr(node, ExprContextType.Store);
    private void DeleteExpr(AstExprNode node) => CompileExpr(node, ExprContextType.Del);

    private void CompileExpr(AstExprNode node, ExprContextType ctx)
    {
        node = Reducer.Fold(node);

        Generator.PushMetaInfo(node.MetaInfo);
        switch (node)
        {
            case ConstantNode n: CompileConstant(n); break;
            case NameNode n: CompileName(n, ctx); break;
            case CallNode n: CompileCall(n); break;
            case BinOpNode n: CompileBinOp(n); break;
            case UnaryOpNode n: CompileUnaryOp(n); break;
            case CompareNode n: CompileCompare(n); break;
            case AttributeNode n: CompileAttribute(n, ctx); break;
            case ListNode n: CompileList(n, ctx); break;
            case TupleNode n: CompileTuple(n, ctx); break;
            case SetNode n: CompileSet(n); break;
            case DictNode n: CompileDict(n); break;
            case ListCompNode n: CompileListComp(n); break;
            case SetCompNode n: CompileSetComp(n); break;
            case DictCompNode n: CompileDictComp(n); break;
            case GeneratorExpNode n: CompileGeneratorExp(n); break;
            case YieldNode n: CompileYield(n); break;
            case YieldFromNode n: CompileYieldFrom(n); break;
            case NamedExprNode n: CompileNamedExpr(n); break;
            case SubscriptNode n: CompileSubscript(n, ctx); break;
            case SliceNode n: CompileSlice(n); break;
            case IfExpNode n: CompileIfExp(n); break;
            case LambdaNode n: CompileLambda(n); break;
            case FormattedValueNode n: CompileFormattedValue(n); break;
            case JoinedStrNode n: CompileJoinedStr(n); break;
            case BoolOpNode n: CompileBoolOp(n); break;
            case StarredNode n: CompileStarred(n, ctx); break;
            default: throw new NotImplementedException();
        }
        Generator.PopMetaInfo();
    }

    private void CompileConstant(ConstantNode node)
    {
        Generator.Emit(OpCode.LoadConst, node.Value);
    }

    private void CompileName(NameNode node, ExprContextType ctx)
    {
        CompileName(node.Id, ctx);
    }

    private void CompileName(string name, ExprContextType ctx)
    {
        if (VariableScope is RootVariableScope)
        {
            if (OnlyAsName)
                AsName();
            else
                AsGlobal();
        }
        else if (VariableScope is ClassVariableScope classVariableScope)
        {
            if (classVariableScope.Variables.TryGetValue(name, out var type) && type is PyVariableType.Closure)
                AsDeref();
            else
                AsName();
        }
        else if (VariableScope is CallableVariableScope callableVariableScope)
        {
            if (!callableVariableScope.LocalsTable.TryGetValue(name, out var nameIndex))
            {
                AsGlobal();
            }
            else
            {
                if (callableVariableScope.Variables[name] is PyVariableType.Closure or PyVariableType.CapturedLocal or PyVariableType.CapturedParameter)
                    AsDerefFast(nameIndex);
                else
                    AsFast(nameIndex);
            }
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

            Generator.Emit(opCode, name);
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

            Generator.Emit(opCode, name);
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

            Generator.Emit(opCode, name);
        }

        void AsDerefFast(int nameIndex)
        {
            var opCode = ctx switch
            {
                ExprContextType.Load => OpCode._LoadDerefFast,
                ExprContextType.Store => OpCode._StoreDerefFast,
                ExprContextType.Del => OpCode._DeleteDerefFast,

                _ => throw new InvalidOperationException()
            };

            Generator.Emit(opCode, nameIndex);
        }
    }

    private void CompileCall(CallNode node)
    {
        var callMethod = false;
        var hasStarred = node.Args.Any(static arg => arg is StarredNode)
            || node.Keywords.Any(static kwarg => kwarg.Arg is null);

        if (!hasStarred && node.Func is AttributeNode attributeNode)
        {
            // a.b()

            callMethod = true;
            LoadExpr(attributeNode.Value);
            Generator.Emit(OpCode.LoadMethod, attributeNode.Identifier);
        }
        else
        {
            LoadExpr(node.Func);
        }

        if (!hasStarred)
            CompileCallOrCallKw();
        else
            CompileCallFunctionEx();

        void CompileCallOrCallKw()
        {
            foreach (var arg in node.Args)
                LoadExpr(arg);

            var argsLength = node.Args.Length;
            if (callMethod)
                argsLength++;

            if (node.Keywords.Length is 0)
            {
                Generator.Emit(OpCode.Call, argsLength);
                return;
            }

            foreach (var kwarg in node.Keywords)
                LoadExpr(kwarg.Value);

            var tuple = PyTupleObject.CreateTuple(node.Keywords.Select(k => PyStrObject.FromString(k.Arg ?? throw new UnreachableException())));
            Generator.Emit(OpCode.LoadConst, tuple);

            Generator.Emit(OpCode.CallKw, argsLength + node.Keywords.Length);
        }

        void CompileCallFunctionEx()
        {
            Generator.Emit(OpCode.BuildList, 0);
            foreach (var arg in node.Args)
            {
                LoadExpr(arg);
                Generator.Emit(arg is StarredNode ? OpCode.ListExtend : OpCode.ListAppend, 1);
            }

            var nonStarredCount = 0;

            foreach (var kwarg in node.Keywords)
            {
                if (kwarg.Arg is null)
                    continue;

                nonStarredCount++;
                Generator.Emit(OpCode.LoadConst, PyStrObject.FromString(kwarg.Arg));
                LoadExpr(kwarg.Value);
            }
            Generator.Emit(OpCode.BuildMap, nonStarredCount);

            foreach (var kwarg in node.Keywords)
            {
                if (kwarg.Arg is not null)
                    continue;

                LoadExpr(kwarg.Value);
                Generator.Emit(OpCode.DictMerge, 1);
            }

            Generator.Emit(OpCode.CallFunctionEx);
        }
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
        if (node.Op is UnaryOpType.Not)
        {
            Generator.Emit(OpCode.ToBool);
            Generator.Emit(OpCode.UnaryNot);
        }
        else
        {
            Generator.Emit(OpCode._UnaryOp, (int)node.Op);
        }
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
                Generator.PopJumpIfFalse(fastEndLabel); // -> [b, a op b]
                Generator.Emit(OpCode.PopTop); // -> [b]
            }

            // [a]
            LoadExpr(node.Comparators[^1]); // -> [a, b]
            EmitOp(node.Ops[^1]); // -> [a op b]
            Generator.Jump(endLabel);

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

    private void InternalCompileElts(ImmutableArray<AstExprNode> elts, ExprContextType ctx, out bool unpackWhenLoad)
    {
        unpackWhenLoad = false;

        if (ctx is ExprContextType.Load)
        {
            unpackWhenLoad = elts.Any(static item => item is StarredNode);

            if (!unpackWhenLoad)
            {
                foreach (var elt in elts)
                    LoadExpr(elt);
            }
            else
            {
                Generator.Emit(OpCode.BuildList, 0);
                foreach (var elt in elts)
                {
                    LoadExpr(elt);
                    Generator.Emit(elt is StarredNode ? OpCode.ListExtend : OpCode.ListAppend, 1);
                }
            }
        }
        else if (ctx is ExprContextType.Store)
        {
            var (index, starred) = elts.Index().FirstOrDefault(static item => item.Item is StarredNode);
            if (starred is null)
                Generator.Emit(OpCode.UnpackSequence, elts.Length);
            else
                Generator.Emit(OpCode.UnpackEx, (index << 16) | (elts.Length - index - 1));
            foreach (var elt in elts)
                StoreExpr(elt);
        }
        else if (ctx is ExprContextType.Del)
        {
            foreach (var elt in elts)
                DeleteExpr(elt);
        }
        else
        {
            throw new UnreachableException();
        }
    }

    private void CompileList(ListNode node, ExprContextType ctx)
    {
        InternalCompileElts(node.Elts, ctx, out var unpackWhenLoad);

        // if unpackWhenLoad, Stack[-1] is already a list
        if (ctx is ExprContextType.Load && !unpackWhenLoad)
            Generator.Emit(OpCode.BuildList, node.Elts.Length);
    }

    private void CompileTuple(TupleNode node, ExprContextType ctx)
    {
        InternalCompileElts(node.Elts, ctx, out var unpackWhenLoad);

        if (ctx is ExprContextType.Load)
        {
            if (unpackWhenLoad)
                Generator.Emit(OpCode._ListToTuple);
            else
                Generator.Emit(OpCode.BuildTuple, node.Elts.Length);
        }
    }

    private void CompileSet(SetNode node)
    {
        InternalCompileElts(node.Elts, ExprContextType.Load, out var unpackWhenLoad);

        if (unpackWhenLoad)
            Generator.Emit(OpCode._ListToSet);
        else
            Generator.Emit(OpCode.BuildSet, node.Elts.Length);
    }

    private void CompileDict(DictNode node)
    {
        if (node.Keys.All(static key => key is not null))
        {
            for (int i = 0; i < node.Keys.Length; i++)
            {
                LoadExpr(node.Keys[i]!);
                LoadExpr(node.Values[i]);
            }
            Generator.Emit(OpCode.BuildMap, node.Keys.Length);
            return;
        }

        Generator.Emit(OpCode.BuildMap, 0);
        for (int i = 0; i < node.Keys.Length; i++)
        {
            var key = node.Keys[i];
            var value = node.Values[i];

            if (key is not null)
            {
                LoadExpr(key);
                LoadExpr(value);
                Generator.Emit(OpCode.MapAdd, 1);
            }
            else
            {
                LoadExpr(value);
                Generator.Emit(OpCode.DictUpdate, 1);
            }
        }
    }

    private void InternalCompileGenerators(ImmutableArray<AstComprehensionNode> generators, Action compileElt)
    {
        CompileGenerator(0);

        void CompileGenerator(int i)
        {
            if (i == generators.Length)
            {
                compileElt();
                return;
            }

            var forIterLabel = Generator.DefineLabel();
            var endForLabel = Generator.DefineLabel();

            var generator = generators[i];

            LoadExpr(generator.Iter);
            Generator.Emit(OpCode.GetIter);
            Generator.MarkLabel(forIterLabel);
            Generator.Emit(OpCode.ForIter, endForLabel);
            StoreExpr(generator.Target);

            foreach (var test in generator.Ifs)
            {
                LoadExpr(test);
                Generator.Emit(OpCode.ToBool);
                Generator.PopJumpIfFalse(forIterLabel);
            }

            CompileGenerator(i + 1);

            Generator.Jump(forIterLabel);

            Generator.MarkLabel(endForLabel);
            Generator.Emit(OpCode.PopIter);
        }
    }

    private void CompileListComp(ListCompNode node)
    {
        Generator.Emit(OpCode.BuildList, 0);
        Generator.Emit(OpCode._EnterInlineFrame);

        InternalCompileGenerators(node.Generators, () =>
        {
            LoadExpr(node.Elt);
            Generator.Emit(OpCode.ListAppend, node.Generators.Length + 1);
        });

        Generator.Emit(OpCode._ExitInlineFrame);
    }

    private void CompileSetComp(SetCompNode node)
    {
        Generator.Emit(OpCode.BuildSet, 0);
        Generator.Emit(OpCode._EnterInlineFrame);

        InternalCompileGenerators(node.Generators, () =>
        {
            LoadExpr(node.Elt);
            Generator.Emit(OpCode.SetAdd, node.Generators.Length + 1);
        });

        Generator.Emit(OpCode._ExitInlineFrame);
    }

    private void CompileDictComp(DictCompNode node)
    {
        Generator.Emit(OpCode.BuildMap, 0);
        Generator.Emit(OpCode._EnterInlineFrame);

        InternalCompileGenerators(node.Generators, () =>
        {
            LoadExpr(node.Key);
            LoadExpr(node.Value);
            Generator.Emit(OpCode.MapAdd, node.Generators.Length + 1);
        });

        Generator.Emit(OpCode._ExitInlineFrame);
    }

    private void CompileYield(YieldNode node)
    {
        if (node.Value is not null)
            LoadExpr(node.Value);
        else
            Generator.Emit(OpCode.LoadConst, PyNoneObject.None);
        Generator.Emit(OpCode.YieldValue);
        Generator.Emit(OpCode._CheckExcToRaise);
    }

    private void CompileYieldFrom(YieldFromNode node)
    {
        var sendLabel = Generator.DefineLabel();
        var endSendLabel = Generator.DefineLabel();

        LoadExpr(node.Value);
        Generator.Emit(OpCode.GetYieldFromIter);
        Generator.Emit(OpCode.LoadConst, PyNoneObject.None); // activate the iter
        Generator.MarkLabel(sendLabel);
        Generator.Emit(OpCode.Send, endSendLabel);
        Generator.Emit(OpCode.YieldValue);
        Generator.Jump(sendLabel);

        Generator.MarkLabel(endSendLabel);
        Generator.Emit(OpCode.Swap, 2); // swap iter and StopIteration.value
        Generator.Emit(OpCode.PopTop); // pop iter
    }

    private void CompileGeneratorExp(GeneratorExpNode node)
    {
        var currentGenerator = Generator;
        Generator = BytecodeGenerator.Create(_source);

        Generator.Emit(OpCode.PopTop); // pop the first sent to activate the generator

        InternalCompileGenerators(node.Generators, () =>
        {
            LoadExpr(node.Elt);
            Generator.Emit(OpCode.YieldValue);
            Generator.Emit(OpCode._CheckExcToRaise);
            Generator.Emit(OpCode.PopTop); // no raising, pop received value
        });
        var bytecode = Generator.ToBytecode();

        Generator = currentGenerator;

        var codeObj = new PyCodeObject("<genexpr>", bytecode, CodeObjectFlags.Generator);
        Generator.Emit(OpCode.LoadConst, codeObj);
        Generator.Emit(OpCode._MakeGeneratorExp);
    }

    private void CompileNamedExpr(NamedExprNode node)
    {
        LoadExpr(node.Value);
        Generator.Emit(OpCode.Copy, 1);

        var name = node.Target.Id;
        if (VariableScope is CallableVariableScope scope && (scope.CellVars.Contains(name) || scope.FreeVars.Contains(name)))
            Generator.Emit(OpCode._StoreDerefIncludedNonInlineFrame, name);
        else
            Generator.Emit(OpCode._StoreNameIncludedNonInlineFrame, name);
    }

    private void CompileSubscript(SubscriptNode node, ExprContextType ctx)
    {
        LoadExpr(node.Value);
        LoadExpr(node.Slice);

        if (ctx is ExprContextType.Load)
            Generator.Emit(OpCode.BinarySubscr);
        else if (ctx is ExprContextType.Store)
            Generator.Emit(OpCode.StoreSubscr);
        else if (ctx is ExprContextType.Del)
            Generator.Emit(OpCode.DeleteSubscr);
        else
            throw new UnreachableException();
    }

    private void CompileIfExp(IfExpNode node)
    {
        var endLabel = Generator.DefineLabel();
        var elseLabel = Generator.DefineLabel();

        LoadExpr(node.Test);
        Generator.Emit(OpCode.ToBool);
        Generator.PopJumpIfFalse(elseLabel);

        LoadExpr(node.Body);
        Generator.Jump(endLabel);

        Generator.MarkLabel(elseLabel);
        LoadExpr(node.OrElse);

        Generator.MarkLabel(endLabel);
    }

    private void CompileLambda(LambdaNode node)
    {
        var currentGenerator = Generator;
        Generator = BytecodeGenerator.Create(_source);
        var currentScope = VariableScope;
        var scope = Model.GetVariableScope<CallableVariableScope>(node);
        Debug.Assert(scope is not null);
        VariableScope = scope;

        if (scope.HasYield)
        {
            Generator.Emit(OpCode.ReturnGenerator);
            Generator.Emit(OpCode.PopTop); // pop the first sent to activate the generator
        }
        LoadExpr(node.Body);
        Generator.Emit(OpCode.ReturnValue);

        var bytecode = Generator.ToBytecode();

        Generator = currentGenerator;
        VariableScope = currentScope;

        var codeObj = new PyCodeObject(scope, bytecode);

        foreach (var argDefault in node.Args.Defaults)
            LoadExpr(argDefault);

        foreach (var kwargDefault in node.Args.KwDefaults)
        {
            if (kwargDefault is not null)
                LoadExpr(kwargDefault);
            else
                Generator.Emit(OpCode.PushNull);
        }

        Generator.Emit(OpCode.LoadConst, codeObj);
        Generator.Emit(OpCode._MakeFunctionWithPyArgsDef);
    }

    private void CompileJoinedStr(JoinedStrNode node)
    {
        foreach (var value in node.Values)
            LoadExpr(value);
        Generator.Emit(OpCode.BuildString, node.Values.Length);
    }

    private void CompileFormattedValue(FormattedValueNode node)
    {
        LoadExpr(node.Value);

        if (node.Conversion is not -1)
        {
            var arg = node.Conversion switch
            {
                's' => 1,
                'r' => 2,
                'a' => 3,
                _ => throw new UnreachableException()
            };
            Generator.Emit(OpCode.ConvertValue, arg);
        }

        if (node.FormatSpec is not null)
        {
            LoadExpr(node.FormatSpec);
            Generator.Emit(OpCode.FormatWithSpec);
        }
        else
        {
            Generator.Emit(OpCode.FormatSimple);
        }
    }

    private void CompileBoolOp(BoolOpNode node)
    {
        var endLabel = Generator.DefineLabel();

        var isAnd = node.Op is BoolOpType.And;
        for (int i = 0; i < node.Values.Length - 1; i++)
        {
            LoadExpr(node.Values[i]);
            Generator.Emit(OpCode.Copy, 1);
            Generator.Emit(OpCode.ToBool);
            Generator.Emit(isAnd ? OpCode.PopJumpIfFalse : OpCode.PopJumpIfTrue, endLabel);
            Generator.Emit(OpCode.PopTop);
        }
        LoadExpr(node.Values[^1]);

        Generator.MarkLabel(endLabel);
    }

    private void CompileStarred(StarredNode node, ExprContextType ctx)
    {
        if (ctx is ExprContextType.Load)
            LoadExpr(node.Value);
        else if (ctx is ExprContextType.Store)
            StoreExpr(node.Value);
        else
            throw new UnreachableException();
    }

    private void CompileSlice(SliceNode node)
    {
        if (node.Lower is null)
            Generator.Emit(OpCode.LoadConst, PyNoneObject.None);
        else
            LoadExpr(node.Lower);

        if (node.Upper is null)
            Generator.Emit(OpCode.LoadConst, PyNoneObject.None);
        else
            LoadExpr(node.Upper);

        if (node.Step is not null)
            LoadExpr(node.Step);

        Generator.Emit(OpCode.BuildSlice, node.Step is not null ? 3 : 2);
    }
}
