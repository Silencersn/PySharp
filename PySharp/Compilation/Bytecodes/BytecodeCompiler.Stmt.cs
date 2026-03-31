using PySharp.Compilation.AstNodes;
using PySharp.Compilation.Bytecodes.Extensions;
using PySharp.Compilation.Primitives;
using PySharp.Modules.Builtins;
using PySharp.Runtime;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Xml.Linq;

namespace PySharp.Compilation.Bytecodes;

partial class BytecodeCompiler
{
    private void CompileStmts(ImmutableArray<AstStmtNode> stmts, out bool isPostUnreachable)
    {
        isPostUnreachable = false;
        foreach (var stmt in stmts)
        {
            CompileStmt(stmt, out isPostUnreachable);
            if (isPostUnreachable)
                break;
        }
    }

    private void CompileStmts(ImmutableArray<AstStmtNode> stmts)
    {
        CompileStmts(stmts, out _);
    }

    private void CompileStmt(AstStmtNode node, out bool isPostUnreachable)
    {
        isPostUnreachable = false;
        Generator.PushMetaInfo(node.MetaInfo);
        switch (node)
        {
            case ExprNode n: CompileExpr(n); break;
            case PassNode n: CompilePass(n); break;
            case AssignNode n: CompileAssign(n); break;
            case AugAssignNode n: CompileAugAssign(n); break;
            case AnnAssignNode n: CompileAnnAssign(n); break;
            case DeleteNode n: CompileDelete(n); break;
            case RaiseNode n: CompileRaise(n, out isPostUnreachable); break;
            case BreakNode n: CompileBreak(n, out isPostUnreachable); break;
            case ContinueNode n: CompileContinue(n, out isPostUnreachable); break;
            case ReturnNode n: CompileReturn(n, out isPostUnreachable); break;
            case ImportNode n: CompileImport(n); break;
            case ImportFromNode n: CompileImportFrom(n); break;
            case GlobalNode n: CompileGlobal(n); break;
            case NonlocalNode n: CompileNonlocal(n); break;
            case AssertNode n: CompileAssert(n); break;
            case IfNode n: CompileIf(n, out isPostUnreachable); break;
            case TryNode n: CompileTry(n); break;
            case TryStarNode n: CompileTryStar(n); break;
            case ForNode n: CompileFor(n); break;
            case WhileNode n: CompileWhile(n); break;
            case WithNode n: CompileWith(n); break;
            case MatchNode n: CompileMatch(n); break;
            case FunctionDefNode n: CompileFunctionDef(n); break;
            case ClassDefNode n: CompileClassDef(n); break;
            case AsyncFunctionDefNode n: CompileAsyncFunctionDef(n); break;
            default: throw new UnreachableException();
        }
        Generator.PopMetaInfo();
    }

    private void CompileExpr(ExprNode node)
    {
        LoadExpr(node.Value);
        Generator.Emit(IsInteractive && VariableScope is RootVariableScope ? OpCode._CallPrintIfNotNone : OpCode.PopTop);
    }

    private void CompileAssign(AssignNode node)
    {
        LoadExpr(node.Value);
        for (int i = 0; i < node.Targets.Length; i++)
        {
            if (i < node.Targets.Length - 1)
                Generator.Emit(OpCode.Copy, 1);
            StoreExpr(node.Targets[i]);
        }
    }

    private void CompileAnnAssign(AnnAssignNode node)
    {
        // TODO: __annotations__

        if (node.Value is null)
            return;

        LoadExpr(node.Value);
        StoreExpr(node.Target);
    }

    private void CompileIf(IfNode node, out bool isPostUnreachable)
    {
        var test = Reducer.ToBool(node.Test);
        if (test is not null)
        {
            CompileStmts(test.Value ? node.Body : node.OrElse, out isPostUnreachable);
            return;
        }

        var elseBlockLabel = Generator.DefineLabel();
        var ifStmtEndLabel = Generator.DefineLabel();

        LoadExpr(node.Test);
        Generator.Emit(OpCode.ToBool);
        Generator.PopJumpIfFalse(elseBlockLabel);

        CompileStmts(node.Body, out var bodyPostUnreachable);
        Generator.Jump(ifStmtEndLabel);

        Generator.MarkLabel(elseBlockLabel);
        CompileStmts(node.OrElse, out var orElsePostUnreachable);

        Generator.MarkLabel(ifStmtEndLabel);

        isPostUnreachable = bodyPostUnreachable && orElsePostUnreachable;
    }

