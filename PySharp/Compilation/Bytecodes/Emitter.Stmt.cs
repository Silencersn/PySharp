using PySharp.Compilation.AstNodes;
using PySharp.Compilation.Bytecodes.Extensions;
using PySharp.Compilation.Primitives;
using PySharp.Modules.Builtins;
using PySharp.Runtime;
using System.Collections.Immutable;
using System.Diagnostics;

namespace PySharp.Compilation.Bytecodes;

partial class Emitter
{
    private void EmitStmts(ImmutableArray<AstStmtNode> stmts, out bool isPostUnreachable)
    {
        isPostUnreachable = false;
        foreach (var stmt in stmts)
        {
            EmitStmt(stmt, out isPostUnreachable);
            if (isPostUnreachable)
                break;
        }
    }

    private void EmitStmts(ImmutableArray<AstStmtNode> stmts)
    {
        EmitStmts(stmts, out _);
    }

    private void EmitStmt(AstStmtNode node, out bool isPostUnreachable)
    {
        isPostUnreachable = false;
        Builder.PushMetaInfo(node.MetaInfo);
        switch (node)
        {
            case ExprNode n: EmitExpr(n); break;
            case PassNode n: EmitPass(n); break;
            case AssignNode n: EmitAssign(n); break;
            case AugAssignNode n: EmitAugAssign(n); break;
            case AnnAssignNode n: EmitAnnAssign(n); break;
            case DeleteNode n: EmitDelete(n); break;
            case RaiseNode n: EmitRaise(n, out isPostUnreachable); break;
            case BreakNode n: EmitBreak(n, out isPostUnreachable); break;
            case ContinueNode n: EmitContinue(n, out isPostUnreachable); break;
            case ReturnNode n: EmitReturn(n, out isPostUnreachable); break;
            case TypeAliasNode n: EmitTypeAlias(n); break;
            case ImportNode n: EmitImport(n); break;
            case ImportFromNode n: EmitImportFrom(n); break;
            case GlobalNode n: EmitGlobal(n); break;
            case NonlocalNode n: EmitNonlocal(n); break;
            case AssertNode n: EmitAssert(n); break;
            case IfNode n: EmitIf(n, out isPostUnreachable); break;
            case TryNode n: EmitTry(n); break;
            case TryStarNode n: EmitTryStar(n); break;
            case ForNode n: EmitFor(n); break;
            case AsyncForNode n: EmitAsyncFor(n); break;
            case WhileNode n: EmitWhile(n); break;
            case WithNode n: EmitWith(n); break;
            case AsyncWithNode n: EmitAsyncWith(n); break;
            case MatchNode n: EmitMatch(n); break;
            case FunctionDefNode n: EmitFunctionDef(n); break;
            case ClassDefNode n: EmitClassDef(n); break;
            case AsyncFunctionDefNode n: EmitAsyncFunctionDef(n); break;
            default: throw new UnreachableException();
        }
        Builder.PopMetaInfo();
    }

    private void EmitTypeAlias(TypeAliasNode n)
    {
        var currentBuilder = Builder;
        Builder = BytecodeBuilder.Create(_source);

        LoadExpr(n.Value);
        Builder.Emit(OpCode.ReturnValue);

        var bytecode = Builder.ToBytecode();
        Builder = currentBuilder;

        var codeObj = new PyCodeObject(n.Name, _source.Name, bytecode, CodeObjectFlags.Function);
        Builder.Emit(OpCode.LoadConst, codeObj);
        Builder.Emit(OpCode._MakeFunctionWithPyArgsDef, arg: 0);

        Builder.Emit(OpCode._MakeTypeAlias, n.Name);
        StoreName(n.Name);
    }

    private void EmitExpr(ExprNode node)
    {
        LoadExpr(node.Value);
        Builder.Emit(IsInteractive && VariableScope is RootVariableScope ? OpCode._CallPrintIfNotNone : OpCode.PopTop);
    }

    private void EmitAssign(AssignNode node)
    {
        LoadExpr(node.Value);
        for (int i = 0; i < node.Targets.Length; i++)
        {
            if (i < node.Targets.Length - 1)
                Builder.Emit(OpCode.Copy, 1);
            StoreExpr(node.Targets[i]);
        }
    }

    private void EmitAnnAssign(AnnAssignNode node)
    {
        // Store annotations as original source strings for simple names in class/module scope.
        // Non-simple targets (self.x: int, x[i]: int) and function scope are skipped
        // (matching CPython behavior; function annotations deferred to Phase 2).
        if (node.Simple && VariableScope is RootVariableScope or ClassVariableScope)
        {
            // Extract original source text of the annotation expression using its source span
            var span = node.Annotation.MetaInfo.Range;
            var annotationStr = _source.Code.GetString(span);

            // Ensure __annotations__ dict exists in the current scope's locals
            Builder.Emit(OpCode.SetupAnnotations);

            // Store annotation string: __annotations__["name"] = "annotation_source_text"
            Builder.Emit(OpCode.LoadConst, PyStrObject.FromString(annotationStr.ToString()));
            Builder.Emit(OpCode.LoadName, PySpecialNames.Annotations);
            Debug.Assert(node.Target is NameNode);
            Builder.Emit(OpCode.LoadConst, PyStrObject.FromString(((NameNode)node.Target).Id));
            Builder.Emit(OpCode.StoreSubscr);
        }

        if (node.Value is null)
            return;

        LoadExpr(node.Value);
        StoreExpr(node.Target);
    }

