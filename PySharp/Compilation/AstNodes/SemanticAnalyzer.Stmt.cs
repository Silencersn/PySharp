using PySharp.Compilation.Primitives;
using PySharp.Modules.Builtins;
using System.Diagnostics;

namespace PySharp.Compilation.AstNodes;

partial class SemanticAnalyzer
{
    private void VisitStmt(AstStmtNode node)
    {
        switch (node)
        {
            case ExprNode n: VisitExpr(n); break;
            case PassNode n: VisitPass(n); break;
            case AssignNode n: VisitAssign(n); break;
            case AugAssignNode n: VisitAugAssign(n); break;
            case AnnAssignNode n: VisitAnnAssign(n); break;
            case DeleteNode n: VisitDelete(n); break;
            case RaiseNode n: VisitRaise(n); break;
            case BreakNode n: VisitBreak(n); break;
            case ContinueNode n: VisitContinue(n); break;
            case ReturnNode n: VisitReturn(n); break;
            case TypeAliasNode n: VisitTypeAlias(n); break;
            case ImportNode n: VisitImport(n); break;
            case ImportFromNode n: VisitImportFrom(n); break;
            case GlobalNode n: VisitGlobal(n); break;
            case NonlocalNode n: VisitNonlocal(n); break;
            case AssertNode n: VisitAssert(n); break;
            case IfNode n: VisitIf(n); break;
            case TryNode n: VisitTry(n); break;
            case TryStarNode n: VisitTryStar(n); break;
            case ForNode n: VisitFor(n); break;
            case AsyncForNode n: VisitAsyncFor(n); break;
            case WhileNode n: VisitWhile(n); break;
            case WithNode n: VisitWith(n); break;
            case AsyncWithNode n: VisitAsyncWith(n); break;
            case MatchNode n: VisitMatch(n); break;
            case FunctionDefNode n: VisitFunctionDef(n); break;
            case AsyncFunctionDefNode n: VisitAsyncFunctionDef(n); break;
            case ClassDefNode n: VisitClassDef(n); break;
            default: throw new UnreachableException();
        }
    }

    private void VisitExpr(ExprNode node)
    {
        VisitNode(node.Value);
    }

    private static void VisitPass(PassNode node)
    {
    }

    private void VisitAssign(AssignNode node)
    {
        VisitNodes(node.Targets);
        VisitNode(node.Value);
    }

    private void VisitAugAssign(AugAssignNode node)
    {
        VisitNode(node.Target);
        VisitNode(node.Value);
    }

    private void VisitAnnAssign(AnnAssignNode node)
    {
        // CPython analyze_name: an annotated name can't be global/nonlocal.
        // root Variables are all Global (every module name is), so only
        // non-root scopes diagnose here; the root ann-then-global order is
        // caught by VisitGlobal through AnnotatedNames instead
        if (node.Simple && node.Target is NameNode { Id: var name })
        {
            var currentScope = _currentScopeStats.Scope;
            if (!currentScope.IsRoot && currentScope.Variables.TryGetValue(name, out var type))
            {
                if (type is PyVariableType.Global)
                    throw SyntaxError(PySR.InvalidSyntax_Semantic_AnnotatedNameCantBeGlobal, name);
                if (type is PyVariableType.Nonlocal)
                    throw SyntaxError(PySR.InvalidSyntax_Semantic_AnnotatedNameCantBeNonlocal, name);
            }
            currentScope.AnnotatedNames.Add(name);
        }

        VisitNode(node.Target);
        // PEP 649 evaluates class-body annotations in a code object of their
        // own but against the class-body scope, so their names belong to this
        // scope — a name from an enclosing function is captured as a cell.
        if (node.Simple && _currentScopeStats.Scope is ClassVariableScope)
            VisitNode(node.Annotation);
        VisitNullableNode(node.Value);
    }

    private void VisitDelete(DeleteNode node)
    {
        VisitNodes(node.Targets);
    }

    private void VisitRaise(RaiseNode node)
    {
        VisitNullableNode(node.Exc);
        VisitNullableNode(node.Cause);
    }

