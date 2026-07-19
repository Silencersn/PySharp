using PySharp.Compilation.Primitives;
using PySharp.Modules.Builtins;
using PySharp.Runtime;
using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace PySharp.Compilation.AstNodes;

public sealed class SemanticModel
{
    private readonly Dictionary<AstNode, VariableScope> _nodeToScope = [];
    private readonly AstModNode _root;

    internal AstModNode Root => _root;

    internal SemanticModel(AstModNode root)
    {
        _root = root;
    }

    internal void AppendScope(AstNode node, VariableScope scope)
    {
        _nodeToScope.Add(node, scope);
    }

    internal T? GetVariableScope<T>(AstNode node) where T : VariableScope
    {
        if (!_nodeToScope.TryGetValue(node, out var scope))
            return null;

        return scope as T;
    }
}

internal abstract class VariableScope
{
    public abstract AstNode Owner { get; }
    public OrderedDictionary<string, PyVariableType> Variables { get; } = [];

    // used for detecting global stmt and nonlocal stmt
    // root scope does not need to maintain this property
    internal Dictionary<string, ExprContextType> FirstContext { get; } = [];

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
                while (!parent.IsRoot && (currentName is "<lambda>" or "<genexpr>" || !parent.Variables.TryGetValue(currentName, out var varType) || varType is not PyVariableType.Global))
                {
                    if (parent is CallableVariableScope)
                        nameToRoot.Push("<locals>");

                    // Skip transparent scopes (e.g. GenericParamVariableScope) for naming
                    if (parent is not GenericParamVariableScope)
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

    public void AppendVariable(string name, ExprContextType ctx)
    {
        if (IsRoot)
        {
            Variables[name] = PyVariableType.Global;
            return;
        }

        FirstContext.TryAdd(name, ctx);
        switch (ctx)
        {
            case ExprContextType.Load:
                Variables.TryAdd(name, PyVariableType.Unknown);
                if (name is "super" && this is CallableVariableScope)
                {
                    var parent = Parent;
                    while (true)
                    {
                        if (parent is null)
                            break;

                        if (parent is not CallableVariableScope)
                        {
                            if (parent is ClassVariableScope)
                                AppendVariable(PySpecialNames.Class, ExprContextType.Load);
                            break;
                        }

                        parent = parent.Parent;
                    }
                }
                break;

            case ExprContextType.Store:
            case ExprContextType.Del:
                if (!Variables.TryGetValue(name, out var type) || type is PyVariableType.Unknown)
                    Variables[name] = PyVariableType.Local;
                break;

            default:
                throw new UnreachableException();
        }
    }

    public virtual void Bind(SemanticModel model)
    {
        model.AppendScope(Owner, this);

        foreach (var childScope in Children)
            childScope.Bind(model);
    }
}

internal sealed class RootVariableScope : VariableScope
{
    public override AstModNode Owner { get; }
    public override string? Name => null;

    public RootVariableScope(AstModNode owner) : base(null)
    {
        Owner = owner;
    }
}

internal interface IScopeWithFreeVars
{
    public List<string> TempFrees { get; }
}

/// <summary>
/// Scope for the generic parameters of a generic class (e.g. the <c>[T]</c> in <c>class C[T]:</c>).
/// Sits between the enclosing scope and <see cref="ClassVariableScope"/>.
/// TypeVar objects are created in this scope and communicated to the class body via cell/freevar closure.
/// Corresponds to CPython's annotation scope (<c>COMPILE_SCOPE_ANNOTATIONS</c>) for type params.
/// </summary>
internal sealed class GenericParamVariableScope : VariableScope, IScopeWithFreeVars
{
    public override ClassDefNode Owner { get; }
    public override string Name => $"<generic parameters of {Owner.Name}>";
    public Dictionary<string, HashSet<IScopeWithFreeVars>> ScopesRequiringFree = [];
    public PyCodeObject? CodeObject { get; set; }
    public List<string> TempFrees { get; } = [];
    public ImmutableArray<string> CellVars { get; internal set; } = [];
    public ImmutableArray<string> FreeVars { get; internal set; } = [];
    public ImmutableArray<string> VarNames { get; internal set; } = [];
    public FrozenDictionary<string, int> LocalsTable { get; internal set; } = FrozenDictionary<string, int>.Empty;

