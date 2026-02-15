using PySharp.Modules;
using PySharp.Modules.Builtins;
using PySharp.Runtime;
using PySharp.Runtime.Calls;
using System.Collections.Immutable;
using System.Diagnostics;

namespace PySharp.Compilation.AstNodes;

public abstract class AstStmtNode : AstNode;

public sealed class ExprNode : AstStmtNode
{
    public AstExprNode Value { get; }

    internal ExprNode(AstExprNode value)
    {
        Value = value;
    }

    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        yield return Value;
    }
}

public sealed class AssignNode : AstStmtNode
{
    internal AssignNode(ImmutableArray<AstExprNode> targets, AstExprNode value)
    {
        Targets = targets;
        Value = value;
    }

    public ImmutableArray<AstExprNode> Targets { get; }
    public AstExprNode Value { get; }

    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        foreach (var t in Targets) yield return t;
        yield return Value;
    }
}

public sealed class AugAssignNode : AstStmtNode
{
    internal AugAssignNode(AstExprNode target, OperatorType op, AstExprNode value)
    {
        Target = target;
        Op = op;
        Value = value;
    }

    public AstExprNode Target { get; }
    public OperatorType Op { get; }
    public AstExprNode Value { get; }

    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        yield return Target;
        yield return Value;
    }

    internal static PyResult EvalInplaceOperator(PyCallContext context, OperatorType op, PyObject left, PyObject right)
    {
        return op switch
        {
            OperatorType.Add => PyOperators.InPlaceAdd(context, left, right),
            OperatorType.Sub => PyOperators.InPlaceSub(context, left, right),
            OperatorType.Mult => PyOperators.InPlaceMult(context, left, right),
            OperatorType.MatMult => throw new NotImplementedException(), // PyOperators.InPlaceMatMult(context, left, right),
            OperatorType.Div => PyOperators.InPlaceTrueDiv(context, left, right),
            OperatorType.Mod => PyOperators.InPlaceMod(context, left, right),
            OperatorType.Pow => PyOperators.InPlacePow(context, left, right, PyNoneObject.None),
            OperatorType.LShift => PyOperators.InPlaceLShift(context, left, right),
            OperatorType.RShift => PyOperators.InPlaceRShift(context, left, right),
            OperatorType.BitOr => PyOperators.InPlaceBitOr(context, left, right),
            OperatorType.BitXor => PyOperators.InPlaceBitXor(context, left, right),
            OperatorType.BitAnd => PyOperators.InPlaceBitAnd(context, left, right),
            OperatorType.FloorDiv => PyOperators.InPlaceFloorDiv(context, left, right),
            _ => throw new UnreachableException(),
        };
    }
}

public sealed class AnnAssignNode : AstStmtNode
{
    internal AnnAssignNode(AstExprNode target, AstExprNode annotation, AstExprNode? value, bool simple)
    {
        Target = target;
        Annotation = annotation;
        Value = value;
        Simple = simple;
    }

    public AstExprNode Target { get; }
    public AstExprNode Annotation { get; }
    public AstExprNode? Value { get; }
    public bool Simple { get; }

    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        yield return Target;

        // TODO: if EnumerateSubNodes is called by SemanticAnalyzer, it should not enumerate Annotation
        //yield return Annotation;

        if (Value is not null)
            yield return Value;
    }
}

public sealed class AssertNode : AstStmtNode
{
    internal AssertNode(AstExprNode test, AstExprNode? msg)
    {
        Test = test;
        Msg = msg;
    }

    public AstExprNode Test { get; }
    public AstExprNode? Msg { get; }

    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        yield return Test;
        if (Msg is not null)
            yield return Msg;
    }
}

public sealed class PassNode : AstStmtNode
{
    internal PassNode()
    {
    }
    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        return [];
    }
}

public sealed class DeleteNode : AstStmtNode
{
    public ImmutableArray<AstExprNode> Targets { get; }

    internal DeleteNode(ImmutableArray<AstExprNode> targets)
    {
        Targets = targets;
    }
    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        foreach (var t in Targets) yield return t;
    }
}

public sealed class ReturnNode : AstStmtNode
{
    public AstExprNode? Value { get; }