    private void EmitIf(IfNode node, out bool isPostUnreachable)
    {
        var test = Reducer.ToBool(node.Test);
        if (test is not null)
        {
            EmitStmts(test.Value ? node.Body : node.OrElse, out isPostUnreachable);
            return;
        }

        var elseBlockLabel = Builder.DefineLabel();
        var ifStmtEndLabel = Builder.DefineLabel();

        LoadExpr(node.Test);
        Builder.Emit(OpCode.ToBool);
        Builder.PopJumpIfFalse(elseBlockLabel);

        EmitStmts(node.Body, out var bodyPostUnreachable);
        Builder.Jump(ifStmtEndLabel);

        Builder.MarkLabel(elseBlockLabel);
        EmitStmts(node.OrElse, out var orElsePostUnreachable);

        Builder.MarkLabel(ifStmtEndLabel);

        isPostUnreachable = bodyPostUnreachable && orElsePostUnreachable;
    }

    private void EmitRaise(RaiseNode node, out bool isPostUnreachable)
    {
        isPostUnreachable = true;

        if (node.Exc is null)
        {
            Builder.Emit(OpCode.RaiseVarArgs, 0);
            return;
        }

        LoadExpr(node.Exc);
        if (node.Cause is null)
        {
            Builder.Emit(OpCode.RaiseVarArgs, 1);
            return;
        }

        LoadExpr(node.Cause);
        Builder.Emit(OpCode.RaiseVarArgs, 2);
    }

    private void EmitTry(TryNode node)
    {
        var finallyBlockLabel = Builder.DefineLabel();
        Span<Label> exceptorLabels = stackalloc Label[node.Exceptors.Length];
        for (int i = 0; i < node.Exceptors.Length; i++)
            exceptorLabels[i] = Builder.DefineLabel();
        var tryStmtEndLabel = Builder.DefineLabel();

        Builder.Emit(OpCode._SetupFinally, finallyBlockLabel);
        if (exceptorLabels.Length > 0)
            Builder.Emit(OpCode._SetupExcept, exceptorLabels[0]);

        EmitStmts(node.Body);
        EmitStmts(node.OrElse);
        Builder.MarkLabel(finallyBlockLabel);
        Builder.Emit(OpCode._EnterFinally);
        EmitStmts(node.FinalBody);
        Builder.Emit(OpCode._ExitFinally);
        Builder.Jump(tryStmtEndLabel);

        for (int i = 0; i < node.Exceptors.Length; i++)
        {
            Builder.MarkLabel(exceptorLabels[i]);

            var exceptor = node.Exceptors[i];
            if (i < node.Exceptors.Length - 1)
            {
                Debug.Assert(exceptor.Type is not null);
                LoadExpr(exceptor.Type);
                Builder.Emit(OpCode.CheckExcMatch);
                Builder.PopJumpIfFalse(exceptorLabels[i + 1]); // jump to next except

            }
            else
            {
                if (exceptor.Type is not null)
                {
                    LoadExpr(exceptor.Type);
                    Builder.Emit(OpCode.CheckExcMatch);
                    Builder.PopJumpIfFalse(finallyBlockLabel); // last exceptor, jump to finally
                }
            }

            if (exceptor.Name is not null)
            {
                Builder.Emit(OpCode._LoadExc);
                StoreName(exceptor.Name);
            }

            EmitStmts(exceptor.Body);

            if (exceptor.Name is not null)
                DeleteName(exceptor.Name);

            Builder.Emit(OpCode._PopException);
            Builder.Jump(finallyBlockLabel); // jump to finally
        }

        Builder.MarkLabel(tryStmtEndLabel);
    }

    private void EmitTryStar(TryStarNode node)
    {
        var finallyBlockLabel = Builder.DefineLabel();
        Span<Label> exceptorLabels = stackalloc Label[node.Exceptors.Length];
        for (int i = 0; i < node.Exceptors.Length; i++)
            exceptorLabels[i] = Builder.DefineLabel();
        var tryStmtEndLabel = Builder.DefineLabel();

        Builder.Emit(OpCode._SetupFinally, finallyBlockLabel);
        Debug.Assert(exceptorLabels.Length > 0);
        Builder.Emit(OpCode._SetupExcept, exceptorLabels[0]);

        EmitStmts(node.Body);
        EmitStmts(node.OrElse);
        Builder.MarkLabel(finallyBlockLabel);
        Builder.Emit(OpCode._EnterFinally);
        EmitStmts(node.FinalBody);
        Builder.Emit(OpCode._ExitFinally);
        Builder.Jump(tryStmtEndLabel);

        for (int i = 0; i < node.Exceptors.Length; i++)
        {
            Builder.MarkLabel(exceptorLabels[i]);

            var exceptor = node.Exceptors[i];
            Debug.Assert(exceptor.Type is not null);
            LoadExpr(exceptor.Type);
            Builder.Emit(OpCode.CheckEgMatch);
            var nextLabel = i < node.Exceptors.Length - 1 ? exceptorLabels[i + 1] : finallyBlockLabel;
            Builder.Emit(OpCode._CheckMatch, nextLabel); // if match None, jump to next except or finally

            if (exceptor.Name is not null)
                StoreName(exceptor.Name);
            else
                Builder.Emit(OpCode.PopTop);

            EmitStmts(exceptor.Body);

            if (exceptor.Name is not null)
                DeleteName(exceptor.Name);

            Builder.Emit(OpCode._PopExceptionAndJumpIfNull, finallyBlockLabel); // pop exc and jump to finally if rest is None
            Builder.Jump(nextLabel); // jump to next except or finally
        }

        Builder.MarkLabel(tryStmtEndLabel);
    }

