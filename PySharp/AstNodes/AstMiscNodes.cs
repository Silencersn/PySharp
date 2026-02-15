using PySharp.PyModules.Builtins;
using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;

namespace PySharp.AstNodes;

public class AstAliasNode : AstNode
{
    internal AstAliasNode(string name, string? asName)
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
    public AstExprNode? Annotation { get; }

    internal AstArgNode(string arg, AstExprNode? annotation = null)
    {
        Arg = arg;
        Annotation = annotation;
    }

    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        return [];
    }
}

public class AstArgumentsNode : AstNode
{
    internal AstArgumentsNode(ImmutableArray<AstArgNode> posonlyArgs, ImmutableArray<AstArgNode> args, AstArgNode? varArg, ImmutableArray<AstArgNode> kwonlyArgs, AstArgNode? kwArg, ImmutableArray<AstExprNode?> kwDefaults, ImmutableArray<AstExprNode> defaults)
    {
        PosonlyArgs = posonlyArgs;
        Args = args;
        VarArg = varArg;
        KwonlyArgs = kwonlyArgs;
        KwArg = kwArg;
        KwDefaults = kwDefaults;
        Defaults = defaults;
    }

    public ImmutableArray<AstArgNode> PosonlyArgs { get; }
    public ImmutableArray<AstArgNode> Args { get; }
    public AstArgNode? VarArg { get; }
    public ImmutableArray<AstArgNode> KwonlyArgs { get; }
    public AstArgNode? KwArg { get; }
    public ImmutableArray<AstExprNode?> KwDefaults { get; }
    public ImmutableArray<AstExprNode> Defaults { get; }

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

    internal static Func<PyExceptionObject, bool> MakeCondition(PyCallContext context, PyObject type)
    {
        if (type is PyTypeObject typeObj)
        {
            if (!typeObj.IsSubclassOf(PyBaseExceptionObjectType.Shared))
                throw context.TypeError(PySR.Runtime_TryStmt_CatchNonException);

            return typeObj.IsInstance;
        }
        else if (type is PyTupleObject tupleObj)
        {
            if (!tupleObj._array.All(obj => obj is PyTypeObject t && t.IsSubclassOf(PyBaseExceptionObjectType.Shared)))
                throw context.TypeError(PySR.Runtime_TryStmt_CatchNonException);

            return exc => tupleObj._array.Any(obj => ((PyTypeObject)obj).IsInstance(exc));
        }
        else
        {
            throw context.TypeError(PySR.Runtime_TryStmt_CatchNonException);
        }
    }

    internal static (PyExceptionObject? RestExc, PyObject MatchedExc) Split(PyCallContext context, PyExceptionObject exception, PyObject type)
    {
        var splitResult = exception.CallMethod(context, "split", [type]).PyUnwrap(context);
        if (splitResult is not PyTupleObject tuple)
            throw context.TypeError(PySR.Runtime_TryStmt_SplitReturnsNonTuple, exception.PyType.FullName, splitResult.PyType.FullName);

        if (tuple._array.Length is not 2)
            throw context.TypeError(PySR.Runtime_TryStmt_SplitReturnsTupleWithWrongSize, exception.PyType.FullName, tuple._array.Length);

        var match = tuple._array[0];
        var restObj = tuple._array[1];
        var rest = restObj is PyNoneObject ? null : (restObj as PyExceptionObject) ??
            throw context.TypeError(PySR.Runtime_TryStmt_ExpectedExceptionOrNone, tuple._array[1].PyType.FullName);

        return (rest, match);
    }
    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        if (Type is not null)
            yield return Type;
        foreach (var stmt in Body)
            yield return stmt;
    }
}

public sealed class AstWithItemNode : AstNode
{
    internal AstWithItemNode(AstExprNode contextExpr, AstExprNode? optionalVars)
    {
        ContextExpr = contextExpr;
        OptionalVars = optionalVars;
    }

    public AstExprNode ContextExpr { get; }
    public AstExprNode? OptionalVars { get; }

    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        yield return ContextExpr;
        if (OptionalVars is not null)
            yield return OptionalVars;
    }
}

public sealed class AstMatchCaseNode : AstNode
{
    internal AstMatchCaseNode(AstPatternNode pattern, AstExprNode? guard, ImmutableArray<AstStmtNode> body)
    {
        Pattern = pattern;
        Guard = guard;
        Body = body;
    }

    public AstPatternNode Pattern { get; }
    public AstExprNode? Guard { get; }
    public ImmutableArray<AstStmtNode> Body { get; }

    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        yield return Pattern;
        if (Guard is not null)
            yield return Guard;
        foreach (var stmt in Body)
            yield return stmt;
    }
}

public abstract class AstPatternNode : AstNode
{
}

public sealed class MatchValueNode : AstPatternNode
{
    internal MatchValueNode(AstExprNode value)
    {
        Value = value;
    }

    public AstExprNode Value { get; }

    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        yield return Value;
    }
}

