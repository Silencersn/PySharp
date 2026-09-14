using PySharp.Compilation.Primitives;
using PySharp.Modules.Builtins;
using System.Collections.Immutable;
using System.Diagnostics;

namespace PySharp.Compilation.AstNodes;

partial class SemanticAnalyzer
{
    private void VisitExpr(AstExprNode node)
    {
        switch (node)
        {
            case ConstantNode n: VisitConstant(n); break;
            case NameNode n: VisitName(n); break;
            case CallNode n: VisitCall(n); break;
            case BinOpNode n: VisitBinOp(n); break;
            case UnaryOpNode n: VisitUnaryOp(n); break;
            case CompareNode n: VisitCompare(n); break;
            case AttributeNode n: VisitAttribute(n); break;
            case ListNode n: VisitList(n); break;
            case TupleNode n: VisitTuple(n); break;
            case SetNode n: VisitSet(n); break;
            case DictNode n: VisitDict(n); break;
            case ListCompNode n: VisitListComp(n); break;
            case SetCompNode n: VisitSetComp(n); break;
            case DictCompNode n: VisitDictComp(n); break;
            case GeneratorExpNode n: VisitGeneratorExp(n); break;
            case YieldNode n: VisitYield(n); break;
            case YieldFromNode n: VisitYieldFrom(n); break;
            case NamedExprNode n: VisitNamedExpr(n); break;
            case SubscriptNode n: VisitSubscript(n); break;
            case SliceNode n: VisitSlice(n); break;
            case IfExpNode n: VisitIfExp(n); break;
            case LambdaNode n: VisitLambda(n); break;
            case FormattedValueNode n: VisitFormattedValue(n); break;
            case JoinedStrNode n: VisitJoinedStr(n); break;
            case InterpolationNode n: VisitInterpolation(n); break;
            case TemplateStrNode n: VisitTemplateStr(n); break;
            case BoolOpNode n: VisitBoolOp(n); break;
            case StarredNode n: VisitStarred(n); break;
            case AwaitNode n: VisitAwait(n); break;
            default: throw new UnreachableException();
        }
    }

    private void VisitConstant(ConstantNode node)
    {
    }

    private void VisitName(string name, ExprContextType ctx)
    {
        _currentScopeStats.Scope.AppendVariable(name, ctx);
    }

    private void VisitName(NameNode node)
    {
        VisitName(node.Id, node.Ctx);
    }

    private void VisitCall(CallNode node)
    {
        for (int i = 0; i < node.Keywords.Length; i++)
        {
            var currentKeyword = node.Keywords[i];
            if (currentKeyword.Arg is null)
                continue;
            for (int j = 0; j < i; j++)
            {
                var previousKeyword = node.Keywords[j];
                if (previousKeyword.Arg == currentKeyword.Arg)
                    throw SyntaxError(PySR.InvalidSyntax_Semantic_KeywordArgumentRepeated, currentKeyword.Arg);
            }
        }
        VisitNode(node.Func);
        VisitNodes(node.Args);
        VisitNodes(node.Keywords);
    }

    private void VisitBinOp(BinOpNode node)
    {
        if (node.Left is not BinOpNode)
        {
            VisitNode(node.Left);
            VisitNode(node.Right);
            return;
        }

        var currentNode = node;
        while (currentNode.Left is BinOpNode leftBinOpNode)
        {
            PreVisitNode(leftBinOpNode);
            currentNode = leftBinOpNode;
        }
        VisitNode(currentNode.Left);
        while (!ReferenceEquals(currentNode, node))
        {
            VisitNode(currentNode.Right);
            PostVisitNode(currentNode);
            currentNode = (BinOpNode)_nodesToRoot.Peek();
        }
        VisitNode(node.Right);
    }

    private void VisitUnaryOp(UnaryOpNode node)
    {
        VisitNode(node.Operand);
    }

    private void VisitCompare(CompareNode node)
    {
        WarnIdentityLiteralComparison(node);
        VisitNode(node.Left);
        VisitNodes(node.Comparators);
    }