    private void CompileRaise(RaiseNode node, out bool isPostUnreachable)
    {
        isPostUnreachable = true;

        if (node.Exc is null)
        {
            Generator.Emit(OpCode.RaiseVarArgs, 0);
            return;
        }

        LoadExpr(node.Exc);
        if (node.Cause is null)
        {
            Generator.Emit(OpCode.RaiseVarArgs, 1);
            return;
        }

        LoadExpr(node.Cause);
        Generator.Emit(OpCode.RaiseVarArgs, 2);
    }

    private void CompileTry(TryNode node)
    {
        var finallyBlockLabel = Generator.DefineLabel();
        Span<Label> exceptorLabels = stackalloc Label[node.Exceptors.Length];
        for (int i = 0; i < node.Exceptors.Length; i++)
            exceptorLabels[i] = Generator.DefineLabel();
        var tryStmtEndLabel = Generator.DefineLabel();

        Generator.Emit(OpCode._SetupFinally, finallyBlockLabel);
        if (exceptorLabels.Length > 0)
            Generator.Emit(OpCode._SetupExcept, exceptorLabels[0]);

        CompileStmts(node.Body);
        CompileStmts(node.OrElse);
        Generator.MarkLabel(finallyBlockLabel);
        Generator.Emit(OpCode._EnterFinally);
        CompileStmts(node.FinalBody);
        Generator.Emit(OpCode._ExitFinally);
        Generator.Jump(tryStmtEndLabel);

        for (int i = 0; i < node.Exceptors.Length; i++)
        {
            Generator.MarkLabel(exceptorLabels[i]);

            var exceptor = node.Exceptors[i];
            if (i < node.Exceptors.Length - 1)
            {
                Debug.Assert(exceptor.Type is not null);
                LoadExpr(exceptor.Type);
                Generator.Emit(OpCode.CheckExcMatch);
                Generator.PopJumpIfFalse(exceptorLabels[i + 1]); // jump to next except

            }
            else
            {
                if (exceptor.Type is not null)
                {
                    LoadExpr(exceptor.Type);
                    Generator.Emit(OpCode.CheckExcMatch);
                    Generator.PopJumpIfFalse(finallyBlockLabel); // last exceptor, jump to finally
                }
            }

            if (exceptor.Name is not null)
            {
                Generator.Emit(OpCode._LoadExc);
                StoreName(exceptor.Name);
            }

            CompileStmts(exceptor.Body);

            if (exceptor.Name is not null)
                DeleteName(exceptor.Name);

            Generator.Emit(OpCode._PopException);
            Generator.Jump(finallyBlockLabel); // jump to finally
        }

        Generator.MarkLabel(tryStmtEndLabel);
    }

    private void CompileTryStar(TryStarNode node)
    {
        var finallyBlockLabel = Generator.DefineLabel();
        Span<Label> exceptorLabels = stackalloc Label[node.Exceptors.Length];
        for (int i = 0; i < node.Exceptors.Length; i++)
            exceptorLabels[i] = Generator.DefineLabel();
        var tryStmtEndLabel = Generator.DefineLabel();

        Generator.Emit(OpCode._SetupFinally, finallyBlockLabel);
        Debug.Assert(exceptorLabels.Length > 0);
        Generator.Emit(OpCode._SetupExcept, exceptorLabels[0]);

        CompileStmts(node.Body);
        CompileStmts(node.OrElse);
        Generator.MarkLabel(finallyBlockLabel);
        Generator.Emit(OpCode._EnterFinally);
        CompileStmts(node.FinalBody);
        Generator.Emit(OpCode._ExitFinally);
        Generator.Jump(tryStmtEndLabel);

        for (int i = 0; i < node.Exceptors.Length; i++)
        {
            Generator.MarkLabel(exceptorLabels[i]);

            var exceptor = node.Exceptors[i];
            Debug.Assert(exceptor.Type is not null);
            LoadExpr(exceptor.Type);
            Generator.Emit(OpCode.CheckEgMatch);
            var nextLabel = i < node.Exceptors.Length - 1 ? exceptorLabels[i + 1] : finallyBlockLabel;
            Generator.Emit(OpCode._CheckMatch, nextLabel); // if match None, jump to next except or finally

            if (exceptor.Name is not null)
                StoreName(exceptor.Name);
            else
                Generator.Emit(OpCode.PopTop);

            CompileStmts(exceptor.Body);

            if (exceptor.Name is not null)
                DeleteName(exceptor.Name);

            Generator.Emit(OpCode._PopExceptionAndJumpIfNull, finallyBlockLabel); // pop exc and jump to finally if rest is None
            Generator.Jump(nextLabel); // jump to next except or finally
        }

        Generator.MarkLabel(tryStmtEndLabel);
    }

