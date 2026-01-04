using PySharp.PyRuntime.Calls;
using System.Collections.Frozen;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace PySharp.AstNodes;

public sealed class SemanticAnalyzer
{
    public static void Analyze(PyCallContext context, AstModNode root)
    {
        if (root.VariableScope is not null)
            return;

        var scope = InternalAnalyze(context, root);
        scope.Bind();
    }

    internal static RootVariableScope InternalAnalyze(PyCallContext context, AstModNode root)
    {
        var analyzer = new SemanticAnalyzer(context);
        var scope = analyzer.BuildBasicScope(root);
        FillUnknownVariables(scope);
        analyzer.CheckClosureAndFillCapturedVariables(scope);
        FillCallableProperties(scope);
        return scope;
    }

    private readonly PyCallContext _context;

    private SemanticAnalyzer(PyCallContext context)
    {
        _context = context;
    }

    internal RootVariableScope BuildBasicScope(AstModNode root)
    {
        Stack<VariableScope> scopeStack = [];
        var rootScope = new RootVariableScope(root);
        VariableScope currentScope = rootScope;

        Stack<int> loopDepthStack = [];
        int currentLoopDepth = 0;

        Stack<AstExprNode?> comprehensionStack = [];
        AstExprNode? currentComprehension = null;

        foreach (var subNode in root.EnumerateSubNodes())
            BuildBasicScopeImpl(subNode);
        Debug.Assert(scopeStack.Count is 0);
        return rootScope;

        void BuildBasicScopeImpl(AstNode node)
        {
            CheckValid(node);
            TryAppendVariableTo(currentScope, node);

            VariableScope? scope = node switch
            {
                ModuleNode n => throw new UnreachableException(),
                ClassDefNode n => new ClassVariableScope(n, currentScope),
                FunctionDefNode n => new FunctionVariableScope(n, currentScope),
                LambdaNode n => new LambdaVariableScope(n, currentScope),
                _ => null
            };

            if (scope is not null)
            {
                if (node is IScopedSubNodesProvider provider)
                {
                    foreach (var subNode in provider.EnumerateSubNodesOuterScope())
                        BuildBasicScopeImpl(subNode);
                }

                scopeStack.Push(currentScope);
                currentScope = scope;

                loopDepthStack.Push(currentLoopDepth);
                currentLoopDepth = 0;
            }

            if (node is ForNode or WhileNode)
            {
                currentLoopDepth++;
            }

            if (node is ListCompNode or SetCompNode or DictCompNode or GeneratorExpNode)
            {
                comprehensionStack.Push(currentComprehension);
                currentComprehension = (AstExprNode)node;
            }

            {
                if (node is IScopedSubNodesProvider provider)
                {
                    foreach (var subNode in provider.EnumerateSubNodesInnerScope())
                        BuildBasicScopeImpl(subNode);
                }
                else
                {
                    foreach (var subNode in node.EnumerateSubNodes())
                        BuildBasicScopeImpl(subNode);
                }
            }

            if (node is ListCompNode or SetCompNode or DictCompNode or GeneratorExpNode)
            {
                currentComprehension = comprehensionStack.Pop();
            }

            if (node is ForNode or WhileNode)
            {
                currentLoopDepth--;
            }

            if (scope is not null)
            {
                currentScope = scopeStack.Pop();

                Debug.Assert(currentComprehension is null);

                Debug.Assert(currentLoopDepth is 0);
                currentLoopDepth = loopDepthStack.Pop();
            }
        }

        void CheckValid(AstNode node)
        {
            // TOTO: no return / continue / break in finally

            switch (node)
            {
                case BreakNode:
                    if (currentLoopDepth is 0)
                        throw _context.ThrowableSyntaxError("'break' outside loop");
                    break;

                case ContinueNode:
                    if (currentLoopDepth is 0)
                        throw _context.ThrowableSyntaxError("'continue' outside loop");
                    break;

                case ReturnNode:
                    if (currentScope is not FunctionVariableScope)
                        throw _context.ThrowableSyntaxError("'return' outside function");
                    break;

                case YieldNode:
                    if (currentComprehension is not null)
                        throw _context.ThrowableSyntaxError($"'yield' inside {AstUtils.GetExprNodeName(currentComprehension)}");

                    if (currentScope is not CallableVariableScope callableYieldScope)
                        throw _context.ThrowableSyntaxError("'yield' outside function");

                    callableYieldScope.HasYield = true;
                    break;

                case YieldFromNode:
                    if (currentComprehension is not null)
                        throw _context.ThrowableSyntaxError($"'yield from' inside {AstUtils.GetExprNodeName(currentComprehension)}");

                    if (currentScope is not CallableVariableScope callableYieldFromScope)
                        throw _context.ThrowableSyntaxError("'yield from' outside function");

                    callableYieldFromScope.HasYield = true;
                    break;

                case CallNode n:
                    for (int i = 0; i < n.Keywords.Length; i++)
                    {
                        var currentKeyword = n.Keywords[i];
                        foreach (var previousKeyword in n.Keywords.Take(i))
                        {
                            if (previousKeyword.Arg == currentKeyword.Arg)
                                throw _context.ThrowableSyntaxError($"keyword argument repeated: {currentKeyword.Arg}");
                        }
                    }
                    break;
            }
        }
    }

