using PySharp.AstNodes;
using PySharp.PyModules.Builtins;
using PySharp.PyRuntime;
using System.Diagnostics;

namespace PySharp.Bytecodes;

partial class BytecodeCompiler
{
    private void CompileStmt(AstStmtNode node)
    {
        switch (node)
        {
            case ExprNode n: CompileExpr(n); break;
            case PassNode n: CompilePass(n); break;
            case AssignNode n: CompileAssign(n); break;
            case AugAssignNode n: CompileAugAssign(n); break;
            case AnnAssignNode n: CompileAnnAssign(n); break;
            case DeleteNode n: CompileDelete(n); break;
            case RaiseNode n: CompileRaise(n); break;
            case BreakNode n: CompileBreak(n); break;
            case ContinueNode n: CompileContinue(n); break;
            case ReturnNode n: CompileReturn(n); break;
            case ImportNode n: CompileImport(n); break;
            case ImportFromNode n: CompileImportFrom(n); break;
            case GlobalNode n: CompileGlobal(n); break;
            case NonlocalNode n: CompileNonlocal(n); break;
            case AssertNode n: CompileAssert(n); break;
            case IfNode n: CompileIf(n); break;
            case TryNode n: CompileTry(n); break;
            case TryStarNode n: CompileTryStar(n); break;
            case ForNode n: CompileFor(n); break;
            case WhileNode n: CompileWhile(n); break;
            case WithNode n: CompileWith(n); break;
            case MatchNode n: CompileMatch(n); break;
            case FunctionDefNode n: CompileFunctionDef(n); break;
            case ClassDefNode n: CompileClassDef(n); break;
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

    private void CompileRaise(RaiseNode node)
    {
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
        var exceptorLabels = new Label[node.Exceptors.Length];
        for (int i = 0; i < node.Exceptors.Length; i++)
            exceptorLabels[i] = Generator.DefineLabel();
        var tryStmtEndLabel = Generator.DefineLabel();

        if (exceptorLabels.Length > 0)
            Generator.Emit(OpCode._SetupExceptionHandler, (exceptorLabels[0], finallyBlockLabel));
        else
            Generator.Emit(OpCode._SetupExceptionHandler, (default(Label), finallyBlockLabel));

        foreach (var stmt in node.Body)
            CompileStmt(stmt);
        foreach (var stmt in node.OrElse)
            CompileStmt(stmt);
        Generator.MarkLabel(finallyBlockLabel);
        Generator.Emit(OpCode._EnterFinally);
        foreach (var stmt in node.FinalBody)
            CompileStmt(stmt);
        Generator.Emit(OpCode._ExitFinally);
        Generator.Emit(OpCode.Jump, tryStmtEndLabel);

        for (int i = 0; i < node.Exceptors.Length; i++)
        {
            Generator.MarkLabel(exceptorLabels[i]);

            var exceptor = node.Exceptors[i];
            if (i < node.Exceptors.Length - 1)
            {
                Debug.Assert(exceptor.Type is not null);
                LoadExpr(exceptor.Type);
                Generator.Emit(OpCode.CheckExcMatch);
                Generator.Emit(OpCode.PopJumpIfFalse, exceptorLabels[i + 1]); // jump to next except

            }
            else
            {
                if (exceptor.Type is not null)
                {
                    LoadExpr(exceptor.Type);
                    Generator.Emit(OpCode.CheckExcMatch);
                    Generator.Emit(OpCode.PopJumpIfFalse, finallyBlockLabel); // last exceptor, jump to finally
                }
            }

            if (exceptor.Name is not null)
            {
                Generator.Emit(OpCode._LoadExc);
                StoreExpr(Ast.Name(exceptor.Name) /* TODO: store string directly */);
            }

            foreach (var stmt in exceptor.Body)
                CompileStmt(stmt);

            if (exceptor.Name is not null)
                DeleteExpr(Ast.Name(exceptor.Name) /* TODO: del string directly */);

            Generator.Emit(OpCode._PopException);
            Generator.Emit(OpCode.Jump, finallyBlockLabel); // jump to finally
        }

        Generator.MarkLabel(tryStmtEndLabel);
    }

    private void CompileTryStar(TryStarNode node)
    {
        var finallyBlockLabel = Generator.DefineLabel();
        var exceptorLabels = new Label[node.Exceptors.Length];
        for (int i = 0; i < node.Exceptors.Length; i++)
            exceptorLabels[i] = Generator.DefineLabel();
        var tryStmtEndLabel = Generator.DefineLabel();

        Debug.Assert(exceptorLabels.Length > 0);
        Generator.Emit(OpCode._SetupExceptionHandler, (exceptorLabels[0], finallyBlockLabel));

        foreach (var stmt in node.Body)
            CompileStmt(stmt);
        foreach (var stmt in node.OrElse)
            CompileStmt(stmt);
        Generator.MarkLabel(finallyBlockLabel);
        Generator.Emit(OpCode._EnterFinally);
        foreach (var stmt in node.FinalBody)
            CompileStmt(stmt);
        Generator.Emit(OpCode._ExitFinally);
        Generator.Emit(OpCode.Jump, tryStmtEndLabel);

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
                StoreExpr(Ast.Name(exceptor.Name) /* TODO: store string directly */);
            else
                Generator.Emit(OpCode.PopTop);

            foreach (var stmt in exceptor.Body)
                CompileStmt(stmt);

            if (exceptor.Name is not null)
                DeleteExpr(Ast.Name(exceptor.Name) /* TODO: del string directly */);

            Generator.Emit(OpCode._PopExceptionAndJumpIfNull, finallyBlockLabel); // pop exc and jump to finally if rest is None
            Generator.Emit(OpCode.Jump, nextLabel); // jump to next except or finally
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

        foreach (var stmt in node.Body)
            CompileStmt(stmt);
        Generator.Emit(OpCode.Jump, forIterLabel);

        Generator.MarkLabel(forElseLabel);
        foreach (var stmt in node.OrElse)
            CompileStmt(stmt);

        Generator.MarkLabel(endForLabel);
        Generator.Emit(OpCode.PopIter);

        Loops.Pop();
    }

    private void CompileBreak(BreakNode node)
    {
        Generator.Emit(OpCode.Jump, Loops.Peek().LoopEnd);
    }

    private void CompileContinue(ContinueNode node)
    {
        Generator.Emit(OpCode.Jump, Loops.Peek().LoopBegin);
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
        Generator.Emit(OpCode.PopJumpIfFalse, whileElseLabel);

        foreach (var stmt in node.Body)
            CompileStmt(stmt);
        Generator.Emit(OpCode.Jump, whileBeginLabel);

        Generator.MarkLabel(whileElseLabel);
        foreach (var stmt in node.OrElse)
            CompileStmt(stmt);

        Generator.MarkLabel(whileEndLabel);

        Loops.Pop();
    }

    private void CompilePass(PassNode node)
    {
        Generator.Emit(OpCode.NoOperation);
    }

    private void CompileFunctionDef(FunctionDefNode node)
    {
        var currentGenerator = Generator;
        Generator = new BytecodeGenerator();
        var currentScope = VariableScope;
        var scope = Model.GetVariableScope<CallableVariableScope>(node);
        Debug.Assert(scope is not null);
        VariableScope = scope;

        if (scope.HasYield)
        {
            Generator.Emit(OpCode.ReturnGenerator);
            Generator.Emit(OpCode.PopTop); // pop the first sent to activate the generator
        }
        foreach (var stmt in node.Body)
            CompileStmt(stmt);

        Generator.Emit(OpCode.LoadConst, PyNoneObject.None);
        Generator.Emit(OpCode.ReturnValue);
        var bytecode = new Bytecode(Generator);

        Generator = currentGenerator;
        VariableScope = currentScope;

        var codeObj = new PyCodeObject(scope, bytecode);

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
        Generator.Emit(OpCode._MakeFunctionWithPyArgsDef, node.Args);

        for (int i = 0; i < node.DecoratorList.Length; i++)
            Generator.Emit(OpCode.Call, 1);

        StoreExpr(Ast.Name(node.Name) /* TODO: no creating ast node */);
    }

    private void CompileReturn(ReturnNode node)
    {
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
        Generator = new BytecodeGenerator();
        var currentScope = VariableScope;
        var scope = Model.GetVariableScope<ClassVariableScope>(node);
        Debug.Assert(scope is not null);
        VariableScope = scope;

        foreach (var stmt in node.Body)
            CompileStmt(stmt);

        var bytecode = new Bytecode(Generator);

        Generator = currentGenerator;
        VariableScope = currentScope;

        var codeObj = new PyCodeObject(scope, bytecode);

        foreach (var decorator in node.DecoratorList)
            LoadExpr(decorator);

        foreach (var baseType in node.Bases)
            LoadExpr(baseType);

        // TODO: Keywords

        Generator.Emit(OpCode.LoadConst, codeObj);
        Generator.Emit(OpCode._BuildClass, node.Bases.Length);

        for (int i = 0; i < node.DecoratorList.Length; i++)
            Generator.Emit(OpCode.Call, 1);

        StoreExpr(Ast.Name(node.Name) /* TODO: no creating ast node */);
    }

    private void CompileAssert(AssertNode node)
    {
        var noRaisingLabel = Generator.DefineLabel();

        LoadExpr(node.Test);
        Generator.Emit(OpCode.ToBool);
        Generator.Emit(OpCode.PopJumpIfTrue, noRaisingLabel);

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
                StoreExpr(Ast.Name(name.Name) /* TODO: do not create node */);
            }
            else
            {
                var parts = name.Name.Split('.');
                for (int i = 1; i < parts.Length - 1; i++)
                {
                    // [mod]
                    Generator.Emit(OpCode.ImportFrom, parts[i]); // -> [mod, mod.submod]
                    Generator.Emit(OpCode.Swap, 2); // -> [mod.submod, mod]
                    Generator.Emit(OpCode.PopTop); // -> [mod.submod]
                }
                Generator.Emit(OpCode.ImportFrom, parts[^1]);
                StoreExpr(Ast.Name(name.AsName) /* TODO: do not create node */);

                Generator.Emit(OpCode.PopTop);
            }
        }
    }