    private void EmitFor(ForNode node)
    {
        var forIterLabel = Builder.DefineLabel();
        var forElseLabel = Builder.DefineLabel();
        var endForLabel = Builder.DefineLabel();
        Loops.Push((forIterLabel, endForLabel));

        LoadExpr(node.Iter);
        Builder.Emit(OpCode.GetIter);

        Builder.MarkLabel(forIterLabel);
        Builder.Emit(OpCode.ForIter, forElseLabel);
        StoreExpr(node.Target);

        EmitStmts(node.Body);
        Builder.Jump(forIterLabel);

        Builder.MarkLabel(forElseLabel);
        EmitStmts(node.OrElse);

        Builder.MarkLabel(endForLabel);
        Builder.Emit(OpCode.PopIter);

        Loops.Pop();
    }

    private void EmitBreak(BreakNode node, out bool isPostUnreachable)
    {
        isPostUnreachable = true;
        Builder.Jump(Loops.Peek().LoopEnd);
    }

    private void EmitContinue(ContinueNode node, out bool isPostUnreachable)
    {
        isPostUnreachable = true;
        Builder.Jump(Loops.Peek().LoopBegin);
    }

    private void EmitWhile(WhileNode node)
    {
        var whileBeginLabel = Builder.DefineLabel();
        var whileElseLabel = Builder.DefineLabel();
        var whileEndLabel = Builder.DefineLabel();
        Loops.Push((whileBeginLabel, whileEndLabel));

        Builder.MarkLabel(whileBeginLabel);
        LoadExpr(node.Test);
        Builder.Emit(OpCode.ToBool);
        Builder.PopJumpIfFalse(whileElseLabel);

        EmitStmts(node.Body);
        Builder.Jump(whileBeginLabel);

        Builder.MarkLabel(whileElseLabel);
        EmitStmts(node.OrElse);

        Builder.MarkLabel(whileEndLabel);

        Loops.Pop();
    }

    private void EmitPass(PassNode node)
    {
        Builder.Emit(OpCode.NoOperation);
    }

    private void InternalEmitIFunctionDef(IFunctionDefNode node)
    {
        var currentBuilder = Builder;
        Builder = BytecodeBuilder.Create(_source);
        var currentScope = VariableScope;
        var scope = Model.GetVariableScope<CallableVariableScope>((AstNode)node);
        Debug.Assert(scope is not null);
        VariableScope = scope;

        foreach (var cell in scope.CellVars)
            Builder.Emit(OpCode._MakeCellFast, scope.LocalsTable[cell]);

        if (scope.IsGenerator || scope is AsyncFunctionVariableScope)
        {
            Builder.Emit(OpCode.ReturnGenerator);
            Builder.Emit(OpCode.PopTop); // pop the first sent to activate the generator
        }
        EmitStmts(node.Body, out var bodyPostUnreachable);
        if (!bodyPostUnreachable)
        {
            Builder.Emit(OpCode.LoadConst, PyNoneObject.None);
            Builder.Emit(OpCode.ReturnValue);
        }

        var bytecode = Builder.ToBytecode();

        Builder = currentBuilder;
        VariableScope = currentScope;

        var codeObj = new PyCodeObject(_source.Name, scope, bytecode);

        foreach (var decorator in node.DecoratorList)
            LoadExpr(decorator);

        foreach (var argDefault in node.Args.Defaults)
            LoadExpr(argDefault);

        foreach (var kwargDefault in node.Args.KwDefaults)
        {
            if (kwargDefault is not null)
                LoadExpr(kwargDefault);
            else
                Builder.Emit(OpCode.PushNull);
        }

        Builder.Emit(OpCode.LoadConst, codeObj);
        Builder.Emit(OpCode._MakeFunctionWithPyArgsDef, node.Args.Defaults.Length + node.Args.KwDefaults.Length);

        if (OptimizationLevel < 2 && TryGetDoc(node.Body, out var doc))
        {
            Builder.Emit(OpCode.Copy, 1);
            Builder.Emit(OpCode.LoadConst, doc);
            Builder.Emit(OpCode.Swap, 2);
            Builder.Emit(OpCode.StoreAttr, PySpecialNames.Doc);
        }

        for (int i = 0; i < node.DecoratorList.Length; i++)
            Builder.Emit(OpCode.Call, 1);

        StoreName(node.Name);
    }

    private void EmitFunctionDef(FunctionDefNode node)
    {
        InternalEmitIFunctionDef(node);
    }

    private void EmitAsyncFunctionDef(AsyncFunctionDefNode node)
    {
        InternalEmitIFunctionDef(node);
    }