    private void VisitBreak(BreakNode node)
    {
        // CPython reports the except* violation before "outside loop"
        CheckControlStmtNotInExceptStarUntil(static n => n is ForNode or WhileNode or AsyncForNode);
        if (_currentScopeStats.LoopDepth is 0)
            throw SyntaxError(PySR.InvalidSyntax_Semantic_BreakOutsideLoop);
        if (_currentScopeStats.FinallyDepth > 0)
            CheckControlStmtNotInFinallyUntil(static n => n is ForNode or WhileNode or AsyncForNode, PySR.InvalidSyntax_Semantic_BreakInFinally);
    }

    private void VisitContinue(ContinueNode node)
    {
        CheckControlStmtNotInExceptStarUntil(static n => n is ForNode or WhileNode or AsyncForNode);
        if (_currentScopeStats.LoopDepth is 0)
            throw SyntaxError(PySR.InvalidSyntax_Semantic_ContinueOutsideLoop);
        if (_currentScopeStats.FinallyDepth > 0)
            CheckControlStmtNotInFinallyUntil(static n => n is ForNode or WhileNode or AsyncForNode, PySR.InvalidSyntax_Semantic_ContinueInFinally);
    }

    private void VisitReturn(ReturnNode node)
    {
        if (_currentScopeStats.Scope is not (FunctionVariableScope or AsyncFunctionVariableScope))
            throw SyntaxError(PySR.InvalidSyntax_Semantic_ReturnOutsideFunction);
        CheckControlStmtNotInExceptStarUntil(static n => false);
        if (_currentScopeStats.FinallyDepth > 0)
            CheckControlStmtNotInFinallyUntil(static n => false, PySR.InvalidSyntax_Semantic_ReturnInFinally);
        if (node.Value is not null && _currentScopeStats.Scope is AsyncFunctionVariableScope asyncScope)
            // checked when the scope pops: a yield later in the body still
            // makes the function an async generator
            asyncScope.ReturnWithValue ??= node;
        VisitNullableNode(node.Value);
    }

    private void VisitImport(ImportNode node)
    {
        VisitNodes(node.Names);
    }

    private void VisitImportFrom(ImportFromNode node)
    {
        if (_currentScopeStats.Scope is not RootVariableScope && node.IsImportStar())
            throw SyntaxError(PySR.InvalidSyntax_Semantic_ImportStarNotAtModuleLevel);
        VisitNodes(node.Names);
    }

    private void VisitGlobal(GlobalNode node)
    {
        var currentScope = _currentScopeStats.Scope;
        if (currentScope.IsRoot)
        {
            if (currentScope is RootVariableScope rootScope)
            {
                foreach (var name in node.Names)
                {
                    if (rootScope.AnnotatedNames.Contains(name))
                        throw SyntaxError(PySR.InvalidSyntax_Semantic_AnnotatedNameCantBeGlobal, name);
                }
                rootScope.DeclaredGlobals.UnionWith(node.Names);
            }
            return;
        }

        foreach (var name in node.Names)
        {
            if (currentScope.AnnotatedNames.Contains(name))
                throw SyntaxError(PySR.InvalidSyntax_Semantic_AnnotatedNameCantBeGlobal, name);

            if (!currentScope.Variables.TryGetValue(name, out var type))
            {
                currentScope.Variables.Add(name, PyVariableType.Global);
                continue;
            }

            switch (type)
            {
                case PyVariableType.Global:
                    break;
                case PyVariableType.Parameter:
                    throw SyntaxError(PySR.InvalidSyntax_Semantic_BothParameterAndGlobal, name);
                case PyVariableType.Nonlocal:
                    throw SyntaxError(PySR.InvalidSyntax_Semantic_BothNonlocalAndGlobal, name);
                default:
                    if (currentScope.FirstContext[name] is ExprContextType.Load)
                        throw SyntaxError(PySR.InvalidSyntax_Semantic_UsedPriorToGlobal, name);
                    else
                        throw SyntaxError(PySR.InvalidSyntax_Semantic_AssignToBeforeGlobal, name);
            }
        }
    }