    // CPython: codegen_check_compare — an identity check with a constant
    // literal operand (except the named singletons) warns about the value
    // comparison operators
    private void WarnIdentityLiteralComparison(CompareNode node)
    {
        var left = node.Left;
        bool leftIsArg = IsIdentityCheckArg(left);

        for (int i = 0; i < node.Ops.Length; i++)
        {
            var op = node.Ops[i];
            var right = node.Comparators[i];
            bool rightIsArg = IsIdentityCheckArg(right);

            if (op is CmpopType.Is or CmpopType.IsNot && (!rightIsArg || !leftIsArg))
            {
                var literal = leftIsArg ? right : left;
                var message = op is CmpopType.Is
                    ? PySR.Format(PySR.InvalidSyntax_Warning_IsWithLiteral, GetLiteralTypeName(literal))
                    : PySR.Format(PySR.InvalidSyntax_Warning_IsNotWithLiteral, GetLiteralTypeName(literal));
                _ = _context.WarnSyntax(message, this).PyUnwrap(_context);
                // CPython returns on the first warning, so a comparison warns
                // at most once even with several offending operands
                return;
            }

            left = right;
            leftIsArg = rightIsArg;
        }
    }

    // CPython: check_is_arg — false when the operand is a constant literal
    // (constant tuples included) other than None/True/False/Ellipsis
    private static bool IsIdentityCheckArg(AstExprNode node)
    {
        if (node is TupleNode tuple)
            return tuple.Elts.Any(static elt => elt is not ConstantNode);
        if (node is not ConstantNode constant)
            return true;
        return constant.Value is PyNoneObject or PyBoolObject or PyEllipsisObject;
    }

    private static string GetLiteralTypeName(AstExprNode node)
    {
        return node is TupleNode ? "tuple" : ((ConstantNode)node).Value.PyType.Name;
    }

    private void VisitAttribute(AttributeNode node)
    {
        VisitNode(node.Value);
    }

    private void VisitList(ListNode node)
    {
        if (node.Ctx is ExprContextType.Store)
            ValidateNonMultipleStarred(node.Elts);
        VisitNodes(node.Elts);
    }

    private void VisitTuple(TupleNode node)
    {
        if (node.Ctx is ExprContextType.Store)
            ValidateNonMultipleStarred(node.Elts);
        VisitNodes(node.Elts);
    }

    private void ValidateNonMultipleStarred(ImmutableArray<AstExprNode> targets)
    {
        if (targets.Count(static node => node is StarredNode) > 1)
            throw SyntaxError(PySR.InvalidSyntax_Semantic_MultipleStarredInAssignment);
    }

    private void VisitSet(SetNode node)
    {
        VisitNodes(node.Elts);
    }

    private void VisitDict(DictNode node)
    {
        VisitNullableNodes(node.Keys);
        VisitNodes(node.Values);
    }

    private void VisitListComp(ListCompNode node)
    {
        _currentNestedComprehensionStats.PushComprehension(node);
        VisitInlineComprehension(node, node.Generators, () => VisitNode(node.Elt));
        _currentNestedComprehensionStats.PopComprehension();
    }

    private void VisitSetComp(SetCompNode node)
    {
        _currentNestedComprehensionStats.PushComprehension(node);
        VisitInlineComprehension(node, node.Generators, () => VisitNode(node.Elt));
        _currentNestedComprehensionStats.PopComprehension();
    }

    private void VisitDictComp(DictCompNode node)
    {
        _currentNestedComprehensionStats.PushComprehension(node);
        VisitInlineComprehension(node, node.Generators, () =>
        {
            VisitNode(node.Key);
            VisitNode(node.Value);
        });
        _currentNestedComprehensionStats.PopComprehension();
    }