    private void EmitReturn(ReturnNode node, out bool isPostUnreachable)
    {
        isPostUnreachable = true;

        if (node.Value is not null)
            LoadExpr(node.Value);
        else
            Builder.Emit(OpCode.LoadConst, PyNoneObject.None);
        Builder.Emit(OpCode.ReturnValue);
    }

    private void EmitGlobal(GlobalNode node)
    {
        // do nothing
    }

    private void EmitNonlocal(NonlocalNode node)
    {
        // do nothing
    }

    private void EmitClassDef(ClassDefNode node)
    {
        var currentBuilder = Builder;
        Builder = BytecodeBuilder.Create(_source);
        var currentScope = VariableScope;
        var scope = Model.GetVariableScope<ClassVariableScope>(node);
        Debug.Assert(scope is not null);
        VariableScope = scope;

        if (scope.ClassCaptured)
            Builder.Emit(OpCode.MakeCell, PySpecialNames.Class);

        if (OptimizationLevel < 2 && TryGetDoc(node.Body, out var doc))
        {
            Builder.Emit(OpCode.LoadConst, doc);
            StoreName(PySpecialNames.Doc);
        }

        // For generic classes (e.g. class Foo[T]:), store the type param names as __type_params__
        // This is the compile-time equivalent of CPython's codegen_set_type_params_in_class.
        //
        // NOTE: We store string names rather than TypeVar objects (as CPython does).
        // This is intentional: TypeVar runtime objects are not yet available, and the current
        // scope (external subscript only) doesn't need them. String names are sufficient to:
        //   (a) mark the class as generic (triggers auto-injection of __class_getitem__)
        //   (b) provide basic __type_params__ metadata
        // When class-body type-parameter references are needed, this must be upgraded to
        // emit code that creates TypeVar objects (matching CPython's codegen_type_params).
        if (node.TypeParams.Length > 0)
        {
            var typeParamNames = node.TypeParams.Select(static tp => tp switch
            {
                TypeVarNode tv => tv.Name,
                ParamSpecNode ps => ps.Name,
                TypeVarTupleNode tvn => tvn.Name,
                _ => throw new UnreachableException()
            });
            var namesTuple = PyTupleObject.CreateTuple(typeParamNames.Select(PyStrObject.FromString));
            Builder.Emit(OpCode.LoadConst, namesTuple);
            StoreName(PySpecialNames.TypeParams);
        }

        EmitStmts(node.Body);

        var bytecode = Builder.ToBytecode();

        Builder = currentBuilder;
        VariableScope = currentScope;

        var codeObj = new PyCodeObject(_source.Name, scope, bytecode);

        foreach (var decorator in node.DecoratorList)
            LoadExpr(decorator);

        foreach (var baseType in node.Bases)
            LoadExpr(baseType);

        foreach (var kwarg in node.Keywords)
            LoadExpr(kwarg.Value);

        var tuple = node.Keywords.Length is 0 ? PyTupleObject.Empty : PyTupleObject.CreateTuple(node.Keywords.Select(k => PyStrObject.FromString(k.Arg ?? throw new UnreachableException())));
        Builder.Emit(OpCode.LoadConst, tuple);

        Builder.Emit(OpCode.LoadConst, codeObj);
        int arg = node.Bases.Length + node.Keywords.Length;
        Builder.Emit(OpCode._BuildClass, arg);

        for (int i = 0; i < node.DecoratorList.Length; i++)
            Builder.Emit(OpCode.Call, 1);

        StoreName(node.Name);
    }

    private void EmitAssert(AssertNode node)
    {
        if (OptimizationLevel > 0)
            return;

        var noRaisingLabel = Builder.DefineLabel();

        LoadExpr(node.Test);
        Builder.Emit(OpCode.ToBool);
        Builder.PopJumpIfTrue(noRaisingLabel);

        Builder.Emit(OpCode.LoadConst, PyAssertionErrorObjectType.Shared);

        if (node.Msg is not null)
        {
            LoadExpr(node.Msg);
            Builder.Emit(OpCode.Call, 1); // AssertionError(msg)
        }

        Builder.Emit(OpCode.RaiseVarArgs, 1);
        Builder.MarkLabel(noRaisingLabel);
    }

    private void EmitAugAssign(AugAssignNode node)
    {
        LoadExpr(node.Target);
        LoadExpr(node.Value);
        Builder.Emit(OpCode._AugAssignOp, (int)node.Op);
        StoreExpr(node.Target);
    }

    private void EmitDelete(DeleteNode node)
    {
        foreach (var target in node.Targets)
            DeleteExpr(target);
    }

    private void EmitImport(ImportNode node)
    {
        foreach (var name in node.Names)
        {
            Builder.Emit(OpCode.LoadConst, PyIntObject.Zero);
            Builder.Emit(OpCode.LoadConst, PyNoneObject.None);
            Builder.Emit(OpCode.ImportName, name.Name);

            if (name.AsName is null)
            {
                var parts = name.Name.Split('.');
                StoreName(parts[0]);
            }
            else
            {
                var parts = name.Name.Split('.');
                if (parts.Length is 1)
                {
                    StoreName(name.AsName);
                }
                else
                {
                    for (int i = 1; i < parts.Length - 1; i++)
                    {
                        // [mod]
                        Builder.Emit(OpCode.ImportFrom, parts[i]); // -> [mod, mod.submod]
                        Builder.Emit(OpCode.Swap, 2); // -> [mod.submod, mod]
                        Builder.Emit(OpCode.PopTop); // -> [mod.submod]
                    }
                    Builder.Emit(OpCode.ImportFrom, parts[^1]);
                    StoreName(name.AsName);

                    Builder.Emit(OpCode.PopTop);
                }
            }
        }
    }