    internal void TryAppendVariableTo(VariableScope currentScope, AstNode node)
    {
        switch (node)
        {
            case NameNode n:
                currentScope.AppendVariable(n.Id, n.Ctx);
                break;

            case FunctionDefNode n:
                currentScope.AppendVariable(n.Name, ExprContext.Store);
                break;

            case ClassDefNode n:
                currentScope.AppendVariable(n.Name, ExprContext.Store);
                break;

            case ImportFromNode n when n.Module is not null:
                currentScope.AppendVariable(n.Module, ExprContext.Store);
                break;

            case GlobalNode n:
                if (currentScope.IsRoot)
                    break;

                foreach (var name in n.Names)
                {
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
                            throw _context.ThrowableSyntaxError($"name '{name}' is parameter and global");

                        case PyVariableType.Nonlocal:
                            throw _context.ThrowableSyntaxError($"name '{name}' is nonlocal and global");

                        default:
                            if (currentScope.FirstContext[name] is ExprContext.Load)
                                throw _context.ThrowableSyntaxError($"name '{name}' is used prior to global declaration");
                            else
                                throw _context.ThrowableSyntaxError($"name '{name}' is assigned to before global declaration");
                    }
                }

                break;

            case NonlocalNode n:
                if (currentScope.IsRoot)
                    throw _context.ThrowableSyntaxError("nonlocal declaration not allowed at module level");

                foreach (var name in n.Names)
                {
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
                            throw _context.ThrowableSyntaxError($"name '{name}' is parameter and nonlocal");

                        case PyVariableType.Global:
                            throw _context.ThrowableSyntaxError($"name '{name}' is nonlocal and global");

                        default:
                            if (currentScope.FirstContext[name] is ExprContext.Load)
                                throw _context.ThrowableSyntaxError($"name '{name}' is used prior to nonlocal declaration");
                            else
                                throw _context.ThrowableSyntaxError($"name '{name}' is assigned to before nonlocal declaration");
                    }
                }

                break;

            case ExceptHandlerNode n when n.Name is not null:
                currentScope.AppendVariable(n.Name, ExprContext.Store);
                break;

            case AstArgNode n:
                if (currentScope.Variables.ContainsKey(n.Arg))
                    throw _context.ThrowableSyntaxError($"duplicate argument '{n.Arg}' in function definition");
                currentScope.Variables[n.Arg] = PyVariableType.Parameter;
                break;