    // The comprehension body is a nested scope that skips the class block:
    // class scopes are invisible to nested scopes, and only the outermost
    // iterable is evaluated in the enclosing class scope (CPython symtable
    // rule). In any other scope the inlined form is kept and body names
    // simply merge into the enclosing scope.
    private void VisitInlineComprehension(AstExprNode node, ImmutableArray<AstComprehensionNode> generators, Action visitElement)
    {
        if (_currentScopeStats.Scope is not ClassVariableScope classScope)
        {
            _currentNestedComprehensionStats.CurrentComprehensionStats.VisitingPart = ComprehensionStatsVisitingPart.Element;
            visitElement();
            VisitNodes(generators);
            return;
        }

        var comprehensionScope = new ComprehensionVariableScope(node, classScope);
        PushComprehensionScope(comprehensionScope);
        try
        {
            _currentNestedComprehensionStats.CurrentComprehensionStats.VisitingPart = ComprehensionStatsVisitingPart.Element;
            visitElement();

            for (int i = 0; i < generators.Length; i++)
            {
                var generator = generators[i];
                _nodesToRoot.Push(generator);

                if (generator.IsAsync)
                    throw SyntaxError(PySR.InvalidSyntax_Semantic_AsyncCompOutsideAsyncFunc);

                _currentNestedComprehensionStats.CurrentComprehensionStats.VisitingPart = ComprehensionStatsVisitingPart.GeneratorTarget;
                VisitNode(generator.Target);

                _currentNestedComprehensionStats.CurrentComprehensionStats.VisitingPart = ComprehensionStatsVisitingPart.GeneratorIter;
                if (i is 0)
                {
                    // the outermost iterable is evaluated in the class scope
                    PopComprehensionScope();
                    VisitNode(generator.Iter);
                    PushComprehensionScope(comprehensionScope);
                }
                else
                {
                    VisitNode(generator.Iter);
                }

                _currentNestedComprehensionStats.CurrentComprehensionStats.VisitingPart = ComprehensionStatsVisitingPart.GeneratorIfs;
                VisitNodes(generator.Ifs);

                _currentNestedComprehensionStats.CurrentComprehensionStats.VisitingPart = ComprehensionStatsVisitingPart.None;
                _nodesToRoot.Pop();
            }
        }
        finally
        {
            PopComprehensionScope();
        }
    }

    private void VisitGeneratorExp(GeneratorExpNode node)
    {
        const string FirstIterVarName = ".0";

        _currentNestedComprehensionStats.PushComprehension(node);

        _currentNestedComprehensionStats.CurrentComprehensionStats.VisitingPart = ComprehensionStatsVisitingPart.GeneratorIter;
        var generators = node.Generators;
        Debug.Assert(generators.Length > 0);
        // first iter is passed as an argument named '.0'
        VisitNode(generators[0].Iter);
        _currentNestedComprehensionStats.CurrentComprehensionStats.VisitingPart = ComprehensionStatsVisitingPart.None;

        var scope = new GeneratorExpVariableScope(node, _currentScopeStats.Scope);
        PushScope(scope);

        AddParameter(FirstIterVarName);

        _currentNestedComprehensionStats.CurrentComprehensionStats.VisitingPart = ComprehensionStatsVisitingPart.Element;
        VisitNode(node.Elt);
        for (int i = 0; i < generators.Length; i++)
        {
            var gen = generators[i];

            _nodesToRoot.Push(gen);

            _currentNestedComprehensionStats.CurrentComprehensionStats.VisitingPart = ComprehensionStatsVisitingPart.GeneratorTarget;
            VisitNode(gen.Target);

            _currentNestedComprehensionStats.CurrentComprehensionStats.VisitingPart = ComprehensionStatsVisitingPart.GeneratorIter;
            if (i is 0)
                VisitName(FirstIterVarName, ExprContextType.Load);
            else
                VisitNode(gen.Iter);

            _currentNestedComprehensionStats.CurrentComprehensionStats.VisitingPart = ComprehensionStatsVisitingPart.GeneratorIfs;
            VisitNodes(gen.Ifs);
            _currentNestedComprehensionStats.CurrentComprehensionStats.VisitingPart = ComprehensionStatsVisitingPart.None;

            _nodesToRoot.Pop();
        }

        PopScope();

        _currentNestedComprehensionStats.PopComprehension();
    }

    private void VisitYield(YieldNode node)
    {
        if (_currentNestedComprehensionStats.IsWithinComprehension)
        {
            throw SyntaxError(PySR.InvalidSyntax_Semantic_YieldInsideComprehension,
                AstUtils.GetExprNodeName(_currentNestedComprehensionStats.CurrentComprehension));
        }

        if (_currentScopeStats.Scope is AsyncFunctionVariableScope asyncYieldScope)
        {
            asyncYieldScope.IsGenerator = true;
            asyncYieldScope.IsAsyncGenerator = true;
        }
        else
        {
            Debug.Assert(_currentScopeStats.Scope is not GeneratorExpVariableScope);
            if (_currentScopeStats.Scope is not CallableVariableScope callableYieldScope)
                throw SyntaxError(PySR.InvalidSyntax_Semantic_YieldOutsideFunction);
            callableYieldScope.IsGenerator = true;
        }
        VisitNullableNode(node.Value);
    }