    private void EmitImportFrom(ImportFromNode node)
    {
        Builder.Emit(OpCode.LoadConst, PyIntObject.FromInteger(node.Level));
        Builder.Emit(OpCode.LoadConst, PyTupleObject.CreateTuple(node.Names.Select(static alias => PyStrObject.FromString(alias.Name))));
        Builder.Emit(OpCode.ImportName, node.Module ?? string.Empty);

        if (node.IsImportStar())
        {
            Builder.Emit(OpCode._ImportAllFrom);
            return;
        }

        foreach (var name in node.Names)
        {
            Debug.Assert(name.Name is not "*");
            Builder.Emit(OpCode.ImportFrom, name.Name);
            StoreName(name.GetLocalName());
        }

        Builder.Emit(OpCode.PopTop);
    }

    [AIGenerated]
    private void EmitAsyncFor(AsyncForNode node)
    {
        var forIterLabel = Builder.DefineLabel();
        var forElseLabel = Builder.DefineLabel();
        var endForLabel = Builder.DefineLabel();
        Loops.Push((forIterLabel, endForLabel));

        LoadExpr(node.Iter);
        Builder.Emit(OpCode.GetAIter);

        Builder.MarkLabel(forIterLabel);
        // Equivalent to CPython GET_ANEXT: get __anext__ via slot, call it, wrap in awaitable
        Builder.Emit(OpCode.GetANext);

        // Wrap await in try/except to catch StopAsyncIteration
        var exceptLabel = Builder.DefineLabel();
        var cleanupLabel = Builder.DefineLabel();
        var afterStopLabel = Builder.DefineLabel();
        Builder.Emit(OpCode._SetupFinally, cleanupLabel);
        Builder.Emit(OpCode._SetupExcept, exceptLabel);

        // Await __anext__() via Send
        Builder.Emit(OpCode.LoadConst, PyNoneObject.None);

        var sendLabel = Builder.DefineLabel();
        var afterAwaitLabel = Builder.DefineLabel();
        Builder.MarkLabel(sendLabel);
        Builder.Emit(OpCode.Send, afterAwaitLabel);
        Builder.Emit(OpCode.YieldValue);
        Builder.Jump(sendLabel);

        // Normal: have a value from __anext__()
        Builder.MarkLabel(afterAwaitLabel);
        Builder.Emit(OpCode.Swap, 2);
        Builder.Emit(OpCode.PopTop);
        StoreExpr(node.Target);

        EmitStmts(node.Body);
        Builder.Jump(cleanupLabel);

        // StopAsyncIteration handler
        Builder.MarkLabel(exceptLabel);
        Builder.Emit(OpCode.LoadConst, PyStopAsyncIterationObjectType.Shared);
        Builder.Emit(OpCode.CheckExcMatch);
        Builder.PopJumpIfFalse(cleanupLabel); // not StopAsyncIteration 鈫?re-raise via cleanup
        Builder.Emit(OpCode._PopException);
        Builder.Emit(OpCode.PopTop); // pop the coroutine, leaving [iter]
        Builder.Jump(cleanupLabel);

        // Cleanup 鈥?enters handler; then branches based on HitExcept
        Builder.MarkLabel(cleanupLabel);
        Builder.Emit(OpCode._EnterFinally);
        Builder.Emit(OpCode._LoadHitExcept);
        Builder.PopJumpIfTrue(afterStopLabel);
        // HitExcept=false: normal iteration done 鈫?pop handler and loop back
        Builder.Emit(OpCode._ExitFinally);
        Builder.Jump(forIterLabel);

        // HitExcept=true: exception was caught
        Builder.MarkLabel(afterStopLabel);
        Builder.Emit(OpCode._ExitFinally);
        // If _ExitFinally re-raised (non-StopAsyncIteration), execution stops here
        // For StopAsyncIteration: PyException=null 鈫?handler popped, fall through to else

        // Else clause
        Builder.MarkLabel(forElseLabel);
        EmitStmts(node.OrElse);

        Builder.MarkLabel(endForLabel);
        Builder.Emit(OpCode.PopIter);

        Loops.Pop();
    }