public sealed class MatchSingletonNode : AstPatternNode
{
    internal MatchSingletonNode(PyObject value)
    {
        Value = value;
    }

    public PyObject Value { get; }

    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        return [];
    }
}

public sealed class MatchSequenceNode : AstPatternNode
{
    internal MatchSequenceNode(ImmutableArray<AstPatternNode> patterns)
    {
        Patterns = patterns;
    }

    public ImmutableArray<AstPatternNode> Patterns { get; }

    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        foreach (var p in Patterns)
            yield return p;
    }
    internal static bool IsSequenceForMatch(PyObject obj, out (IEnumerable<PyObject> Sequence, BigInteger Length) result)
    {
        switch (obj)
        {
            case PyListObject list:
                result = (list._list, list._list.Count);
                return true;

            case PyTupleObject tuple:
                result = (tuple._array, tuple._array.Length);
                return true;

            case PyRangeObject range:
                result = (range.EnumerateRange(), range._len);
                return true;

            case PyStrObject:
            default:
                // TODO: support other valid sequences
                result = default;
                return false;
        }
    }
}

public sealed class MatchMappingNode : AstPatternNode
{
    internal MatchMappingNode(ImmutableArray<AstExprNode> keys, ImmutableArray<AstPatternNode> patterns, string? rest)
    {
        Keys = keys;
        Patterns = patterns;
        Rest = rest;
    }

    public ImmutableArray<AstExprNode> Keys { get; }
    public ImmutableArray<AstPatternNode> Patterns { get; }
    public string? Rest { get; }

    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        foreach (var k in Keys)
            yield return k;
        foreach (var p in Patterns)
            yield return p;
    }
    internal static bool IsMappingForMatch(PyObject obj, [NotNullWhen(true)] out IDictionary<PyObject, PyObject>? result)
    {
        switch (obj)
        {
            case PyDictObject dict:
                result = dict._dict;
                return true;

            default:
                // TODO: support other valid mapping
                result = default;
                return false;
        }
    }
}

public sealed class MatchClassNode : AstPatternNode
{
    internal MatchClassNode(AstExprNode cls, ImmutableArray<AstPatternNode> patterns, ImmutableArray<string> kwdAttrs, ImmutableArray<AstPatternNode> kwdPatterns)
    {
        Cls = cls;
        Patterns = patterns;
        KwdAttrs = kwdAttrs;
        KwdPatterns = kwdPatterns;
    }

    public AstExprNode Cls { get; }
    public ImmutableArray<AstPatternNode> Patterns { get; }
    public ImmutableArray<string> KwdAttrs { get; }
    public ImmutableArray<AstPatternNode> KwdPatterns { get; }

    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        yield return Cls;
        foreach (var p in Patterns)
            yield return p;
        foreach (var kp in KwdPatterns)
            yield return kp;
    }
}

public sealed class MatchStarNode : AstPatternNode
{
    internal MatchStarNode(string? name)
    {
        Name = name;
    }

    public string? Name { get; }

    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        return [];
    }
}

public sealed class MatchAsNode : AstPatternNode
{
    internal MatchAsNode(AstPatternNode? pattern, string? name)
    {
        Pattern = pattern;
        Name = name;
    }

    public AstPatternNode? Pattern { get; }
    public string? Name { get; }

    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        if (Pattern is not null)
            yield return Pattern;
    }
}

public sealed class MatchOrNode : AstPatternNode
{
    internal MatchOrNode(ImmutableArray<AstPatternNode> patterns)
    {
        Patterns = patterns;
    }

    public ImmutableArray<AstPatternNode> Patterns { get; }

    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        foreach (var p in Patterns)
            yield return p;
    }
}

public abstract class AstTypeParamNode : AstNode;

public sealed class TypeVarNode : AstTypeParamNode
{
    internal TypeVarNode(string name, AstExprNode? bound, AstExprNode? defaultValue)
    {
        Name = name;
        Bound = bound;
        DefaultValue = defaultValue;
    }

    public string Name { get; }
    public AstExprNode? Bound { get; }
    public AstExprNode? DefaultValue { get; }

    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        if (Bound is not null)
            yield return Bound;
        if (DefaultValue is not null)
            yield return DefaultValue;
    }
}

public sealed class ParamSpecNode : AstTypeParamNode
{
    internal ParamSpecNode(string name, AstExprNode? defaultValue)
    {
        Name = name;
        DefaultValue = defaultValue;
    }

    public string Name { get; }
    public AstExprNode? DefaultValue { get; }

    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        if (DefaultValue is not null)
            yield return DefaultValue;
    }
}

public sealed class TypeVarTupleNode : AstTypeParamNode
{
    internal TypeVarTupleNode(string name, AstExprNode? defaultValue)
    {
        Name = name;
        DefaultValue = defaultValue;
    }

    public string Name { get; }
    public AstExprNode? DefaultValue { get; }

    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        if (DefaultValue is not null)
            yield return DefaultValue;
    }
}