    private void VisitNonlocal(NonlocalNode node)
    {
        var currentScope = _currentScopeStats.Scope;
        if (currentScope.IsRoot)
            throw SyntaxError(PySR.InvalidSyntax_Semantic_NonlocalAtModule);

        foreach (var name in node.Names)
        {
            if (currentScope.AnnotatedNames.Contains(name))
                throw SyntaxError(PySR.InvalidSyntax_Semantic_AnnotatedNameCantBeNonlocal, name);

            if (!currentScope.Variables.TryGetValue(name, out var type))
            {
                currentScope.Variables.Add(name, PyVariableType.Nonlocal);
                continue;
            }

            switch (type)
            {
                case PyVariableType.Nonlocal:
                    break;
                case PyVariableType.Parameter:
                    throw SyntaxError(PySR.InvalidSyntax_Semantic_BothParameterAndNonlocal, name);
                case PyVariableType.Global:
                    throw SyntaxError(PySR.InvalidSyntax_Semantic_BothNonlocalAndGlobal, name);
                default:
                    if (currentScope.FirstContext[name] is ExprContextType.Load)
                        throw SyntaxError(PySR.InvalidSyntax_Semantic_UsedPriorToNonlocal, name);
                    else
                        throw SyntaxError(PySR.InvalidSyntax_Semantic_AssignToBeforeNonlocal, name);
            }
        }
    }

    private void VisitAssert(AssertNode node)
    {
        // CPython codegen_assert: a non-empty tuple literal display as the
        // test (the classic "assert (cond, msg)" mistake) warns at compile
        // time; an empty tuple warns nowhere since it is always false
        if (node.Test is TupleNode { Elts.IsEmpty: false } || node.Test is ConstantNode { Value: PyTupleObject { Count: > 0 } })
            _ = _context.WarnSyntax(PySR.InvalidSyntax_Warning_AssertionAlwaysTrue, this, _session).PyUnwrap(_context);

        VisitNode(node.Test);
        VisitNullableNode(node.Msg);
    }

    private void VisitIf(IfNode node)
    {
        VisitNode(node.Test);
        VisitNodes(node.Body);
        VisitNodes(node.OrElse);
    }

    private void VisitTry(TryNode node)
    {
        for (int i = 0; i < node.Exceptors.Length; i++)
        {
            if (node.Exceptors[i].Type is null && i < node.Exceptors.Length - 1)
                throw SyntaxError(PySR.InvalidSyntax_Semantic_NonLastDefaultExcept);
        }

        VisitNodes(node.Body);
        VisitNodes(node.Exceptors);
        VisitNodes(node.OrElse);

        _currentScopeStats.FinallyDepth++;
        VisitNodes(node.FinalBody);
        _currentScopeStats.FinallyDepth--;
    }

    private void VisitTryStar(TryStarNode node)
    {
        VisitNodes(node.Body);
        VisitNodes(node.Exceptors);
        VisitNodes(node.OrElse);

        _currentScopeStats.FinallyDepth++;
        VisitNodes(node.FinalBody);
        _currentScopeStats.FinallyDepth--;
    }

    private void VisitFor(ForNode node)
    {
        _currentScopeStats.LoopDepth++;
        VisitNode(node.Target);
        VisitNode(node.Iter);
        VisitNodes(node.Body);
        VisitNodes(node.OrElse);
        _currentScopeStats.LoopDepth--;
    }

    private void VisitAsyncFor(AsyncForNode node)
    {
        if (_currentScopeStats.Scope is not AsyncFunctionVariableScope)
            throw SyntaxError(PySR.InvalidSyntax_Semantic_AsyncForOutsideAsyncFunc);

        _currentScopeStats.LoopDepth++;
        VisitNode(node.Target);
        VisitNode(node.Iter);
        VisitNodes(node.Body);
        VisitNodes(node.OrElse);
        _currentScopeStats.LoopDepth--;
    }

    private void VisitWhile(WhileNode node)
    {
        _currentScopeStats.LoopDepth++;
        VisitNode(node.Test);
        VisitNodes(node.Body);
        VisitNodes(node.OrElse);
        _currentScopeStats.LoopDepth--;
    }