    private void EmitWith(WithNode node)
    {
        EmitWithItem(0);

        void EmitWithItem(int i)
        {
            if (i == node.Items.Length)
            {
                EmitStmts(node.Body);
                return;
            }

            var finallyLabel = Builder.DefineLabel();
            var exitFinallyLabel = Builder.DefineLabel();
            var exceptLabel = Builder.DefineLabel();

            var item = node.Items[i];

            // []
            LoadExpr(item.ContextExpr); // -> [manager]
            Builder.Emit(OpCode.LoadSpecial, (int)LoadSpecialMethods.Enter); // -> [manager, enter]
            Builder.Emit(OpCode.Swap, 2); // [enter, manager]
            Builder.Emit(OpCode.LoadSpecial, (int)LoadSpecialMethods.Exit); // -> [enter, manager, exit]
            Builder.Emit(OpCode.Swap, 3); // -> [exit, manager, enter]
            Builder.Emit(OpCode.Copy, 2); // -> [exit, manager, enter, manager]
            Builder.Emit(OpCode.Call, 1); // -> [exit, manager, value]

            Builder.Emit(OpCode._SetupFinally, finallyLabel);
            Builder.Emit(OpCode._SetupExcept, exceptLabel);

            if (item.OptionalVars is not null)
                StoreExpr(item.OptionalVars);
            else
                Builder.Emit(OpCode.PopTop);
            // -> [exit, manager]

            EmitWithItem(i + 1);
            Builder.Jump(finallyLabel);

            Builder.MarkLabel(exceptLabel);
            Builder.Emit(OpCode._LoadExcInfo); // -> [exit, manager, exc_type, exc, traceback]
            Builder.Emit(OpCode.Call, 4); // -> [handled]
            Builder.Emit(OpCode.ToBool); // -> [handled_bool]
            Builder.Emit(OpCode._PopExceptionIfTrue);
            Builder.PopJumpIfTrue(finallyLabel); // -> []
            Builder.Emit(OpCode.RaiseVarArgs, 0);

            Builder.MarkLabel(finallyLabel);
            Builder.Emit(OpCode._EnterFinally);

            Builder.Emit(OpCode._LoadHitExcept);
            Builder.PopJumpIfTrue(exitFinallyLabel);

            Builder.Emit(OpCode.LoadConst, PyNoneObject.None);
            Builder.Emit(OpCode.LoadConst, PyNoneObject.None);
            Builder.Emit(OpCode.LoadConst, PyNoneObject.None);
            Builder.Emit(OpCode.Call, 4);
            Builder.Emit(OpCode.PopTop);

            Builder.MarkLabel(exitFinallyLabel);
            Builder.Emit(OpCode._ExitFinally);
        }
    }

    [AIGenerated]
    private void EmitAsyncWith(AsyncWithNode node)
    {
        EmitAsyncWithItem(0);

        void EmitAsyncWithItem(int i)
        {
            if (i == node.Items.Length)
            {
                EmitStmts(node.Body);
                return;
            }

            var finallyLabel = Builder.DefineLabel();
            var exitFinallyLabel = Builder.DefineLabel();
            var exceptLabel = Builder.DefineLabel();

            var item = node.Items[i];

            // []
            LoadExpr(item.ContextExpr); // -> [manager]
            Builder.Emit(OpCode.LoadSpecial, (int)LoadSpecialMethods.AEnter); // -> [manager, aenter]
            Builder.Emit(OpCode.Swap, 2); // [aenter, manager]
            Builder.Emit(OpCode.LoadSpecial, (int)LoadSpecialMethods.AExit); // -> [aenter, manager, aexit]
            Builder.Emit(OpCode.Swap, 3); // -> [aexit, manager, aenter]
            Builder.Emit(OpCode.Copy, 2); // -> [aexit, manager, aenter, manager]
            Builder.Emit(OpCode.Call, 1); // -> [aexit, manager, coroutine]

            // Await __aenter__() result
            Builder.Emit(OpCode.GetAwaitable);
            Builder.Emit(OpCode.LoadConst, PyNoneObject.None);

            var sendEnterLabel = Builder.DefineLabel();
            var afterEnterLabel = Builder.DefineLabel();
            Builder.MarkLabel(sendEnterLabel);
            Builder.Emit(OpCode.Send, afterEnterLabel);
            Builder.Emit(OpCode.YieldValue);
            Builder.Jump(sendEnterLabel);

            Builder.MarkLabel(afterEnterLabel);
            Builder.Emit(OpCode.Swap, 2); // swap coroutine and value
            Builder.Emit(OpCode.PopTop); // pop coroutine
            // -> [aexit, manager, value]

            Builder.Emit(OpCode._SetupFinally, finallyLabel);
            Builder.Emit(OpCode._SetupExcept, exceptLabel);

            if (item.OptionalVars is not null)
                StoreExpr(item.OptionalVars);
            else
                Builder.Emit(OpCode.PopTop);
            // -> [aexit, manager]

            EmitAsyncWithItem(i + 1);
            Builder.Jump(finallyLabel);

            Builder.MarkLabel(exceptLabel);
            Builder.Emit(OpCode._LoadExcInfo); // -> [aexit, manager, exc_type, exc, traceback]
            Builder.Emit(OpCode.Call, 4); // -> [coroutine]

            // Await __aexit__() result
            Builder.Emit(OpCode.GetAwaitable);
            Builder.Emit(OpCode.LoadConst, PyNoneObject.None);

            var sendExitLabel = Builder.DefineLabel();
            var afterExitLabel = Builder.DefineLabel();
            Builder.MarkLabel(sendExitLabel);
            Builder.Emit(OpCode.Send, afterExitLabel);
            Builder.Emit(OpCode.YieldValue);
            Builder.Jump(sendExitLabel);

            Builder.MarkLabel(afterExitLabel);
            Builder.Emit(OpCode.Swap, 2); // [result, coroutine]
            Builder.Emit(OpCode.PopTop);  // pop coroutine
            // [result]
            Builder.Emit(OpCode.ToBool); // -> [handled_bool]
            Builder.Emit(OpCode._PopExceptionIfTrue);
            Builder.PopJumpIfTrue(finallyLabel); // -> []
            Builder.Emit(OpCode.RaiseVarArgs, 0);

            Builder.MarkLabel(finallyLabel);
            Builder.Emit(OpCode._EnterFinally);

            Builder.Emit(OpCode._LoadHitExcept);
            Builder.PopJumpIfTrue(exitFinallyLabel);

            Builder.Emit(OpCode.LoadConst, PyNoneObject.None);
            Builder.Emit(OpCode.LoadConst, PyNoneObject.None);
            Builder.Emit(OpCode.LoadConst, PyNoneObject.None);
            Builder.Emit(OpCode.Call, 4);

            // Await __aexit__() result for normal exit
            Builder.Emit(OpCode.GetAwaitable);
            Builder.Emit(OpCode.LoadConst, PyNoneObject.None);

            var sendExitNormalLabel = Builder.DefineLabel();
            var afterExitNormalLabel = Builder.DefineLabel();
            Builder.MarkLabel(sendExitNormalLabel);
            Builder.Emit(OpCode.Send, afterExitNormalLabel);
            Builder.Emit(OpCode.YieldValue);
            Builder.Jump(sendExitNormalLabel);

            Builder.MarkLabel(afterExitNormalLabel);
            Builder.Emit(OpCode.Swap, 2); // [result, coroutine]
            Builder.Emit(OpCode.PopTop);  // pop coroutine
            Builder.Emit(OpCode.PopTop);  // pop result

            Builder.MarkLabel(exitFinallyLabel);
            Builder.Emit(OpCode._ExitFinally);
        }
    }