    private void CompileFor(ForNode node)
    {
        var forIterLabel = Generator.DefineLabel();
        var forElseLabel = Generator.DefineLabel();
        var endForLabel = Generator.DefineLabel();
        Loops.Push((forIterLabel, endForLabel));

        LoadExpr(node.Iter);
        Generator.Emit(OpCode.GetIter);

        Generator.MarkLabel(forIterLabel);
        Generator.Emit(OpCode.ForIter, forElseLabel);
        StoreExpr(node.Target);

        CompileStmts(node.Body);
        Generator.Jump(forIterLabel);

        Generator.MarkLabel(forElseLabel);
        CompileStmts(node.OrElse);

        Generator.MarkLabel(endForLabel);
        Generator.Emit(OpCode.PopIter);

        Loops.Pop();
    }

    private void CompileBreak(BreakNode node, out bool isPostUnreachable)
    {
        isPostUnreachable = true;
        Generator.Jump(Loops.Peek().LoopEnd);
    }

    private void CompileContinue(ContinueNode node, out bool isPostUnreachable)
    {
        isPostUnreachable = true;
        Generator.Jump(Loops.Peek().LoopBegin);
    }

    private void CompileWhile(WhileNode node)
    {
        var whileBeginLabel = Generator.DefineLabel();
        var whileElseLabel = Generator.DefineLabel();
        var whileEndLabel = Generator.DefineLabel();
        Loops.Push((whileBeginLabel, whileEndLabel));

        Generator.MarkLabel(whileBeginLabel);
        LoadExpr(node.Test);
        Generator.Emit(OpCode.ToBool);
        Generator.PopJumpIfFalse(whileElseLabel);

        CompileStmts(node.Body);
        Generator.Jump(whileBeginLabel);

        Generator.MarkLabel(whileElseLabel);
        CompileStmts(node.OrElse);

        Generator.MarkLabel(whileEndLabel);

        Loops.Pop();
    }

    private void CompilePass(PassNode node)
    {
        Generator.Emit(OpCode.NoOperation);
    }

    private void InternalCompileIFunctionDef(IFunctionDefNode node)
    {
        var currentGenerator = Generator;
        Generator = BytecodeGenerator.Create(_source);
        var currentScope = VariableScope;
        var scope = Model.GetVariableScope<CallableVariableScope>((AstNode)node);
        Debug.Assert(scope is not null);
        VariableScope = scope;

        foreach (var cell in scope.CellVars)
            Generator.Emit(OpCode._MakeCellFast, scope.LocalsTable[cell]);

        if (scope.IsGenerator || scope is AsyncFunctionVariableScope)
        {
            Generator.Emit(OpCode.ReturnGenerator);
            Generator.Emit(OpCode.PopTop); // pop the first sent to activate the generator
        }
        CompileStmts(node.Body, out var bodyPostUnreachable);
        if (!bodyPostUnreachable)
        {
            Generator.Emit(OpCode.LoadConst, PyNoneObject.None);
            Generator.Emit(OpCode.ReturnValue);
        }

        var bytecode = Generator.ToBytecode();

        Generator = currentGenerator;
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
                Generator.Emit(OpCode.PushNull);
        }

        Generator.Emit(OpCode.LoadConst, codeObj);
        Generator.Emit(OpCode._MakeFunctionWithPyArgsDef);