    private void VisitWith(WithNode node)
    {
        VisitNodes(node.Items);
        VisitNodes(node.Body);
    }

    private void VisitAsyncWith(AsyncWithNode node)
    {
        if (_currentScopeStats.Scope is not AsyncFunctionVariableScope)
            throw SyntaxError(PySR.InvalidSyntax_Semantic_AsyncWithOutsideAsyncFunc);

        VisitNodes(node.Items);
        VisitNodes(node.Body);
    }

    private void VisitMatch(MatchNode node)
    {
        for (int i = 0; i < node.Cases.Length; i++)
        {
            var c = node.Cases[i];

            CheckDuplicatePatternCaptures(c.Pattern, []);

            var irrefutablePattern = FindIrrefutablePattern(c.Pattern, out var isLast);

            if (irrefutablePattern is null)
                continue;

            if (!isLast)
                ThrowUnreachable(irrefutablePattern);

            if (c.Guard is not null)
                continue;

            if (i < node.Cases.Length - 1)
                ThrowUnreachable(irrefutablePattern);
        }

        MatchAsNode? FindIrrefutablePattern(AstPatternNode pattern, out bool isLast)
        {
            if (pattern is MatchAsNode matchAsNode)
            {
                isLast = true;
                return matchAsNode.Pattern is null ? matchAsNode : null;
            }

            isLast = false;
            if (pattern is not MatchOrNode matchOrNode)
                return null;

            var span = matchOrNode.Patterns.AsSpan();
            for (int i = 0; i < span.Length; i++)
            {
                var result = FindIrrefutablePattern(span[i], out var isSubLast);
                if (result is null)
                    continue;

                isLast = isSubLast && (i == span.Length - 1);
                return result;
            }

            return null;
        }

        void ThrowUnreachable(MatchAsNode irrefutablePattern)
        {
            if (irrefutablePattern.Name is null)
                throw SyntaxError(PySR.InvalidSyntax_Semantic_UnreachablePatterns_Wildcard);

            throw SyntaxError(PySR.InvalidSyntax_Semantic_UnreachablePatterns_Capture, irrefutablePattern.Name);
        }

        // CPython codegen_pattern_helper_store_name: a name may be captured
        // only once per pattern path; each or-pattern alternative has its own
        // capture set.
        void CheckDuplicatePatternCaptures(AstPatternNode pattern, HashSet<string> names)
        {
            switch (pattern)
            {
                case MatchAsNode n:
                    if (n.Pattern is not null)
                        CheckDuplicatePatternCaptures(n.Pattern, names);
                    if (n.Name is not null && !names.Add(n.Name))
                        throw SyntaxError(PySR.InvalidSyntax_Semantic_PatternMultipleAssignments, n.Name);
                    break;

                case MatchStarNode n:
                    if (n.Name is not null && !names.Add(n.Name))
                        throw SyntaxError(PySR.InvalidSyntax_Semantic_PatternMultipleAssignments, n.Name);
                    break;

                case MatchSequenceNode n:
                    foreach (var subPattern in n.Patterns)
                        CheckDuplicatePatternCaptures(subPattern, names);
                    break;

                case MatchMappingNode n:
                    foreach (var subPattern in n.Patterns)
                        CheckDuplicatePatternCaptures(subPattern, names);
                    if (n.Rest is not null && !names.Add(n.Rest))
                        throw SyntaxError(PySR.InvalidSyntax_Semantic_PatternMultipleAssignments, n.Rest);
                    break;

                case MatchClassNode n:
                    foreach (var subPattern in n.Patterns)
                        CheckDuplicatePatternCaptures(subPattern, names);
                    foreach (var subPattern in n.KwdPatterns)
                        CheckDuplicatePatternCaptures(subPattern, names);
                    break;

                case MatchOrNode n:
                    foreach (var subPattern in n.Patterns)
                        CheckDuplicatePatternCaptures(subPattern, []);
                    break;
            }
        }

        VisitNode(node.Subject);
        VisitNodes(node.Cases);
    }