    private void EmitMatch(MatchNode node)
    {
        Span<Label> nextCaseLabels = stackalloc Label[node.Cases.Length];
        for (int i = 0; i < node.Cases.Length; i++)
            nextCaseLabels[i] = Builder.DefineLabel();
        var matchEndLabel = Builder.DefineLabel();

        LoadExpr(node.Subject);
        for (int i = 0; i < node.Cases.Length; i++)
        {
            EmitMatchCase(i, nextCaseLabels);
            Builder.MarkLabel(nextCaseLabels[i]);
        }
        Builder.Emit(OpCode.PopTop);
        Builder.MarkLabel(matchEndLabel);

        void EmitMatchCase(int i, Span<Label> nextCaseLabels)
        {
            var caseNode = node.Cases[i];

            EmitPattern(caseNode.Pattern, nextCaseLabels[i]);

            if (caseNode.Guard is not null)
            {
                LoadExpr(caseNode.Guard);
                Builder.Emit(OpCode.ToBool);
                Builder.PopJumpIfFalse(nextCaseLabels[i]);
            }

            Builder.Emit(OpCode.PopTop);
            EmitStmts(caseNode.Body);

            Builder.Jump(matchEndLabel);
        }

        // in the current design,
        // the state of the operand stack before and after EmitPattern
        // should remain unchanged.
        void EmitPattern(AstPatternNode pattern, Label matchFailLabel)
        {
            switch (pattern)
            {
                case MatchValueNode node:
                    Builder.Emit(OpCode.Copy, 1);
                    LoadExpr(node.Value);
                    Builder.Emit(OpCode.CompareOp, (int)CmpopType.Eq);
                    Builder.Emit(OpCode.ToBool);
                    Builder.PopJumpIfFalse(matchFailLabel);
                    break;

                case MatchSingletonNode node:
                    Builder.Emit(OpCode.Copy, 1);
                    Builder.Emit(OpCode.LoadConst, node.Value);
                    Builder.Emit(OpCode.IsOp, 0);
                    Builder.PopJumpIfFalse(matchFailLabel);
                    break;

                case MatchStarNode node:
                    if (node.Name is null)
                        break;

                    Builder.Emit(OpCode.Copy, 1);
                    StoreName(node.Name);
                    break;

                case MatchAsNode node:
                    if (node.Pattern is not null)
                        EmitPattern(node.Pattern, matchFailLabel);

                    if (node.Name is not null)
                    {
                        Builder.Emit(OpCode.Copy, 1);
                        StoreName(node.Name);
                    }
                    break;

                case MatchOrNode node:
                    {
                        Span<Label> nextPatternLabels = stackalloc Label[node.Patterns.Length];
                        for (int i = 0; i < node.Patterns.Length - 1; i++)
                            nextPatternLabels[i] = Builder.DefineLabel();
                        nextPatternLabels[^1] = matchFailLabel;
                        var orEndLabel = Builder.DefineLabel();

                        for (int i = 0; i < node.Patterns.Length; i++)
                        {
                            EmitPattern(node.Patterns[i], nextPatternLabels[i]);
                            Builder.Jump(orEndLabel);
                            if (i < node.Patterns.Length - 1)
                                Builder.MarkLabel(nextPatternLabels[i]);
                        }

                        Builder.MarkLabel(orEndLabel);
                    }
                    break;

                case MatchSequenceNode node:
                    {
                        // ensure subject is sequence
                        Builder.Emit(OpCode.MatchSequence);
                        Builder.PopJumpIfFalse(matchFailLabel);

                        // ensure length of subject is enough
                        Builder.Emit(OpCode.GetLen);
                        var (index, starred) = node.Patterns.Index().FirstOrDefault(static item => item.Item is MatchStarNode);
                        var hasStar = starred is not null;
                        Builder.Emit(OpCode.LoadConst, PyIntObject.FromInteger(node.Patterns.Length + (hasStar ? -1 : 0)));
                        Builder.Emit(OpCode.CompareOp, (int)(hasStar ? CmpopType.GtE : CmpopType.Eq));
                        Builder.PopJumpIfFalse(matchFailLabel);

                        // unpack subject
                        Builder.Emit(OpCode.Copy, 1);
                        if (hasStar)
                            Builder.Emit(OpCode.UnpackEx, (index << 16) | (node.Patterns.Length - index - 1));
                        else
                            Builder.Emit(OpCode.UnpackSequence, node.Patterns.Length);

                        // match each subpattern
                        EmitPatterns(node.Patterns, matchFailLabel);
                    }
                    break;

                case MatchMappingNode node:
                    {
                        // ensure subject is mapping
                        Builder.Emit(OpCode.MatchMapping);
                        Builder.PopJumpIfFalse(matchFailLabel);

                        // ensure keys then get value
                        foreach (var key in node.Keys)
                            LoadExpr(key);
                        Builder.Emit(OpCode.BuildTuple, node.Keys.Length);

                        // [subject, keys]
                        Builder.Emit(OpCode.MatchKeys); // -> [subject, keys, values]

                        var popKeysAndValuesLabel = Builder.DefineLabel();
                        Builder.Emit(OpCode.Copy, 1); // -> [subject, keys, values, values]
                        Builder.PopJumpIfNone(popKeysAndValuesLabel); // -> [subject, keys, values]

                        // match each subpattern
                        var popKeysThenFailLabel = Builder.DefineLabel();
                        Builder.Emit(OpCode.UnpackSequence, node.Keys.Length);
                        EmitPatterns(node.Patterns, popKeysThenFailLabel);

                        if (node.Rest is not null)
                        {
                            // [subject, keys]
                            Builder.Emit(OpCode.Copy, 2); // -> [subject, keys, subject]
                            Builder.Emit(OpCode.BuildMap, 0); // -> [subject, keys, subject, {}]
                            Builder.Emit(OpCode.Swap, 2); // -> [subject, keys, {}, subject]
                            Builder.Emit(OpCode.DictUpdate, 1); // [subject, keys, {**subject}]
                            Builder.Emit(OpCode.Swap, 2); // -> [subject, {**subject}, keys]

                            Builder.Emit(OpCode.UnpackSequence, node.Keys.Length); // -> [subject, {**subject}, *keys]
                            for (int i = node.Keys.Length - 1; i >= 0; i--)
                            {
                                // [subject, {**subject}, key_0 ... key_i]
                                Builder.Emit(OpCode.Copy, i + 2); // -> [subject, {**subject}, key_0 ... key_i, {**subject}]
                                Builder.Emit(OpCode.Swap, 2); // -> [subject, {**subject}, key_0 ... key_i-1, {**subject}, key_i]
                                Builder.Emit(OpCode.DeleteSubscr); // -> [subject, {**subject_removed_key_i}, key_0 ... key_i-1]
                            }
                            // -> [subject, {**subject_removed_keys}]

                            StoreName(node.Rest);
                        }

                        var matchedLabel = Builder.DefineLabel();
                        Builder.Jump(matchedLabel);

                        Builder.MarkLabel(popKeysAndValuesLabel);
                        Builder.Emit(OpCode.PopTop);

                        Builder.MarkLabel(popKeysThenFailLabel);
                        Builder.Emit(OpCode.PopTop);
                        Builder.Jump(matchFailLabel);

                        Builder.MarkLabel(matchedLabel);
                    }
                    break;

                case MatchClassNode node:
                    {
                        Builder.Emit(OpCode.Copy, 1);
                        LoadExpr(node.Cls);
                        Builder.Emit(OpCode.LoadConst, PyTupleObject.CreateTuple(node.KwdAttrs.Select(PyStrObject.FromString)));
                        Builder.Emit(OpCode.MatchClass, node.Patterns.Length);

                        Builder.Emit(OpCode._CheckMatch, matchFailLabel);

                        Builder.Emit(OpCode.UnpackSequence, node.Patterns.Length + node.KwdPatterns.Length);
                        EmitPatterns([.. node.Patterns.Concat(node.KwdPatterns)], matchFailLabel);
                    }
                    break;
            }

            void EmitPatterns(IReadOnlyList<AstPatternNode> patterns, Label matchFailLabel)
            {
                var patternsEndLabel = Builder.DefineLabel();
                Span<Label> popTopLabels = stackalloc Label[patterns.Count];

                for (int i = 0; i < patterns.Count; i++)
                {
                    popTopLabels[i] = Builder.DefineLabel();
                    EmitPattern(patterns[i], popTopLabels[i]);
                    Builder.Emit(OpCode.PopTop);
                }
                Builder.Jump(patternsEndLabel);

                for (int i = 0; i < patterns.Count; i++)
                {
                    Builder.MarkLabel(popTopLabels[i]);
                    Builder.Emit(OpCode.PopTop);
                }
                Builder.Jump(matchFailLabel);

                Builder.MarkLabel(patternsEndLabel);
            }
        }
    }
}