        if (TryGetDoc(node.Body, out var doc))
        {
            Generator.Emit(OpCode.Copy, 1);
            Generator.Emit(OpCode.LoadConst, doc);
            Generator.Emit(OpCode.Swap, 2);
            Generator.Emit(OpCode.StoreAttr, PySpecialNames.Doc);
        }

        for (int i = 0; i < node.DecoratorList.Length; i++)
            Generator.Emit(OpCode.Call, 1);

        StoreName(node.Name);
    }

    private void CompileFunctionDef(FunctionDefNode node)
    {
        InternalCompileIFunctionDef(node);
    }

    private void CompileAsyncFunctionDef(AsyncFunctionDefNode node)
    {
        InternalCompileIFunctionDef(node);
    }

    private void CompileReturn(ReturnNode node, out bool isPostUnreachable)
    {
        isPostUnreachable = true;

        if (node.Value is not null)
            LoadExpr(node.Value);
        else
            Generator.Emit(OpCode.LoadConst, PyNoneObject.None);
        Generator.Emit(OpCode.ReturnValue);
    }

    private void CompileGlobal(GlobalNode node)
    {
        // do nothing
    }

    private void CompileNonlocal(NonlocalNode node)
    {
        // do nothing
    }

    private void CompileClassDef(ClassDefNode node)
    {
        var currentGenerator = Generator;
        Generator = BytecodeGenerator.Create(_source);
        var currentScope = VariableScope;
        var scope = Model.GetVariableScope<ClassVariableScope>(node);
        Debug.Assert(scope is not null);
        VariableScope = scope;

        if (scope.ClassCaptured)
        {
            Generator.Emit(OpCode.MakeCell, PySpecialNames.Class);
            Generator.Emit(OpCode._LoadClass);
            Generator.Emit(OpCode.StoreDeref, PySpecialNames.Class);
        }

        if (TryGetDoc(node.Body, out var doc))
        {
            Generator.Emit(OpCode.LoadConst, doc);
            StoreName(PySpecialNames.Doc);
        }

        CompileStmts(node.Body);

        var bytecode = Generator.ToBytecode();

        Generator = currentGenerator;
        VariableScope = currentScope;

        var codeObj = new PyCodeObject(_source.Name, scope, bytecode);

        foreach (var decorator in node.DecoratorList)
            LoadExpr(decorator);

        foreach (var baseType in node.Bases)
            LoadExpr(baseType);

        // TODO: Keywords

        Generator.Emit(OpCode.LoadConst, codeObj);
        Generator.Emit(OpCode._BuildClass, node.Bases.Length);

        for (int i = 0; i < node.DecoratorList.Length; i++)
            Generator.Emit(OpCode.Call, 1);

        StoreName(node.Name);
    }

    private void CompileAssert(AssertNode node)
    {
        if (_context.PyEnvironment.OptimizationLevel > 0)
            return;

        var noRaisingLabel = Generator.DefineLabel();

        LoadExpr(node.Test);
        Generator.Emit(OpCode.ToBool);
        Generator.PopJumpIfTrue(noRaisingLabel);

        Generator.Emit(OpCode.LoadConst, PyAssertionErrorObjectType.Shared);

        if (node.Msg is not null)
        {
            LoadExpr(node.Msg);
            Generator.Emit(OpCode.Call, 1); // AssertionError(msg)
        }

        Generator.Emit(OpCode.RaiseVarArgs, 1);
        Generator.MarkLabel(noRaisingLabel);
    }

    private void CompileAugAssign(AugAssignNode node)
    {
        LoadExpr(node.Target);
        LoadExpr(node.Value);
        Generator.Emit(OpCode._AugAssignOp, (int)node.Op);
        StoreExpr(node.Target);
    }

    private void CompileDelete(DeleteNode node)
    {
        foreach (var target in node.Targets)
            DeleteExpr(target);
    }

    private void CompileImport(ImportNode node)
    {
        foreach (var name in node.Names)
        {
            Generator.Emit(OpCode.LoadConst, PyIntObject.Zero);
            Generator.Emit(OpCode.LoadConst, PyNoneObject.None);
            Generator.Emit(OpCode.ImportName, name.Name);

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
                        Generator.Emit(OpCode.ImportFrom, parts[i]); // -> [mod, mod.submod]
                        Generator.Emit(OpCode.Swap, 2); // -> [mod.submod, mod]
                        Generator.Emit(OpCode.PopTop); // -> [mod.submod]
                    }
                    Generator.Emit(OpCode.ImportFrom, parts[^1]);
                    StoreName(name.AsName);

                    Generator.Emit(OpCode.PopTop);
                }
            }
        }
    }

    private void CompileImportFrom(ImportFromNode node)
    {
        Generator.Emit(OpCode.LoadConst, PyIntObject.FromInteger(node.Level));
        Generator.Emit(OpCode.LoadConst, PyTupleObject.CreateTuple(node.Names.Select(static alias => PyStrObject.FromString(alias.Name))));
        Generator.Emit(OpCode.ImportName, node.Module ?? string.Empty);

        if (node.IsImportStar())
        {
            Generator.Emit(OpCode._ImportAllFrom);
            return;
        }

        foreach (var name in node.Names)
        {
            Debug.Assert(name.Name is not "*");
            Generator.Emit(OpCode.ImportFrom, name.Name);
            StoreName(name.GetLocalName());
        }

        Generator.Emit(OpCode.PopTop);
    }

    private void CompileWith(WithNode node)
    {
        CompileWithItem(0);

        void CompileWithItem(int i)
        {
            if (i == node.Items.Length)
            {
                CompileStmts(node.Body);
                return;
            }

            var finallyLabel = Generator.DefineLabel();
            var exitFinallyLabel = Generator.DefineLabel();
            var exceptLabel = Generator.DefineLabel();

            var item = node.Items[i];

            // []
            LoadExpr(item.ContextExpr); // -> [manager]
            Generator.Emit(OpCode.LoadSpecial, PySpecialNames.Enter); // -> [manager, enter]
            Generator.Emit(OpCode.Swap, 2); // [enter, manager]
            Generator.Emit(OpCode.LoadSpecial, PySpecialNames.Exit); // -> [enter, manager, exit]
            Generator.Emit(OpCode.Swap, 3); // -> [exit, manager, enter]
            Generator.Emit(OpCode.Copy, 2); // -> [exit, manager, enter, manager]
            Generator.Emit(OpCode.Call, 1); // -> [exit, manager, value]

            Generator.Emit(OpCode._SetupFinally, finallyLabel);
            Generator.Emit(OpCode._SetupExcept, exceptLabel);

            if (item.OptionalVars is not null)
                StoreExpr(item.OptionalVars);
            else
                Generator.Emit(OpCode.PopTop);
            // -> [exit, manager]

            CompileWithItem(i + 1);
            Generator.Jump(finallyLabel);

            Generator.MarkLabel(exceptLabel);
            Generator.Emit(OpCode._LoadExcInfo); // -> [exit, manager, exc_type, exc, traceback]
            Generator.Emit(OpCode.Call, 4); // -> [handled]
            Generator.Emit(OpCode.ToBool); // -> [handled_bool]
            Generator.Emit(OpCode._PopExceptionIfTrue);
            Generator.PopJumpIfTrue(finallyLabel); // -> []
            Generator.Emit(OpCode.RaiseVarArgs, 0);

            Generator.MarkLabel(finallyLabel);
            Generator.Emit(OpCode._EnterFinally);

            Generator.Emit(OpCode._LoadHitExcept);
            Generator.PopJumpIfTrue(exitFinallyLabel);

            Generator.Emit(OpCode.LoadConst, PyNoneObject.None);
            Generator.Emit(OpCode.LoadConst, PyNoneObject.None);
            Generator.Emit(OpCode.LoadConst, PyNoneObject.None);
            Generator.Emit(OpCode.Call, 4);
            Generator.Emit(OpCode.PopTop);

            Generator.MarkLabel(exitFinallyLabel);
            Generator.Emit(OpCode._ExitFinally);
        }
    }

    private void CompileMatch(MatchNode node)
    {
        Span<Label> nextCaseLabels = stackalloc Label[node.Cases.Length];
        for (int i = 0; i < node.Cases.Length; i++)
            nextCaseLabels[i] = Generator.DefineLabel();
        var matchEndLabel = Generator.DefineLabel();

        LoadExpr(node.Subject);
        for (int i = 0; i < node.Cases.Length; i++)
        {
            CompileMatchCase(i, nextCaseLabels);
            Generator.MarkLabel(nextCaseLabels[i]);
        }
        Generator.Emit(OpCode.PopTop);
        Generator.MarkLabel(matchEndLabel);

        void CompileMatchCase(int i, Span<Label> nextCaseLabels)
        {
            var caseNode = node.Cases[i];

            CompilePattern(caseNode.Pattern, nextCaseLabels[i]);

            if (caseNode.Guard is not null)
            {
                LoadExpr(caseNode.Guard);
                Generator.Emit(OpCode.ToBool);
                Generator.PopJumpIfFalse(nextCaseLabels[i]);
            }

            Generator.Emit(OpCode.PopTop);
            CompileStmts(caseNode.Body);

            Generator.Jump(matchEndLabel);
        }

        // in the current design,
        // the state of the operand stack before and after CompilePattern
        // should remain unchanged.
        void CompilePattern(AstPatternNode pattern, Label matchFailLabel)
        {
            switch (pattern)
            {
                case MatchValueNode node:
                    Generator.Emit(OpCode.Copy, 1);
                    LoadExpr(node.Value);
                    Generator.Emit(OpCode.CompareOp, (int)CmpopType.Eq);
                    Generator.Emit(OpCode.ToBool);
                    Generator.PopJumpIfFalse(matchFailLabel);
                    break;

                case MatchSingletonNode node:
                    Generator.Emit(OpCode.Copy, 1);
                    Generator.Emit(OpCode.LoadConst, node.Value);
                    Generator.Emit(OpCode.IsOp, 0);
                    Generator.PopJumpIfFalse(matchFailLabel);
                    break;

                case MatchStarNode node:
                    if (node.Name is null)
                        break;

                    Generator.Emit(OpCode.Copy, 1);
                    StoreName(node.Name);
                    break;

                case MatchAsNode node:
                    if (node.Pattern is not null)
                        CompilePattern(node.Pattern, matchFailLabel);

                    if (node.Name is not null)
                    {
                        Generator.Emit(OpCode.Copy, 1);
                        StoreName(node.Name);
                    }
                    break;

                case MatchOrNode node:
                    {
                        Span<Label> nextPatternLabels = stackalloc Label[node.Patterns.Length];
                        for (int i = 0; i < node.Patterns.Length - 1; i++)
                            nextPatternLabels[i] = Generator.DefineLabel();
                        nextPatternLabels[^1] = matchFailLabel;
                        var orEndLabel = Generator.DefineLabel();

                        for (int i = 0; i < node.Patterns.Length; i++)
                        {
                            CompilePattern(node.Patterns[i], nextPatternLabels[i]);
                            Generator.Jump(orEndLabel);
                            if (i < node.Patterns.Length - 1)
                                Generator.MarkLabel(nextPatternLabels[i]);
                        }

                        Generator.MarkLabel(orEndLabel);
                    }
                    break;

                case MatchSequenceNode node:
                    {
                        // ensure subject is sequence
                        Generator.Emit(OpCode.MatchSequence);
                        Generator.PopJumpIfFalse(matchFailLabel);

                        // ensure length of subject is enough
                        Generator.Emit(OpCode.GetLen);
                        var (index, starred) = node.Patterns.Index().FirstOrDefault(static item => item.Item is MatchStarNode);
                        var hasStar = starred is not null;
                        Generator.Emit(OpCode.LoadConst, PyIntObject.FromInteger(node.Patterns.Length + (hasStar ? -1 : 0)));
                        Generator.Emit(OpCode.CompareOp, (int)(hasStar ? CmpopType.GtE : CmpopType.Eq));
                        Generator.PopJumpIfFalse(matchFailLabel);

                        // unpack subject
                        Generator.Emit(OpCode.Copy, 1);
                        if (hasStar)
                            Generator.Emit(OpCode.UnpackEx, (index << 16) | (node.Patterns.Length - index - 1));
                        else
                            Generator.Emit(OpCode.UnpackSequence, node.Patterns.Length);

                        // match each subpattern
                        CompilePatterns(node.Patterns, matchFailLabel);
                    }
                    break;

                case MatchMappingNode node:
                    {
                        // ensure subject is mapping
                        Generator.Emit(OpCode.MatchMapping);
                        Generator.PopJumpIfFalse(matchFailLabel);

                        // ensure keys then get value
                        foreach (var key in node.Keys)
                            LoadExpr(key);
                        Generator.Emit(OpCode.BuildTuple, node.Keys.Length);

                        // [subject, keys]
                        Generator.Emit(OpCode.MatchKeys); // -> [subject, keys, values]

                        var popKeysAndValuesLabel = Generator.DefineLabel();
                        Generator.Emit(OpCode.Copy, 1); // -> [subject, keys, values, values]
                        Generator.PopJumpIfNone(popKeysAndValuesLabel); // -> [subject, keys, values]

                        // match each subpattern
                        var popKeysThenFailLabel = Generator.DefineLabel();
                        Generator.Emit(OpCode.UnpackSequence, node.Keys.Length);
                        CompilePatterns(node.Patterns, popKeysThenFailLabel);

                        if (node.Rest is not null)
                        {
                            // [subject, keys]
                            Generator.Emit(OpCode.Copy, 2); // -> [subject, keys, subject]
                            Generator.Emit(OpCode.BuildMap, 0); // -> [subject, keys, subject, {}]
                            Generator.Emit(OpCode.Swap, 2); // -> [subject, keys, {}, subject]
                            Generator.Emit(OpCode.DictUpdate, 1); // [subject, keys, {**subject}]
                            Generator.Emit(OpCode.Swap, 2); // -> [subject, {**subject}, keys]

                            Generator.Emit(OpCode.UnpackSequence, node.Keys.Length); // -> [subject, {**subject}, *keys]
                            for (int i = node.Keys.Length - 1; i >= 0; i--)
                            {
                                // [subject, {**subject}, key_0 ... key_i]
                                Generator.Emit(OpCode.Copy, i + 2); // -> [subject, {**subject}, key_0 ... key_i, {**subject}]
                                Generator.Emit(OpCode.Swap, 2); // -> [subject, {**subject}, key_0 ... key_i-1, {**subject}, key_i]
                                Generator.Emit(OpCode.DeleteSubscr); // -> [subject, {**subject_removed_key_i}, key_0 ... key_i-1]
                            }
                            // -> [subject, {**subject_removed_keys}]

                            StoreName(node.Rest);
                        }

                        var matchedLabel = Generator.DefineLabel();
                        Generator.Jump(matchedLabel);

                        Generator.MarkLabel(popKeysAndValuesLabel);
                        Generator.Emit(OpCode.PopTop);

                        Generator.MarkLabel(popKeysThenFailLabel);
                        Generator.Emit(OpCode.PopTop);
                        Generator.Jump(matchFailLabel);

                        Generator.MarkLabel(matchedLabel);
                    }
                    break;

                case MatchClassNode node:
                    {
                        Generator.Emit(OpCode.Copy, 1);
                        LoadExpr(node.Cls);
                        Generator.Emit(OpCode.LoadConst, PyTupleObject.CreateTuple(node.KwdAttrs.Select(PyStrObject.FromString)));
                        Generator.Emit(OpCode.MatchClass, node.Patterns.Length);

                        Generator.Emit(OpCode._CheckMatch, matchFailLabel);

                        Generator.Emit(OpCode.UnpackSequence, node.Patterns.Length + node.KwdPatterns.Length);
                        CompilePatterns([.. node.Patterns.Concat(node.KwdPatterns)], matchFailLabel);
                    }
                    break;
            }

            void CompilePatterns(IReadOnlyList<AstPatternNode> patterns, Label matchFailLabel)
            {
                var patternsEndLabel = Generator.DefineLabel();
                Span<Label> popTopLabels = stackalloc Label[patterns.Count];

                for (int i = 0; i < patterns.Count; i++)
                {
                    popTopLabels[i] = Generator.DefineLabel();
                    CompilePattern(patterns[i], popTopLabels[i]);
                    Generator.Emit(OpCode.PopTop);
                }
                Generator.Jump(patternsEndLabel);

                for (int i = 0; i < patterns.Count; i++)
                {
                    Generator.MarkLabel(popTopLabels[i]);
                    Generator.Emit(OpCode.PopTop);
                }
                Generator.Jump(matchFailLabel);

                Generator.MarkLabel(patternsEndLabel);
            }
        }
    }
}
