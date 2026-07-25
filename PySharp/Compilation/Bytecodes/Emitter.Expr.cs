using PySharp.Compilation.AstNodes;
using PySharp.Compilation.Bytecodes.Extensions;
using PySharp.Compilation.Primitives;
using PySharp.Modules.Builtins;
using System.Collections.Immutable;
using System.Diagnostics;

namespace PySharp.Compilation.Bytecodes;

partial class Emitter
{
    private void LoadName(string name) => EmitName(name, ExprContextType.Load);
    private void StoreName(string name) => EmitName(name, ExprContextType.Store);
    private void DeleteName(string name) => EmitName(name, ExprContextType.Del);
    private void LoadExpr(AstExprNode node) => EmitExpr(node, ExprContextType.Load);
    private void StoreExpr(AstExprNode node) => EmitExpr(node, ExprContextType.Store);
    private void DeleteExpr(AstExprNode node) => EmitExpr(node, ExprContextType.Del);

    private void EmitExpr(AstExprNode node, ExprContextType ctx)
    {
        node = Reducer.Fold(node);

        Builder.PushMetaInfo(node.MetaInfo);
        switch (node)
        {
            case ConstantNode n: EmitConstant(n); break;
            case NameNode n: EmitName(n, ctx); break;
            case CallNode n: EmitCall(n); break;
            case BinOpNode n: EmitBinOp(n); break;
            case UnaryOpNode n: EmitUnaryOp(n); break;
            case CompareNode n: EmitCompare(n); break;
            case AttributeNode n: EmitAttribute(n, ctx); break;
            case ListNode n: EmitList(n, ctx); break;
            case TupleNode n: EmitTuple(n, ctx); break;
            case SetNode n: EmitSet(n); break;
            case DictNode n: EmitDict(n); break;
            case ListCompNode n: EmitListComp(n); break;
            case SetCompNode n: EmitSetComp(n); break;
            case DictCompNode n: EmitDictComp(n); break;
            case GeneratorExpNode n: EmitGeneratorExp(n); break;
            case YieldNode n: EmitYield(n); break;
            case YieldFromNode n: EmitYieldFrom(n); break;
            case NamedExprNode n: EmitNamedExpr(n); break;
            case SubscriptNode n: EmitSubscript(n, ctx); break;
            case SliceNode n: EmitSlice(n); break;
            case IfExpNode n: EmitIfExp(n); break;
            case LambdaNode n: EmitLambda(n); break;
            case FormattedValueNode n: EmitFormattedValue(n); break;
            case JoinedStrNode n: EmitJoinedStr(n); break;
            case InterpolationNode n: EmitInterpolation(n); break;
            case TemplateStrNode n: EmitTemplateStr(n); break;
            case BoolOpNode n: EmitBoolOp(n); break;
            case StarredNode n: EmitStarred(n, ctx); break;
            case AwaitNode n: EmitAwait(n); break;
            default: throw new UnreachableException();
        }
        Builder.PopMetaInfo();
    }

    private void EmitConstant(ConstantNode node)
    {
        Builder.Emit(OpCode.LoadConst, node.Value);
    }

    private void EmitName(NameNode node, ExprContextType ctx)
    {
        EmitName(node.Id, ctx);
    }

    private void EmitName(string name, ExprContextType ctx)
    {
        if (VariableScope is RootVariableScope)
        {
            if (OnlyAsName)
                AsName();
            else
                AsGlobal();
        }
        else if (VariableScope is GenericParamVariableScope genericParamScope)
        {
            // GenericParamScope is like a function scope with locals/cells
            if (!genericParamScope.LocalsTable.TryGetValue(name, out var nameIndex))
                AsGlobal();
            else if (genericParamScope.Variables[name] is PyVariableType.CapturedLocal)
                AsDerefFast(nameIndex);
            else
                AsFast(nameIndex);
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

            Builder.Emit(opCode, name);
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

            Builder.Emit(opCode, name);
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

            Builder.Emit(opCode, nameIndex);
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

            Builder.Emit(opCode, name);
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

            Builder.Emit(opCode, nameIndex);
        }
    }