    private void VisitYieldFrom(YieldFromNode node)
    {
        if (_currentNestedComprehensionStats.IsWithinComprehension)
        {
            throw SyntaxError(PySR.InvalidSyntax_Semantic_YieldFromInsideComprehension,
                AstUtils.GetExprNodeName(_currentNestedComprehensionStats.CurrentComprehension));
        }

        if (_currentScopeStats.Scope is AsyncFunctionVariableScope)
            throw SyntaxError(PySR.InvalidSyntax_Semantic_YieldFromInsideAsyncFunc);

        Debug.Assert(_currentScopeStats.Scope is not GeneratorExpVariableScope);
        if (_currentScopeStats.Scope is not CallableVariableScope callableYieldFromScope
            /* this callable scope is never genexpr because of the first if */)
            throw SyntaxError(PySR.InvalidSyntax_Semantic_YieldFromOutsideFunction);

        callableYieldFromScope.IsGenerator = true;
        VisitNode(node.Value);
    }

    private void VisitNamedExpr(NamedExprNode node)
    {
        if ((_currentScopeStats.Scope is ClassVariableScope && _currentNestedComprehensionStats.IsWithinComprehension)
            || _currentScopeStats.Scope is ComprehensionVariableScope)
            throw SyntaxError(PySR.InvalidSyntax_Semantic_NamedExprInComprehensionInClass);

        CheckNamedExprIfWithinComprehension(node.Target.Id);

        // PEP 572: a genexp target binds in the nearest enclosing
        // non-comprehension scope (an enclosing function local via a
        // shared cell, or the global scope), never in the genexp itself.
        if (_currentScopeStats.Scope is GeneratorExpVariableScope generatorExpScope)
            BindNamedExprTargetInEnclosingScope(generatorExpScope, node.Target.Id);
        else
            VisitNode(node.Target);

        VisitNode(node.Value);
    }

    private void BindNamedExprTargetInEnclosingScope(GeneratorExpVariableScope scope, string name)
    {
        var parent = scope.Parent;
        while (parent is GeneratorExpVariableScope or ComprehensionVariableScope)
            parent = parent.Parent;

        switch (parent)
        {
            case ClassVariableScope:
                throw SyntaxError(PySR.InvalidSyntax_Semantic_NamedExprInComprehensionInClass);

            case CallableVariableScope callableScope:
                callableScope.AppendVariable(name, ExprContextType.Store);
                if (callableScope.Variables[name] is PyVariableType.Global)
                    break; // global-declared in the owner: the genexp stores the global

                // Reference the owner's cell as a free variable; the closure
                // pass promotes the owner's local to a captured cell.
                scope.Variables[name] = PyVariableType.Closure;
                break;

            default:
                // module scope: the genexp stores the global
                break;
        }
    }

    private void CheckNamedExprIfWithinComprehension(string name)
    {
        if (!_currentNestedComprehensionStats.IsWithinComprehension)
            return;

        CheckNamedExprWithinComprehension(name, _currentNestedComprehensionStats.CurrentComprehensionStats);
        foreach (var stats in _currentNestedComprehensionStats.ComprehensionStatsStack)
        {
            if (stats.Node is null)
                // it is the root stats
                break;

            CheckNamedExprWithinComprehension(name, stats);
        }
    }

