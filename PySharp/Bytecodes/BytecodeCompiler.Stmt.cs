using PySharp.AstNodes;
using PySharp.PyModules.Builtins;
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
            case ForNode n: CompileFor(n); break;
            case WhileNode n: CompileWhile(n); break;
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
        for (int i = 0; i <  node.Exceptors.Length; i++)
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
}