    public GenericParamVariableScope(ClassDefNode owner, VariableScope parent) : base(parent)
    {
        Owner = owner;
    }

    public override void Bind(SemanticModel model)
    {
        // GenericParamVariableScope shares its Owner (ClassDefNode) with ClassVariableScope.
        // Skip self-registration to avoid duplicate key conflict in SemanticModel.
        foreach (var childScope in Children)
            childScope.Bind(model);
    }
}

internal sealed class ClassVariableScope : VariableScope, IScopeWithFreeVars
{
    public override ClassDefNode Owner { get; }
    public override string Name => Owner.Name;
    public bool ClassCaptured { get; set; }
    internal HashSet<IScopeWithFreeVars> ScopesRequiringFree = [];
    public PyCodeObject? CodeObject { get; set; }
    public List<string> TempFrees { get; } = [];
    public ImmutableArray<string> FreeVars { get; internal set; } = [];

    public ClassVariableScope(ClassDefNode owner, VariableScope parent) : base(parent)
    {
        Owner = owner;
    }
}

internal abstract class CallableVariableScope : VariableScope, IScopeWithFreeVars
{
    internal abstract AstArgumentsNode ArgumentsNode { get; }
    public bool IsGenerator { get; internal set; }
    public FrozenDictionary<string, int> LocalsTable { get; internal set; } = FrozenDictionary<string, int>.Empty;
    public ImmutableArray<string> VarNames { get; internal set; } = [];
    public ImmutableArray<string> CellVars { get; internal set; } = [];
    public ImmutableArray<string> FreeVars { get; internal set; } = [];
    public List<string> TempFrees { get; } = [];
    public Dictionary<string, HashSet<IScopeWithFreeVars>> ScopesRequiringFree = [];
    public PyCodeObject? CodeObject { get; set; }

    protected CallableVariableScope(VariableScope? parent) : base(parent)
    {
    }

    internal void CaptureVariable(string name)
    {
        var type = Variables[name];
        if (type is PyVariableType.CapturedLocal or PyVariableType.CapturedParameter)
            return;

        Debug.Assert(type is PyVariableType.Local or PyVariableType.Parameter);
        Variables[name] = type is PyVariableType.Local
            ? PyVariableType.CapturedLocal : PyVariableType.CapturedParameter;
    }
}

internal sealed class FunctionVariableScope : CallableVariableScope
{
    internal override AstArgumentsNode ArgumentsNode => Owner.Args;
    public override FunctionDefNode Owner { get; }
    public override string Name => Owner.Name;

    public FunctionVariableScope(FunctionDefNode owner, VariableScope parent) : base(parent)
    {
        Owner = owner;
    }
}

internal sealed class AsyncFunctionVariableScope : CallableVariableScope
{
    internal override AstArgumentsNode ArgumentsNode => Owner.Args;
    public override AsyncFunctionDefNode Owner { get; }
    public override string Name => Owner.Name;
    public bool IsAsyncGenerator { get; internal set; }

    public AsyncFunctionVariableScope(AsyncFunctionDefNode owner, VariableScope parent) : base(parent)
    {
        Owner = owner;
    }
}

internal sealed class LambdaVariableScope : CallableVariableScope
{
    internal override AstArgumentsNode ArgumentsNode => Owner.Args;
    public override LambdaNode Owner { get; }
    public override string Name => "<lambda>";

    public LambdaVariableScope(LambdaNode owner, VariableScope parent) : base(parent)
    {
        Owner = owner;
    }
}

internal sealed class GeneratorExpVariableScope : CallableVariableScope
{
    internal override AstArgumentsNode ArgumentsNode => AstArgumentsNode.GeneratorExp;
    public override GeneratorExpNode Owner { get; }
    public override string Name => "<genexpr>";
    public bool IsAsyncGenerator => Owner.Generators.Any(static g => g.IsAsync);

    public GeneratorExpVariableScope(GeneratorExpNode owner, VariableScope parent) : base(parent)
    {
        Owner = owner;
        IsGenerator = true;
    }
}
