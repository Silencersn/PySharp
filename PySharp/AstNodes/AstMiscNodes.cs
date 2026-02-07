using PySharp.PyModules.Builtins;
using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;
using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Xml.Linq;

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
        if (Arg is not null)
        {
            var value = Value.GetExprValue(context, frame);
            targetDict[Arg] = value;
            return;
        }

        var mapping = Value.GetExprValue(context, frame);
        foreach (var (key, value) in AstUtils.ExtractMapping(context, mapping))
        {
            if (key is not PyStrObject str)
                throw context.TypeError(PySR.Runtime_Keyword_KeywordsMustBeStrings);

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

    private Func<PyExceptionObject, bool> ValidateHandler(PyCallContext context, PyFrame frame, bool isStar, out PyObject type)
    {
        if (Type is null)
        {
            Debug.Assert(!isStar);

            type = null!;
            return static _ => true;
        }

        type = Type.GetExprValue(context, frame);
        return MakeCondition(context, type);
    }

    internal bool TryHandle(PyCallContext context, PyFrame frame, PyExceptionObject exception)
    {
        var handler = ValidateHandler(context, frame, isStar: false, out _);

        if (!handler(exception))
            return false;

        if (Name is not null)
            frame.SetVariable(Name, exception).PyUnwrap(context);

        foreach (var stmt in Body)
            stmt.Execute(context, frame);

        if (Name is not null)
            frame.DeleteVariable(Name).PyUnwrap(context);

        return true;
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

    internal bool TryHandleStar(PyCallContext context, PyFrame frame, PyExceptionObject exception, [NotNullWhen(false)] out PyExceptionObject? rest)
    {
        Debug.Assert(PyBaseExceptionGroupObjectType.Shared.IsInstance(exception));
        Debug.Assert(exception.IsGroup);

        _ = ValidateHandler(context, frame, isStar: true, out var condition);

        var splitResult = exception.CallMethod(context, "split", [condition]).PyUnwrap(context);
        if (splitResult is not PyTupleObject tuple)
            throw context.TypeError(PySR.Runtime_TryStmt_SplitReturnsNonTuple, exception.PyType.FullName, splitResult.PyType.FullName);

        if (tuple._array.Length is not 2)
            throw context.TypeError(PySR.Runtime_TryStmt_SplitReturnsTupleWithWrongSize, exception.PyType.FullName, tuple._array.Length);

        var match = tuple._array[0];
        if (match is not PyNoneObject)
        {
            if (Name is not null)
                frame.SetVariable(Name, match).PyUnwrap(context);

            foreach (var stmt in Body)
                stmt.Execute(context, frame);

            if (Name is not null)
                frame.DeleteVariable(Name).PyUnwrap(context);
        }

        var restObj = tuple._array[1];
        if (restObj is PyNoneObject)
        {
            rest = null;
            return true;
        }

        rest = (restObj as PyExceptionObject) ??
            throw context.TypeError(PySR.Runtime_TryStmt_ExpectedExceptionOrNone, tuple._array[1].PyType.FullName);
        return false;
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

    internal bool TryExecute(PyCallContext context, PyFrame frame, PyObject subject)
    {
        if (!Pattern.IsMatch(context, frame, subject))
            return false;

        if (Guard is not null)
        {
            var guard = Guard.GetExprValue(context, frame);
            if (!PySpecialMethods.Bool(context, guard).PyUnwrap(context).BoolValue)
                return false;
        }

        foreach (var stmt in Body)
            stmt.Execute(context, frame);

        return true;
    }
}

public abstract class AstPatternNode : AstNode
{
    internal abstract bool IsMatch(PyCallContext context, PyFrame frame, PyObject subject);
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

    internal override bool IsMatch(PyCallContext context, PyFrame frame, PyObject subject)
    {
        var value = Value.GetExprValue(context, frame);
        var eq = PyOperators.Eq(context, subject, value).PyUnwrap(context);
        return PySpecialMethods.Bool(context, eq).PyUnwrap(context).BoolValue;
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

    internal override bool IsMatch(PyCallContext context, PyFrame frame, PyObject subject)
    {
        return ReferenceEquals(subject, Value);
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

    internal override bool IsMatch(PyCallContext context, PyFrame frame, PyObject subject)
    {
        if (!IsSequenceForMatch(subject, out var result))
            return false;

        var (sequence, length) = result;
        var (matchStarIndex, matchStar) = Patterns.Index().FirstOrDefault(static item => item.Item is MatchStarNode, (-1, null!));

        if (matchStarIndex is -1)
        {
            if (length != Patterns.Length)
                return false;

            return IsMatchSequence(sequence, Patterns.AsSpan());
        }

        if (length < Patterns.Length - 1)
            return false;

        var cache = sequence.ToArray();
        if (!IsMatchSequence(cache.Take(matchStarIndex), Patterns.AsSpan(..matchStarIndex)))
            return false;

        var lastCount = Patterns.Length - matchStarIndex - 1;
        if (!IsMatchSequence(cache.TakeLast(lastCount), Patterns.AsSpan((matchStarIndex + 1)..)))
            return false;

        var name = ((MatchStarNode)matchStar).Name;
        if (name is not null)
        {
            var list = PyListObject.CreateList(cache.Skip(matchStarIndex).SkipLast(lastCount));
            frame.SetVariable(name, list);
        }

        return true;

        bool IsMatchSequence(IEnumerable<PyObject> items, ReadOnlySpan<AstPatternNode> patterns)
        {
            var index = 0;
            foreach (var item in items)
            {
                if (!patterns[index++].IsMatch(context, frame, item))
                    return false;
            }
            return true;
        }
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

    internal override bool IsMatch(PyCallContext context, PyFrame frame, PyObject subject)
    {
        if (!IsMappingForMatch(subject, out var dict))
            return false;

        HashSet<PyObject> keys = [];
        for (int i = 0; i < Keys.Length; i++)
        {
            var key = Keys[i].GetExprValue(context, frame);
            if (!keys.Add(key))
                return false;

            if (!dict.TryGetValue(key, out var value))
                return false;

            if (!Patterns[i].IsMatch(context, frame, value))
                return false;
        }

        if (Rest is not null)
        {
            List<KeyValuePair<PyObject, PyObject>> rest = [];
            foreach (var pair in dict)
            {
                if (keys.Contains(pair.Key))
                    continue;

                rest.Add(pair);
            }
            var restDict = PyDictObject.CreateDict(rest);
            frame.SetVariable(Rest, restDict);
        }

        return true;
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

    internal override bool IsMatch(PyCallContext context, PyFrame frame, PyObject subject)
    {
        var cls = Cls.GetExprValue(context, frame);
        if (cls is not PyTypeObject type)
            throw context.TypeError(PySR.Runtime_MatchStmt_CallNonClass);

        if (!type.IsInstance(subject))
            return false;

        if (Patterns.Length is not 0)
        {
            var matchArgs = PyOperators.GetAttr(context, type, PySpecialNames.MatchArgs).PyUnwrap(context);

            if (matchArgs is not PyTupleObject tuple)
                throw context.TypeError(PySR.Runtime_MatchStmt_MatchArgsIsNonTuple, type.FullName, matchArgs.PyType.FullName);
            if (Patterns.Length > tuple._array.Length)
                throw context.TypeError(PySR.Runtime_MatchStmt_MatchArgsLengthNotEnough, type.FullName, tuple._array.Length, Patterns.Length);

            var attrs = new List<string>(Patterns.Length);
            foreach (var arg in tuple._array.Take(Patterns.Length))
            {
                if (arg is not PyStrObject str)
                    throw context.TypeError(PySR.Runtime_MatchStmt_MatchArgsEltMustBeString, arg.PyType.FullName);
                attrs.Add(str.Value);
            }

            if (!IsMatchKwdPatterns(attrs.Zip(Patterns)))
                return false;
        }

        if (KwdPatterns.Length is not 0)
        {
            if (!IsMatchKwdPatterns(KwdAttrs.Zip(KwdPatterns)))
                return false;
        }

        return true;

        bool IsMatchKwdPatterns(IEnumerable<(string, AstPatternNode)> kwdPatterns)
        {
            foreach (var (attr, pattern) in kwdPatterns)
            {
                var result = PyOperators.GetAttr(context, subject, attr);
                if (result.IsError)
                {
                    if (!result.IsAttributeError)
                        // non-AttributeError errors will be thrown
                        _ = result.PyUnwrap(context);

                    return false;
                }

                if (!pattern.IsMatch(context, frame, result.Value))
                    return false;
            }

            return true;
        }
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

    internal override bool IsMatch(PyCallContext context, PyFrame frame, PyObject subject)
    {
        throw new UnreachableException();
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

    internal override bool IsMatch(PyCallContext context, PyFrame frame, PyObject subject)
    {
        if (Pattern is null || Pattern.IsMatch(context, frame, subject))
        {
            if (Name is not null)
                frame.SetVariable(Name, subject);
            return true;
        }

        return false;
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

    internal override bool IsMatch(PyCallContext context, PyFrame frame, PyObject subject)
    {
        foreach (var pattern in Patterns)
        {
            if (pattern.IsMatch(context, frame, subject))
                return true;
        }
        return false;
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