    private void CompileImportFrom(ImportFromNode node)
    {
        Generator.Emit(OpCode.LoadConst, PyIntObject.FromInteger(node.Level));
        Generator.Emit(OpCode.LoadConst, PyTupleObject.CreateTuple(node.Names.Select(static alias => PyStrObject.FromString(alias.Name))));
        Generator.Emit(OpCode.ImportName, node.Module);

        if (node.Names.Length is 1 && node.Names[0].Name is "*")
        {
            Generator.Emit(OpCode._ImportAllFrom);
            return;
        }

        foreach (var name in node.Names)
        {
            Debug.Assert(name.Name is not "*");
            Generator.Emit(OpCode.ImportFrom, name.Name);
            StoreExpr(Ast.Name(name.GetLocalName()) /* TODO: do not create node */);
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
                foreach (var stmt in node.Body)
                    CompileStmt(stmt);
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

            Generator.Emit(OpCode._SetupExceptionHandler, (exceptLabel, finallyLabel));

            if (item.OptionalVars is not null)
                StoreExpr(item.OptionalVars);
            else
                Generator.Emit(OpCode.PopTop);
            // -> [exit, manager]

            CompileWithItem(i + 1);
            Generator.Emit(OpCode.Jump, finallyLabel);

            Generator.MarkLabel(exceptLabel);
            Generator.Emit(OpCode._LoadExcInfo); // -> [exit, manager, exc_type, exc, traceback]
            Generator.Emit(OpCode.Call, 4); // -> [handled]
            Generator.Emit(OpCode.ToBool); // -> [handled_bool]
            Generator.Emit(OpCode._PopExceptionIfTrue);
            Generator.Emit(OpCode.PopJumpIfTrue, finallyLabel); // -> []
            Generator.Emit(OpCode.RaiseVarArgs, 0);

            Generator.MarkLabel(finallyLabel);
            Generator.Emit(OpCode._EnterFinally);

            Generator.Emit(OpCode._LoadHitExcept);
            Generator.Emit(OpCode.PopJumpIfTrue, exitFinallyLabel);

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
        var nextCaseLabels = new Label[node.Cases.Length];
        for (int i = 0; i < node.Cases.Length; i++)
            nextCaseLabels[i] = Generator.DefineLabel();
        var matchEndLabel = nextCaseLabels[^1];

        LoadExpr(node.Subject);
        for (int i = 0; i < node.Cases.Length; i++)
        {
            CompileMatchCase(i);
            Generator.MarkLabel(nextCaseLabels[i]);
        }
        Generator.Emit(OpCode.PopTop);

        void CompileMatchCase(int i)
        {
            var caseNode = node.Cases[i];

            CompilePattern(caseNode.Pattern, nextCaseLabels[i]);

            if (caseNode.Guard is not null)
            {
                LoadExpr(caseNode.Guard);
                Generator.Emit(OpCode.ToBool);
                Generator.Emit(OpCode.PopJumpIfFalse, nextCaseLabels[i]);
            }

            foreach (var stmt in caseNode.Body)
                CompileStmt(stmt);

            Generator.Emit(OpCode.Jump, matchEndLabel);
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
                    Generator.Emit(OpCode.PopJumpIfFalse, matchFailLabel);
                    break;

                case MatchSingletonNode node:
                    Generator.Emit(OpCode.Copy, 1);
                    Generator.Emit(OpCode.LoadConst, node.Value);
                    Generator.Emit(OpCode.IsOp, 0);
                    Generator.Emit(OpCode.PopJumpIfFalse, matchFailLabel);
                    break;

                case MatchStarNode node:
                    if (node.Name is null)
                        break;

                    Generator.Emit(OpCode.Copy, 1);
                    StoreExpr(Ast.Name(node.Name) /* TODO: no creating ast */);
                    break;

                case MatchAsNode node:
                    if (node.Pattern is not null)
                        CompilePattern(node.Pattern, matchFailLabel);

                    if (node.Name is not null)
                    {
                        Generator.Emit(OpCode.Copy, 1);
                        StoreExpr(Ast.Name(node.Name) /* TODO: no creating ast */);
                    }
                    break;

                case MatchOrNode node:
                    {
                        var nextPatternLabels = new Label[node.Patterns.Length];
                        for (int i = 0; i < node.Patterns.Length - 1; i++)
                            nextPatternLabels[i] = Generator.DefineLabel();
                        nextPatternLabels[^1] = matchFailLabel;
                        var orEndLabel = Generator.DefineLabel();

                        for (int i = 0; i < node.Patterns.Length; i++)
                        {
                            CompilePattern(node.Patterns[i], nextPatternLabels[i]);
                            Generator.Emit(OpCode.Jump, orEndLabel);
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
                        Generator.Emit(OpCode.PopJumpIfFalse, matchFailLabel);

                        // ensure length of subject is enough
                        Generator.Emit(OpCode.GetLen);
                        var (index, starred) = node.Patterns.Index().FirstOrDefault(static item => item.Item is MatchStarNode);
                        var hasStar = starred is not null;
                        Generator.Emit(OpCode.LoadConst, PyIntObject.FromInteger(node.Patterns.Length + (hasStar ? -1 : 0)));
                        Generator.Emit(OpCode.CompareOp, (int)(hasStar ? CmpopType.GtE : CmpopType.Eq));
                        Generator.Emit(OpCode.PopJumpIfFalse, matchFailLabel);

                        // unpack subject
                        Generator.Emit(OpCode.Copy, 1);
                        if (hasStar)
                            Generator.Emit(OpCode.UnpackEx, (index, node.Patterns.Length - index - 1));
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
                        Generator.Emit(OpCode.PopJumpIfFalse, matchFailLabel);

                        // ensure keys then get value
                        foreach (var key in node.Keys)
                            LoadExpr(key);
                        Generator.Emit(OpCode.BuildTuple, node.Keys.Length);

                        // [subject, keys]
                        Generator.Emit(OpCode.MatchKeys); // -> [subject, keys, values]

                        var popKeysAndValuesLabel = Generator.DefineLabel();
                        Generator.Emit(OpCode.Copy, 1); // -> [subject, keys, values, values]
                        Generator.Emit(OpCode.PopJumpIfNone, popKeysAndValuesLabel); // -> [subject, keys, values]

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

                            StoreExpr(Ast.Name(node.Rest) /* TODO: no creating ast */);
                        }

                        var matchedLabel = Generator.DefineLabel();
                        Generator.Emit(OpCode.Jump, matchedLabel);

                        Generator.MarkLabel(popKeysAndValuesLabel);
                        Generator.Emit(OpCode.PopTop);

                        Generator.MarkLabel(popKeysThenFailLabel);
                        Generator.Emit(OpCode.PopTop);
                        Generator.Emit(OpCode.Jump, matchFailLabel);

                        Generator.MarkLabel(matchedLabel);
                    }
                    break;

                case MatchClassNode node:
                    {
                        Generator.Emit(OpCode.Copy, 1);
                        LoadExpr(node.Cls);
                        Generator.Emit(OpCode.LoadConst, PyTupleObject.CreateTuple(node.KwdAttrs.Select(PyStrObject.FromString)));
                        Generator.Emit(OpCode.MatchClass, node.Patterns.Length);

                        Generator.Emit(OpCode.Copy, 1);
                        Generator.Emit(OpCode.PopJumpIfNone, matchFailLabel);

                        Generator.Emit(OpCode.UnpackSequence, node.Patterns.Length + node.KwdPatterns.Length);
                        CompilePatterns([.. node.Patterns.Concat(node.KwdPatterns)], matchFailLabel);
                    }
                    break;
            }

            void CompilePatterns(IReadOnlyList<AstPatternNode> patterns, Label matchFailLabel)
            {
                var patternsEndLabel = Generator.DefineLabel();
                var popTopLabels = new Label[patterns.Count];

                for (int i = 0; i < patterns.Count; i++)
                {
                    popTopLabels[i] = Generator.DefineLabel();
                    CompilePattern(patterns[i], popTopLabels[i]);
                    Generator.Emit(OpCode.PopTop);
                }
                Generator.Emit(OpCode.Jump, patternsEndLabel);

                for (int i = 0; i < patterns.Count; i++)
                {
                    Generator.MarkLabel(popTopLabels[i]);
                    Generator.Emit(OpCode.PopTop);
                }
                Generator.Emit(OpCode.Jump, matchFailLabel);

                Generator.MarkLabel(patternsEndLabel);
            }
        }
    }
}