            case AstAliasNode n:
                currentScope.AppendVariable(n.AsName ?? n.Name, ExprContext.Store);
                break;
        }
    }

    internal static void FillUnknownVariables(RootVariableScope root)
    {
        FillUnknownVariablesImpl(root);

        static void FillUnknownVariablesImpl(VariableScope scope)
        {
            foreach (var (name, type) in scope.Variables)
            {
                if (type is not PyVariableType.Unknown)
                    continue;

                var parent = scope.Parent;
                while (true)
                {
                    if (parent is null)
                    {
                        scope.Variables[name] = PyVariableType.Global;
                        break;
                    }

                    if (parent is CallableVariableScope &&
                        parent.Variables.TryGetValue(name, out var typeOfParentVariable))
                    {
                        scope.Variables[name] = typeOfParentVariable is PyVariableType.Global
                            ? PyVariableType.Global : PyVariableType.Closure;
                        break;
                    }

                    parent = parent.Parent;
                }
            }

            foreach (var childScope in scope.Children)
                FillUnknownVariablesImpl(childScope);
        }
    }

    internal void CheckClosureAndFillCapturedVariables(RootVariableScope root)
    {
        CheckClosureAndFillCapturedVariablesImpl(root);

        void CheckClosureAndFillCapturedVariablesImpl(VariableScope scope)
        {
            foreach (var (name, type) in scope.Variables)
            {
                if (type is not PyVariableType.Closure)
                    continue;

                var parent = scope.Parent;
                while (true)
                {
                    if (parent is null)
                        throw _context.ThrowableSyntaxError($"no binding for nonlocal '{name}' found");

                    if (parent is CallableVariableScope &&
                        parent.Variables.TryGetValue(name, out var typeOfParentVariable) &&
                        typeOfParentVariable is not PyVariableType.Closure)
                    {
                        parent.AppendCapturedVariable(name);
                        break;
                    }

                    parent = parent.Parent;
                }
            }

            foreach (var childScope in scope.Children)
                CheckClosureAndFillCapturedVariablesImpl(childScope);
        }
    }

    internal static void FillCallableProperties(RootVariableScope scope)
    {
        FillCallablePropertiesImpl(scope);

        static void FillCallablePropertiesImpl(VariableScope scope)
        {
            foreach (var child in scope.Children)
                FillCallablePropertiesImpl(child);

            if (scope is not CallableVariableScope callableScope)
                return;

            callableScope.HasSuper = HasSuper(callableScope);
            callableScope.VarNames = [.. callableScope.Variables
                .Where(pair => pair.Value is PyVariableType.Local or PyVariableType.Parameter)
                .Select(pair => pair.Key)];
            callableScope.LocalsTable = callableScope.VarNames
                .Index()
                .ToFrozenDictionary(static indexed => indexed.Item, static indexed => indexed.Index);
        }

        static bool HasSuper(CallableVariableScope callableScope)
        {
            if (callableScope.Variables.TryGetValue("super", out var type) &&
                type is not (PyVariableType.Parameter or PyVariableType.CapturedParameter))
                return true;

            return callableScope.Children.OfType<CallableVariableScope>().Any(HasSuper);
        }
    }
}

internal abstract class VariableScope
{
    public abstract AstNode Owner { get; }
    public OrderedDictionary<string, PyVariableType> Variables { get; } = [];
    public HashSet<string> CapturedVariables { get; } = [];

    // used for detecting global stmt and nonlocal stmt
    // root scope does not need to maintain this property
    internal Dictionary<string, ExprContext> FirstContext { get; } = []; 

    public VariableScope? Parent { get; }
    public List<VariableScope> Children { get; } = [];

    [MemberNotNullWhen(false, nameof(Parent), nameof(Name), nameof(QualName))]
    public bool IsRoot => Parent is null;

    public abstract string? Name { get; }
    public string? QualName
    {
        get
        {
            if (IsRoot)
                return null;

            if (field is null)
            {
                Stack<string> nameToRoot = [];
                nameToRoot.Push(Name);

                var currentName = Name;
                var parent = Parent;
                while (!parent.IsRoot && parent.Variables[currentName] is not PyVariableType.Global)
                {
                    if (parent is CallableVariableScope)
                        nameToRoot.Push("<locals>");
                    nameToRoot.Push(parent.Name);

                    currentName = parent.Name;
                    parent = parent.Parent;
                }
                field = string.Join('.', nameToRoot);
            }

            return field;
        }
    }