    private void EmitCall(CallNode node)
    {
        var callMethod = false;
        var hasStarred = node.Args.Any(static arg => arg is StarredNode)
            || node.Keywords.Any(static kwarg => kwarg.Arg is null);

        if (!hasStarred && node.Func is AttributeNode attributeNode)
        {
            // a.b()

            callMethod = true;
            LoadExpr(attributeNode.Value);
            Builder.Emit(OpCode.LoadMethod, attributeNode.Identifier);
        }
        else
        {
            LoadExpr(node.Func);
        }

        if (!hasStarred)
            EmitCallOrCallKw();
        else
            EmitCallFunctionEx();

        void EmitCallOrCallKw()
        {
            foreach (var arg in node.Args)
                LoadExpr(arg);

            var argsLength = node.Args.Length;
            if (callMethod)
                argsLength++;

            if (node.Keywords.Length is 0)
            {
                Builder.Emit(OpCode.Call, argsLength);
                return;
            }

            foreach (var kwarg in node.Keywords)
                LoadExpr(kwarg.Value);

            var tuple = PyTupleObject.CreateTuple(node.Keywords.Select(k => PyStrObject.FromString(k.Arg ?? throw new UnreachableException())));
            Builder.Emit(OpCode.LoadConst, tuple);

            Builder.Emit(OpCode.CallKw, argsLength + node.Keywords.Length);
        }

        void EmitCallFunctionEx()
        {
            Builder.Emit(OpCode.BuildList, 0);
            foreach (var arg in node.Args)
            {
                LoadExpr(arg);
                Builder.Emit(arg is StarredNode ? OpCode.ListExtend : OpCode.ListAppend, 1);
            }

            var nonStarredCount = 0;

            foreach (var kwarg in node.Keywords)
            {
                if (kwarg.Arg is null)
                    continue;

                nonStarredCount++;
                Builder.Emit(OpCode.LoadConst, PyStrObject.FromString(kwarg.Arg));
                LoadExpr(kwarg.Value);
            }
            Builder.Emit(OpCode.BuildMap, nonStarredCount);

            foreach (var kwarg in node.Keywords)
            {
                if (kwarg.Arg is not null)
                    continue;

                LoadExpr(kwarg.Value);
                Builder.Emit(OpCode.DictMerge, 1);
            }

            Builder.Emit(OpCode.CallFunctionEx);
        }
    }

    private void EmitBinOp(BinOpNode node)
    {
        LoadExpr(node.Left);
        LoadExpr(node.Right);
        Builder.Emit(OpCode.BinaryOp, (int)node.Operator);
    }

    private void EmitUnaryOp(UnaryOpNode node)
    {
        LoadExpr(node.Operand);
        if (node.Op is UnaryOpType.Not)
        {
            Builder.Emit(OpCode.ToBool);
            Builder.Emit(OpCode.UnaryNot);
        }
        else
        {
            Builder.Emit(OpCode._UnaryOp, (int)node.Op);
        }
    }

