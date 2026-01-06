using PySharp.PyModules.Builtins;
using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;
using System.Collections.Immutable;

namespace PySharp.AstNodes;

public class AstAliasNode : AstNode
{
    public AstAliasNode(string name, string? asName)
    {
        Name = name;
        AsName = asName;
    }

    public string Name { get; }
    public string? AsName { get; }

    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        return [];
    }

    internal string GetLocalName()
    {
        if (AsName is not null)
            return AsName;

        var index = Name.IndexOf('.');
        if (index is -1)
            return Name;
        return Name[..index];
    }
}


public class AstArgNode : AstNode
{
    public string Arg { get; }

    public AstArgNode(string arg)
    {
        Arg = arg;
    }

    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        return [];
    }
}

public class AstArgumentsNode : AstNode
{
    public AstArgumentsNode()
    {
        PosonlyArgs = [];
        Args = [];
        KwonlyArgs = [];
        KwDefaults = [];
        Defaults = [];
    }

    public List<AstArgNode> PosonlyArgs { get; }
    public List<AstArgNode> Args { get; }
    public AstArgNode? VarArg { get; set; }
    public List<AstArgNode> KwonlyArgs { get; }
    public AstArgNode? KwArg { get; set; }
    public List<AstExprNode?> KwDefaults { get; }
    public List<AstExprNode> Defaults { get; }

    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        foreach (var n in PosonlyArgs) yield return n;
        foreach (var n in Args) yield return n;
        if (VarArg is not null)
            yield return VarArg;
        foreach (var n in KwonlyArgs) yield return n;
        if (KwArg is not null)
            yield return KwArg;
        foreach (var d in KwDefaults)
            if (d is not null)
                yield return d;
        foreach (var d in Defaults) yield return d;
    }
}

public class AstComprehensionNode : AstNode
{
    internal AstComprehensionNode(AstExprNode target, AstExprNode iter, ImmutableArray<AstExprNode> ifs)
    {
        Target = target;
        Iter = iter;
        Ifs = ifs;
    }

    public AstExprNode Target { get; }
    public AstExprNode Iter { get; }
    public ImmutableArray<AstExprNode> Ifs { get; }

    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        yield return Target;
        yield return Iter;
        foreach (var f in Ifs) yield return f;
    }
}


public class AstKeywordNode : AstNode
{
    internal AstKeywordNode(string? arg, AstExprNode value)
    {
        Arg = arg;
        Value = value;
    }

    public string? Arg { get; }
    public AstExprNode Value { get; }

    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        yield return Value;
    }

    internal void AddOrUnpackValueTo(IDictionary<string, PyObject> targetDict, PyCallContext context, PyFrame frame)
    {
        if (Arg is not null)
        {
            var value = Value.GetExprValue(context, frame);
            targetDict[Arg] = value;
            return;
        }

        var mapping = Value.GetExprValue(context, frame);
        if (mapping is not PyDictObject dict)
            throw new NotImplementedException(); // TODO: support other mappings

        foreach (var (key, value) in dict._dict)
        {
            if (key is not PyStrObject str)
                throw context.ThrowableTypeError("keywords must be strings");

            targetDict[str.Value] = value; // TODO: raise Error ?
        }
    }
}

public sealed class ExceptHandlerNode : AstNode
{
    internal ExceptHandlerNode(AstExprNode? type, string? name, ImmutableArray<AstStmtNode> body)
    {
        Type = type;
        Name = name;
        Body = body;
    }

    public AstExprNode? Type { get; }
    public string? Name { get; }
    public ImmutableArray<AstStmtNode> Body { get; }

    public bool TryHandle(PyCallContext context, PyFrame frame, PyExceptionObject exception)
    {
        if (IsMatch())
        {
            if (Name is not null)
                frame.SetVariable(Name, exception).PyUnwrap(context);

            foreach (var stmt in Body)
            {
                stmt.Execute(context, frame);
            }

            if (Name is not null)
                frame.DeleteVariable(Name).PyUnwrap(context);

            return true;
        }

        return false;

        bool IsMatch()
        {
            if (Type is null)
                return true;

            if (Type.GetExprValue(context, frame) is not PyTypeObject typeObj || !typeObj.IsSubclassOf(PyBaseExceptionObjectType.Shared))
                throw context.ThrowableTypeError("catching classes that do not inherit from BaseException is not allowed");

            return typeObj.IsInstance(exception);
        }
    }

    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        if (Type is not null)
            yield return Type;
        foreach (var stmt in Body)
            yield return stmt;
    }
}