    protected VariableScope(VariableScope? parent)
    {
        Parent = parent;
        Parent?.Children.Add(this);
    }

    public void AppendVariable(string name, ExprContext ctx)
    {
        if (IsRoot)
        {
            Variables[name] = PyVariableType.Global;
            return;
        }

        FirstContext.TryAdd(name, ctx);
        switch (ctx)
        {
            case ExprContext.Load:
                Variables.TryAdd(name, PyVariableType.Unknown);
                break;

            case ExprContext.Store:
            case ExprContext.Del:
                if (!Variables.TryGetValue(name, out var type) || type is PyVariableType.Unknown)
                    Variables[name] = PyVariableType.Local;
                break;

            default:
                throw new UnreachableException();
        }
    }

    public void AppendCapturedVariable(string name)
    {
        if (!CapturedVariables.Add(name))
            return;

        var type = Variables[name];
        Debug.Assert(type is PyVariableType.Local or PyVariableType.Parameter);
        Variables[name] = type is PyVariableType.Local
            ? PyVariableType.CapturedLocal : PyVariableType.CapturedParameter;
    }

    public void Bind()
    {
        BindToOwner();
        foreach (var childScope in Children)
            childScope.Bind();
    }

    public abstract void BindToOwner();
}

internal sealed class RootVariableScope : VariableScope
{
    public override AstModNode Owner { get; }
    public override string? Name => null;

    public RootVariableScope(AstModNode owner) : base(null)
    {
        Owner = owner;
    }

    public override void BindToOwner()
    {
        if (Owner.VariableScope is not null && !ReferenceEquals(Owner.VariableScope, this))
            throw new InvalidOperationException();

        Owner.VariableScope = this;
    }
}

internal sealed class ClassVariableScope : VariableScope
{
    public override ClassDefNode Owner { get; }
    public override string? Name => Owner.Name;

    public ClassVariableScope(ClassDefNode owner, VariableScope parent) : base(parent)
    {
        Owner = owner;
    }

    public override void BindToOwner()
    {
        if (Owner.VariableScope is not null && !ReferenceEquals(Owner.VariableScope, this))
            throw new InvalidOperationException();

        Owner.VariableScope = this;
    }
}

internal abstract class CallableVariableScope : VariableScope
{
    internal abstract AstArgumentsNode ArgumentsNode { get; }
    public bool HasSuper { get; internal set; }
    public bool HasYield { get; internal set; }
    public FrozenDictionary<string, int> LocalsTable { get; internal set; } = FrozenDictionary<string, int>.Empty;
    public string[] VarNames { get; internal set; } = [];

    protected CallableVariableScope(VariableScope? parent) : base(parent)
    {
    }
}

internal sealed class FunctionVariableScope : CallableVariableScope
{
    internal override AstArgumentsNode ArgumentsNode => Owner.Args;
    public override FunctionDefNode Owner { get; }
    public override string? Name => Owner.Name;

    public FunctionVariableScope(FunctionDefNode owner, VariableScope parent) : base(parent)
    {
        Owner = owner;
    }

    public override void BindToOwner()
    {
        if (Owner.VariableScope is not null && !ReferenceEquals(Owner.VariableScope, this))
            throw new InvalidOperationException();

        Owner.VariableScope = this;
    }
}

internal sealed class LambdaVariableScope : CallableVariableScope
{
    internal override AstArgumentsNode ArgumentsNode => Owner.Args;
    public override LambdaNode Owner { get; }
    public override string? Name => "<lambda>";

    public LambdaVariableScope(LambdaNode owner, VariableScope parent) : base(parent)
    {
        Owner = owner;
    }

    public override void BindToOwner()
    {
        if (Owner.VariableScope is not null && !ReferenceEquals(Owner.VariableScope, this))
            throw new InvalidOperationException();

        Owner.VariableScope = this;
    }
}