    private void EmitCompare(CompareNode node)
    {
        if (node.Comparators.Length is 1)
            EmitCompareFor2(node);
        else
            EmitCompareForN(node);

        void EmitCompareFor2(CompareNode node)
        {
            LoadExpr(node.Left);
            LoadExpr(node.Comparators[0]);
            EmitOp(node.Ops[0]);
        }

        void EmitCompareForN(CompareNode node)
        {
            var fastEndLabel = Builder.DefineLabel();
            var endLabel = Builder.DefineLabel();

            LoadExpr(node.Left);

            for (int i = 0; i < node.Comparators.Length - 1; i++)
            {
                // [a]
                LoadExpr(node.Comparators[i]); // -> [a, b]
                Builder.Emit(OpCode.Swap, 2); // -> [b, a]
                Builder.Emit(OpCode.Copy, 2); // -> [b, a, b]
                EmitOp(node.Ops[i]); // -> [b, a op b]
                Builder.Emit(OpCode.Copy, 1); // -> [b, a op b, a op b]
                Builder.Emit(OpCode.ToBool); // -> [b, a op b, bool(a op b)]
                Builder.PopJumpIfFalse(fastEndLabel); // -> [b, a op b]
                Builder.Emit(OpCode.PopTop); // -> [b]
            }

            // [a]
            LoadExpr(node.Comparators[^1]); // -> [a, b]
            EmitOp(node.Ops[^1]); // -> [a op b]
            Builder.Jump(endLabel);

            Builder.MarkLabel(fastEndLabel); // [b, a op b]
            Builder.Emit(OpCode.Swap, 2); // -> [a op b, b]
            Builder.Emit(OpCode.PopTop); // -> [a op b]

            Builder.MarkLabel(endLabel);
        }

        void EmitOp(CmpopType op)
        {
            if (op is CmpopType.Is or CmpopType.IsNot)
                Builder.Emit(OpCode.IsOp, op is CmpopType.Is ? 0 : 1);
            else if (op is CmpopType.In or CmpopType.NotIn)
                Builder.Emit(OpCode.ContainsOp, op is CmpopType.In ? 0 : 1);
            else
                Builder.Emit(OpCode.CompareOp, (int)op);
        }
    }

    private void EmitAttribute(AttributeNode node, ExprContextType ctx)
    {
        LoadExpr(node.Value);

        if (ctx is ExprContextType.Load)
            Builder.Emit(OpCode.LoadAttr, node.Identifier);
        else if (ctx is ExprContextType.Store)
            Builder.Emit(OpCode.StoreAttr, node.Identifier);
        else if (ctx is ExprContextType.Del)
            Builder.Emit(OpCode.DeleteAttr, node.Identifier);
        else
            throw new UnreachableException();
    }