    private void VisitFunctionDef(FunctionDefNode node)
    {
        _currentScopeStats.Scope.AppendVariable(node.Name, ExprContextType.Store);

        VisitNodes(node.DecoratorList);
        VisitArgumentsDefaults(node.Args);

        // Generic functions (def foo[T]():) have an outer GenericParamVariableScope that
        // creates TypeVar cells and passes them to the function body via closure.
        if (node.TypeParams.Length > 0)
        {
            var genericParamScope = new GenericParamVariableScope(node, _currentScopeStats.Scope);
            PushScope(genericParamScope);

            foreach (var tp in node.TypeParams)
                genericParamScope.AppendVariable(tp.Name, ExprContextType.Store);

            var funcScope = new FunctionVariableScope(node, genericParamScope);
            PushScope(funcScope);

            foreach (var tp in node.TypeParams)
                funcScope.AppendVariable(tp.Name, ExprContextType.Load);
        }
        else
        {
            var scope = new FunctionVariableScope(node, _currentScopeStats.Scope);
            PushScope(scope);
        }

        VisitArgumentsArgs(node.Args);
        VisitNodes(node.Body);

        PopScope(); // pop function scope
        if (node.TypeParams.Length > 0)
            PopScope(); // pop generic param scope
    }

    private void VisitAsyncFunctionDef(AsyncFunctionDefNode node)
    {
        _currentScopeStats.Scope.AppendVariable(node.Name, ExprContextType.Store);

        VisitNodes(node.DecoratorList);
        VisitArgumentsDefaults(node.Args);

        if (node.TypeParams.Length > 0)
        {
            var genericParamScope = new GenericParamVariableScope(node, _currentScopeStats.Scope);
            PushScope(genericParamScope);

            foreach (var tp in node.TypeParams)
                genericParamScope.AppendVariable(tp.Name, ExprContextType.Store);

            var funcScope = new AsyncFunctionVariableScope(node, genericParamScope);
            PushScope(funcScope);

            foreach (var tp in node.TypeParams)
                funcScope.AppendVariable(tp.Name, ExprContextType.Load);
        }
        else
        {
            var scope = new AsyncFunctionVariableScope(node, _currentScopeStats.Scope);
            PushScope(scope);
        }

        VisitArgumentsArgs(node.Args);
        VisitNodes(node.Body);

        PopScope(); // pop function scope
        if (node.TypeParams.Length > 0)
            PopScope(); // pop generic param scope
    }

    private void VisitClassDef(ClassDefNode node)
    {
        _currentScopeStats.Scope.AppendVariable(node.Name, ExprContextType.Store);

        VisitNodes(node.DecoratorList);
        VisitNodes(node.Bases);
        VisitNodes(node.Keywords);

        // Generic classes (class C[T]:) have an outer GenericParamVariableScope that creates
        // TypeVar objects and communicates them to the class body via cell/freevar closure.
        if (node.TypeParams.Length > 0)
        {
            var genericParamScope = new GenericParamVariableScope(node, _currentScopeStats.Scope);
            PushScope(genericParamScope);

            // Register each type param as a local in the generic param scope
            foreach (var tp in node.TypeParams)
                genericParamScope.AppendVariable(tp.Name, ExprContextType.Store);

            var classScope = new ClassVariableScope(node, genericParamScope);
            PushScope(classScope);

            // Register type param names as Load references in the class scope so they
            // are recognized as Unknown → Closure → FreeVars. This ensures the emitter
            // can build __type_params__ from LoadDeref even when the body is `pass`.
            foreach (var tp in node.TypeParams)
                classScope.AppendVariable(tp.Name, ExprContextType.Load);
        }
        else
        {
            var classScope = new ClassVariableScope(node, _currentScopeStats.Scope);
            PushScope(classScope);
        }

        VisitNodes(node.Body);

        PopScope(); // pop class scope
        if (node.TypeParams.Length > 0)
            PopScope(); // pop generic param scope
    }

    private void VisitTypeAlias(TypeAliasNode node)
    {
        VisitName(node.Name, ExprContextType.Store);
    }
}