    private void CheckNamedExprWithinComprehension(string name, ComprehensionStats stats)
    {
        Debug.Assert(stats.Node is not null);

        var generators = stats.Node switch
        {
            ListCompNode n => n.Generators,
            SetCompNode n => n.Generators,
            DictCompNode n => n.Generators,
            GeneratorExpNode n => n.Generators,
            _ => throw new UnreachableException()
        };

        switch (stats.VisitingPart)
        {
            case ComprehensionStatsVisitingPart.Element:
                {
                    foreach (var generator in generators)
                    {
                        if (ContainsName(generator.Target, name))
                            throw SyntaxError(PySR.InvalidSyntax_Semantic_NamedExprRebindCompIterVar, name);
                    }
                }
                break;

            case ComprehensionStatsVisitingPart.GeneratorTarget:
                throw new UnreachableException("handled by parser");

            case ComprehensionStatsVisitingPart.GeneratorIter:
                throw SyntaxError(PySR.InvalidSyntax_Semantic_NamedExprUsedInCompIter);

            case ComprehensionStatsVisitingPart.GeneratorIfs:
                {
                    AstComprehensionNode? generator = null;
                    int index = -1;
                    foreach (var n in _nodesToRoot)
                    {
                        if (n is not AstComprehensionNode comp)
                            continue;

                        index = generators.IndexOf(comp);
                        if (index >= 0)
                        {
                            generator = comp;
                            break;
                        }
                    }
                    Debug.Assert(generator is not null);
                    Debug.Assert(index >= 0);

                    for (int i = 0; i < generators.Length; i++)
                    {
                        if (i <= index)
                        {
                            if (ContainsName(generators[i].Target, name))
                                throw SyntaxError(PySR.InvalidSyntax_Semantic_NamedExprRebindCompIterVar, name);
                        }
                        else
                        {
                            if (ContainsName(generators[i].Target, name))
                                throw SyntaxError(PySR.InvalidSyntax_Semantic_CompIterVarRebindNamedExpr, name);
                        }
                    }
                }
                break;

            default:
                throw new UnreachableException();
        }

        static bool ContainsName(AstExprNode target, string varName)
        {
            switch (target)
            {
                case NameNode n:
                    return n.Id == varName;

                case TupleNode n:
                    foreach (var elt in n.Elts)
                    {
                        if (ContainsName(elt, varName))
                            return true;
                    }
                    break;

                case ListNode n:
                    foreach (var elt in n.Elts)
                    {
                        if (ContainsName(elt, varName))
                            return true;
                    }
                    break;

                case StarredNode n:
                    return ContainsName(n.Value, varName);
            }
            return false;
        }
    }

    private void VisitSubscript(SubscriptNode node)
    {
        VisitNode(node.Value);
        VisitNode(node.Slice);
    }

    private void VisitSlice(SliceNode node)
    {
        VisitNullableNode(node.Lower);
        VisitNullableNode(node.Upper);
        VisitNullableNode(node.Step);
    }

    private void VisitIfExp(IfExpNode node)
    {
        VisitNode(node.Test);
        VisitNode(node.Body);
        VisitNode(node.OrElse);
    }

    private void VisitLambda(LambdaNode node)
    {
        VisitArgumentsDefaults(node.Args);

        var scope = new LambdaVariableScope(node, _currentScopeStats.Scope);
        PushScope(scope);

        VisitArgumentsArgs(node.Args);
        VisitNode(node.Body);

        PopScope();
    }

    private void VisitFormattedValue(FormattedValueNode node)
    {
        VisitNode(node.Value);
        VisitNullableNode(node.FormatSpec);
    }

    private void VisitJoinedStr(JoinedStrNode node)
    {
        VisitNodes(node.Values);
    }

    private void VisitInterpolation(InterpolationNode node)
    {
        VisitNode(node.Value);
        VisitNullableNode(node.FormatSpec);
    }

    private void VisitTemplateStr(TemplateStrNode node)
    {
        VisitNodes(node.Values);
    }

    private void VisitBoolOp(BoolOpNode node)
    {
        VisitNodes(node.Values);
    }

    private void VisitStarred(StarredNode node)
    {
        // _nodesToRoot enumerates top-first, so index 1 is the starred node's direct parent.
        var parent = _nodesToRoot.ElementAtOrDefault(1);
        if (parent is not (ListNode or TupleNode or SetNode or CallNode))
        {
            throw SyntaxError(node.Ctx is ExprContextType.Store
                ? PySR.InvalidSyntax_StarredExpression_TargetMustBeInListOrTuple
                : PySR.InvalidSyntax_StarredExpression_CannotUseHere);
        }

        VisitNode(node.Value);
    }

    private void VisitAwait(AwaitNode node)
    {
        if (_currentScopeStats.Scope is not AsyncFunctionVariableScope)
            throw SyntaxError(PySR.InvalidSyntax_Semantic_AwaitOutsideAsyncFunc);

        VisitNode(node.Value);
    }
}