    private void InternalEmitElts(ImmutableArray<AstExprNode> elts, ExprContextType ctx, out bool unpackWhenLoad)
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
                Builder.Emit(OpCode.BuildList, 0);
                foreach (var elt in elts)
                {
                    LoadExpr(elt);
                    Builder.Emit(elt is StarredNode ? OpCode.ListExtend : OpCode.ListAppend, 1);
                }
            }
        }
        else if (ctx is ExprContextType.Store)
        {
            var (index, starred) = elts.Index().FirstOrDefault(static item => item.Item is StarredNode);
            if (starred is null)
                Builder.Emit(OpCode.UnpackSequence, elts.Length);
            else
                Builder.Emit(OpCode.UnpackEx, (index << 16) | (elts.Length - index - 1));
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

    private void EmitList(ListNode node, ExprContextType ctx)
    {
        InternalEmitElts(node.Elts, ctx, out var unpackWhenLoad);

        // if unpackWhenLoad, Stack[-1] is already a list
        if (ctx is ExprContextType.Load && !unpackWhenLoad)
            Builder.Emit(OpCode.BuildList, node.Elts.Length);
    }

    private void EmitTuple(TupleNode node, ExprContextType ctx)
    {
        InternalEmitElts(node.Elts, ctx, out var unpackWhenLoad);

        if (ctx is ExprContextType.Load)
        {
            if (unpackWhenLoad)
                Builder.Emit(OpCode._ListToTuple);
            else
                Builder.Emit(OpCode.BuildTuple, node.Elts.Length);
        }
    }

    private void EmitSet(SetNode node)
    {
        InternalEmitElts(node.Elts, ExprContextType.Load, out var unpackWhenLoad);

        if (unpackWhenLoad)
            Builder.Emit(OpCode._ListToSet);
        else
            Builder.Emit(OpCode.BuildSet, node.Elts.Length);
    }

    private void EmitDict(DictNode node)
    {
        if (node.Keys.All(static key => key is not null))
        {
            for (int i = 0; i < node.Keys.Length; i++)
            {
                LoadExpr(node.Keys[i]!);
                LoadExpr(node.Values[i]);
            }
            Builder.Emit(OpCode.BuildMap, node.Keys.Length);
            return;
        }

        Builder.Emit(OpCode.BuildMap, 0);
        for (int i = 0; i < node.Keys.Length; i++)
        {
            var key = node.Keys[i];
            var value = node.Values[i];

            if (key is not null)
            {
                LoadExpr(key);
                LoadExpr(value);
                Builder.Emit(OpCode.MapAdd, 1);
            }
            else
            {
                LoadExpr(value);
                Builder.Emit(OpCode.DictUpdate, 1);
            }
        }
    }

    private void InternalEmitGenerators(ImmutableArray<AstComprehensionNode> generators, Action emitElt, bool isGeneratorExp = false)
    {
        EmitGenerator(0);

        void EmitGenerator(int i)
        {
            if (i == generators.Length)
            {
                emitElt();
                return;
            }

            var generator = generators[i];

            if (generator.IsAsync)
                EmitAsyncGenerator(i);
            else
                EmitSyncGenerator(i);
        }

        void EmitSyncGenerator(int i)
        {
            var forIterLabel = Builder.DefineLabel();
            var endForLabel = Builder.DefineLabel();

            var generator = generators[i];

            if (i is 0 && isGeneratorExp)
            {
                LoadName(".0");
            }
            else
            {
                LoadExpr(generator.Iter);
                Builder.Emit(OpCode.GetIter);
            }
            Builder.MarkLabel(forIterLabel);
            Builder.Emit(OpCode.ForIter, endForLabel);
            StoreExpr(generator.Target);

            foreach (var test in generator.Ifs)
            {
                LoadExpr(test);
                Builder.Emit(OpCode.ToBool);
                Builder.PopJumpIfFalse(forIterLabel);
            }

            EmitGenerator(i + 1);

            Builder.Jump(forIterLabel);

            Builder.MarkLabel(endForLabel);
            Builder.Emit(OpCode.PopIter);
        }

        void EmitAsyncGenerator(int i)
        {
            var forIterLabel = Builder.DefineLabel();
            var endForLabel = Builder.DefineLabel();
            var exceptLabel = Builder.DefineLabel();
            var cleanupLabel = Builder.DefineLabel();
            var afterStopLabel = Builder.DefineLabel();

            var generator = generators[i];

            if (i is 0 && isGeneratorExp)
            {
                LoadName(".0");
            }
            else
            {
                LoadExpr(generator.Iter);
                Builder.Emit(OpCode.GetAIter);
            }
            Builder.MarkLabel(forIterLabel);
            Builder.Emit(OpCode.GetANext);
            Builder.Emit(OpCode._SetupFinally, cleanupLabel);
            Builder.Emit(OpCode._SetupExcept, exceptLabel);
            Builder.Emit(OpCode.LoadConst, PyNoneObject.None);

            var sendLabel = Builder.DefineLabel();
            var afterAwaitLabel = Builder.DefineLabel();
            Builder.MarkLabel(sendLabel);
            Builder.Emit(OpCode.Send, afterAwaitLabel);
            Builder.Emit(OpCode.YieldValue);
            Builder.Jump(sendLabel);

            Builder.MarkLabel(afterAwaitLabel);
            Builder.Emit(OpCode.Swap, 2);
            Builder.Emit(OpCode.PopTop);
            StoreExpr(generator.Target);

            foreach (var test in generator.Ifs)
            {
                LoadExpr(test);
                Builder.Emit(OpCode.ToBool);
                Builder.PopJumpIfFalse(forIterLabel);
            }

            EmitGenerator(i + 1);
            Builder.Jump(cleanupLabel);

            Builder.MarkLabel(exceptLabel);
            Builder.Emit(OpCode.LoadConst, PyStopAsyncIterationObjectType.Shared);
            Builder.Emit(OpCode.CheckExcMatch);
            Builder.PopJumpIfFalse(cleanupLabel);
            Builder.Emit(OpCode._PopException);
            Builder.Emit(OpCode.PopTop);
            Builder.Jump(cleanupLabel);

            Builder.MarkLabel(cleanupLabel);
            Builder.Emit(OpCode._EnterFinally);
            Builder.Emit(OpCode._LoadHitExcept);
            Builder.PopJumpIfTrue(afterStopLabel);
            Builder.Emit(OpCode._ExitFinally);
            Builder.Jump(forIterLabel);

            Builder.MarkLabel(afterStopLabel);
            Builder.Emit(OpCode._ExitFinally);

            Builder.MarkLabel(endForLabel);
            Builder.Emit(OpCode.PopIter);
        }
    }

    private void EmitListComp(ListCompNode node)
    {
        Builder.Emit(OpCode.BuildList, 0);
        Builder.Emit(OpCode._EnterInlineFrame);

        InternalEmitGenerators(node.Generators, () =>
        {
            LoadExpr(node.Elt);
            Builder.Emit(OpCode.ListAppend, node.Generators.Length + 1);
        });

        Builder.Emit(OpCode._ExitInlineFrame);
    }

    private void EmitSetComp(SetCompNode node)
    {
        Builder.Emit(OpCode.BuildSet, 0);
        Builder.Emit(OpCode._EnterInlineFrame);

        InternalEmitGenerators(node.Generators, () =>
        {
            LoadExpr(node.Elt);
            Builder.Emit(OpCode.SetAdd, node.Generators.Length + 1);
        });

        Builder.Emit(OpCode._ExitInlineFrame);
    }

    private void EmitDictComp(DictCompNode node)
    {
        Builder.Emit(OpCode.BuildMap, 0);
        Builder.Emit(OpCode._EnterInlineFrame);

        InternalEmitGenerators(node.Generators, () =>
        {
            LoadExpr(node.Key);
            LoadExpr(node.Value);
            Builder.Emit(OpCode.MapAdd, node.Generators.Length + 1);
        });

        Builder.Emit(OpCode._ExitInlineFrame);
    }

    private void EmitYield(YieldNode node)
    {
        if (node.Value is not null)
            LoadExpr(node.Value);
        else
            Builder.Emit(OpCode.LoadConst, PyNoneObject.None);
        Builder.Emit(OpCode.YieldValue);
        Builder.Emit(OpCode._CheckExcToRaise);
    }

    private void InternalEmitYieldFromOrAwait(AstExprNode nodeValue, bool isAwait)
    {
        var sendLabel = Builder.DefineLabel();
        var endSendLabel = Builder.DefineLabel();

        LoadExpr(nodeValue);
        Builder.Emit(isAwait ? OpCode.GetAwaitable : OpCode.GetYieldFromIter);
        Builder.Emit(OpCode.LoadConst, PyNoneObject.None); // activate the iter
        Builder.MarkLabel(sendLabel);
        Builder.Emit(OpCode.Send, endSendLabel);
        Builder.Emit(OpCode.YieldValue);
        Builder.Jump(sendLabel);

        Builder.MarkLabel(endSendLabel);
        Builder.Emit(OpCode.Swap, 2); // swap iter and StopIteration.value
        Builder.Emit(OpCode.PopTop); // pop iter
    }
    private void EmitYieldFrom(YieldFromNode node)
    {
        InternalEmitYieldFromOrAwait(node.Value, isAwait: false);
    }

    private void EmitGeneratorExp(GeneratorExpNode node)
    {
        var scope = Model.GetVariableScope<CallableVariableScope>(node);
        Debug.Assert(scope is not null);

        PyCodeObject codeObj;
        using (var sub = new EmitterSubScope(this, scope))
        {
            Builder.Emit(OpCode.ReturnGenerator);
            Builder.Emit(OpCode.PopTop);

            InternalEmitGenerators(node.Generators, () =>
            {
                LoadExpr(node.Elt);
                Builder.Emit(OpCode.YieldValue);
                Builder.Emit(OpCode._CheckExcToRaise);
                Builder.Emit(OpCode.PopTop);
            }, isGeneratorExp: true);

            codeObj = new PyCodeObject(_source.Name, scope, Builder.ToBytecode());
        }
        Builder.Emit(OpCode.LoadConst, PyTupleObject.Empty);
        Builder.Emit(OpCode.LoadConst, PyTupleObject.Empty);
        Builder.Emit(OpCode.LoadConst, codeObj);
        Builder.Emit(OpCode._MakeFunctionWithPyArgsDef);

        LoadExpr(node.Generators[0].Iter);
        if (node.Generators[0].IsAsync)
            Builder.Emit(OpCode.GetAIter);
        else
            Builder.Emit(OpCode.GetIter);

        Builder.Emit(OpCode.Call, 1);
    }

    private void EmitNamedExpr(NamedExprNode node)
    {
        LoadExpr(node.Value);
        Builder.Emit(OpCode.Copy, 1);

        var name = node.Target.Id;
        if (VariableScope is CallableVariableScope scope && (scope.CellVars.Contains(name) || scope.FreeVars.Contains(name)))
            Builder.Emit(OpCode._StoreDerefIncludedNonInlineFrame, name);
        else
            Builder.Emit(OpCode._StoreNameIncludedNonInlineFrame, name);
    }

    private void EmitSubscript(SubscriptNode node, ExprContextType ctx)
    {
        LoadExpr(node.Value);
        LoadExpr(node.Slice);

        if (ctx is ExprContextType.Load)
            Builder.Emit(OpCode.BinarySubscr);
        else if (ctx is ExprContextType.Store)
            Builder.Emit(OpCode.StoreSubscr);
        else if (ctx is ExprContextType.Del)
            Builder.Emit(OpCode.DeleteSubscr);
        else
            throw new UnreachableException();
    }

    private void EmitIfExp(IfExpNode node)
    {
        var endLabel = Builder.DefineLabel();
        var elseLabel = Builder.DefineLabel();

        LoadExpr(node.Test);
        Builder.Emit(OpCode.ToBool);
        Builder.PopJumpIfFalse(elseLabel);

        LoadExpr(node.Body);
        Builder.Jump(endLabel);

        Builder.MarkLabel(elseLabel);
        LoadExpr(node.OrElse);

        Builder.MarkLabel(endLabel);
    }

    private void EmitLambda(LambdaNode node)
    {
        var scope = Model.GetVariableScope<CallableVariableScope>(node);
        Debug.Assert(scope is not null);

        PyCodeObject codeObj;
        using (var sub = new EmitterSubScope(this, scope))
        {
            if (scope.IsGenerator)
            {
                Builder.Emit(OpCode.ReturnGenerator);
                Builder.Emit(OpCode.PopTop);
            }
            LoadExpr(node.Body);
            Builder.Emit(OpCode.ReturnValue);

            codeObj = new PyCodeObject(_source.Name, scope, Builder.ToBytecode());
        }

        EmitFunctionDefaults(node.Args);
        Builder.Emit(OpCode.LoadConst, codeObj);
        Builder.Emit(OpCode._MakeFunctionWithPyArgsDef);
    }

    private void EmitJoinedStr(JoinedStrNode node)
    {
        foreach (var value in node.Values)
            LoadExpr(value);
        Builder.Emit(OpCode.BuildString, node.Values.Length);
    }

    private void EmitFormattedValue(FormattedValueNode node)
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
            Builder.Emit(OpCode.ConvertValue, arg);
        }

        if (node.FormatSpec is not null)
        {
            LoadExpr(node.FormatSpec);
            Builder.Emit(OpCode.FormatWithSpec);
        }
        else
        {
            Builder.Emit(OpCode.FormatSimple);
        }
    }

    private void EmitBoolOp(BoolOpNode node)
    {
        var endLabel = Builder.DefineLabel();

        var isAnd = node.Op is BoolOpType.And;
        for (int i = 0; i < node.Values.Length - 1; i++)
        {
            LoadExpr(node.Values[i]);
            Builder.Emit(OpCode.Copy, 1);
            Builder.Emit(OpCode.ToBool);
            Builder.Emit(isAnd ? OpCode.PopJumpIfFalse : OpCode.PopJumpIfTrue, endLabel);
            Builder.Emit(OpCode.PopTop);
        }
        LoadExpr(node.Values[^1]);

        Builder.MarkLabel(endLabel);
    }

    private void EmitStarred(StarredNode node, ExprContextType ctx)
    {
        if (ctx is ExprContextType.Load)
            LoadExpr(node.Value);
        else if (ctx is ExprContextType.Store)
            StoreExpr(node.Value);
        else
            throw new UnreachableException();
    }

    private void EmitSlice(SliceNode node)
    {
        if (node.Lower is null)
            Builder.Emit(OpCode.LoadConst, PyNoneObject.None);
        else
            LoadExpr(node.Lower);

        if (node.Upper is null)
            Builder.Emit(OpCode.LoadConst, PyNoneObject.None);
        else
            LoadExpr(node.Upper);

        if (node.Step is not null)
            LoadExpr(node.Step);

        Builder.Emit(OpCode.BuildSlice, node.Step is not null ? 3 : 2);
    }

    private void EmitAwait(AwaitNode node)
    {
        InternalEmitYieldFromOrAwait(node.Value, isAwait: true);
    }

    private void EmitInterpolation(InterpolationNode node)
    {
        int conversion = node.Conversion switch
        {
            -1 => 0,
            's' => 1,
            'r' => 2,
            'a' => 3,
            _ => throw new UnreachableException()
        };

        int arg = 2 | conversion << 2;

        LoadExpr(node.Value);
        Builder.Emit(OpCode.LoadConst, PyStrObject.FromString(node.Str));

        if (node.FormatSpec is not null)
        {
            LoadExpr(node.FormatSpec);
            arg++;
        }

        Builder.Emit(OpCode.BuildInterpolation, arg);
    }

    private void EmitTemplateStr(TemplateStrNode node)
    {
        var strings = new List<PyStrObject>(node.Values.Length / 2 + 1 /* inaccurate capacity */);
        var interpolations = new List<InterpolationNode>(node.Values.Length / 2 /* inaccurate capacity */);

        var needString = true;
        foreach (var value in node.Values)
        {
            if (value is ConstantNode c)
            {
                if (needString)
                {
                    strings.Add((PyStrObject)c.Value);
                    needString = false;
                }
                else
                {
                    var last = strings[^1];
                    strings[^1] = PyStrObject.FromString(last.Value + ((PyStrObject)c.Value).Value);
                }
            }
            else if (value is InterpolationNode i)
            {
                if (needString)
                    strings.Add(PyStrObject.Empty);
                interpolations.Add(i);
                needString = true;
            }
            else
            {
                throw new UnreachableException();
            }
        }

        if (needString)
            strings.Add(PyStrObject.Empty);

        Builder.Emit(OpCode.LoadConst, PyTupleObject.CreateTuple(strings));

        foreach (var interp in interpolations)
            LoadExpr(interp);

        Builder.Emit(OpCode.BuildTuple, interpolations.Count);
        Builder.Emit(OpCode.BuildTemplate);
    }
}