    internal ReturnNode(AstExprNode? value)
    {
        Value = value;
    }

    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        if (Value is not null)
            yield return Value;
    }
}

public sealed class RaiseNode : AstStmtNode
{
    internal RaiseNode(AstExprNode? exc, AstExprNode? cause)
    {
        Exc = exc;
        Cause = cause;
    }

    public AstExprNode? Exc { get; }
    public AstExprNode? Cause { get; }

    internal static void Raise(PyCallContext context, PyFrame frame, PyObject? excObj, PyObject? causeObj)
    {
        var exc = ToException(context, excObj)
            ?? throw new PyRuntimeException(context, frame.CurrentException);

        if (causeObj is not null)
        {
            if (causeObj is PyNoneObject)
            {
                exc.SuppressContext = true;
            }
            else
            {
                exc.Cause = ToException(context, causeObj);
                exc.CauseReason = PySR.Runtime_RaiseStmt_Cause;
            }
        }

        if (frame.Exceptions.TryPeek(out var pre))
            exc.Context = pre;

        throw new PyRuntimeException(context, exc);

        static PyExceptionObject? ToException(PyCallContext context, PyObject? pyObj)
        {
            if (pyObj is null)
                return null;

            if (pyObj is PyExceptionObject excObj)
                return excObj;

            else if (pyObj is PyTypeObject typeObj && typeObj.IsSubclassOf(PyBaseExceptionObjectType.Shared))
                return new PyExceptionObject(typeObj);

            else
                throw context.TypeError(PySR.Runtime_RaiseStmt_RaiseNonException);
        }
    }

    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        if (Exc is not null)
            yield return Exc;
        if (Cause is not null)
            yield return Cause;
    }
}

public sealed class BreakNode : AstStmtNode
{
    internal BreakNode()
    {
    }
    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        return [];
    }
}

public sealed class ContinueNode : AstStmtNode
{
    internal ContinueNode()
    {
    }
    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        return [];
    }
}

public sealed class ImportNode : AstStmtNode
{
    public ImmutableArray<AstAliasNode> Names { get; }

    internal ImportNode(ImmutableArray<AstAliasNode> names)
    {
        Names = names;
    }

    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        foreach (var n in Names) yield return n;
    }
}

public sealed class ImportFromNode : AstStmtNode
{
    internal ImportFromNode(string? module, ImmutableArray<AstAliasNode> names, int level)
    {
        Module = module;
        Names = names;
        Level = level;
    }

    public string? Module { get; }
    public ImmutableArray<AstAliasNode> Names { get; }
    public int Level { get; }

    internal static void ImportAllFrom(PyCallContext context, PyFrame frame, PyModuleObject module)
    {
        // if module has __all__, import only those names
        // item in __all__ must be str
        if (module.PyAttributes.TryGetValue(PySpecialNames.All, out var all))
        {
            // unlike cpython, allows iterable
            if (!Utils.TryEnumeratedIterable(context, all, out var list, out _))
                throw context.TypeError(PySR.Runtime_Import_NonIterableAll, module.Name);

            foreach (var item in list)
            {
                if (item is not PyStrObject strObj)
                    throw context.TypeError(PySR.Runtime_Import_NonStringAllElt, module.Name, item.PyType.Name);

                var attr = PyOperators.GetAttr(context, module, strObj.Value).PyUnwrap(context);
                frame.SetVariable(strObj.Value, attr).PyUnwrap(context);
            }
        }
        else
        {
            foreach (var kvp in module.PyAttributes)
            {
                // only import names that do not start with '_'
                if (!kvp.Key.StartsWith('_'))
                    frame.SetVariable(kvp.Key, kvp.Value).PyUnwrap(context);
            }
        }
    }

    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        return Names;
    }
}

public sealed class GlobalNode : AstStmtNode
{
    internal GlobalNode(ImmutableArray<string> names)
    {
        Names = names;
    }

    public ImmutableArray<string> Names { get; }

    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        return [];
    }
}

public sealed class NonlocalNode : AstStmtNode
{
    internal NonlocalNode(ImmutableArray<string> names)
    {
        Names = names;
    }

    public ImmutableArray<string> Names { get; }

    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        return [];
    }
}
