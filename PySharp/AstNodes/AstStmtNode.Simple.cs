using PySharp.PyModules;
using PySharp.PyModules.Builtins;
using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;
using System.Collections.Immutable;
using System.Diagnostics;

namespace PySharp.AstNodes;

public abstract class AstStmtNode : AstNode
{
    public sealed override void Execute(PyCallContext context, PyFrame frame)
    {
        using var withMetaInfo = new MetaInfoProviderSetter(frame, this);
        ExecuteStmt(context, frame);
    }

    public abstract void ExecuteStmt(PyCallContext context, PyFrame frame);
}

public abstract class AstControlException : Exception;
public sealed class AstBreakException : AstControlException;
public sealed class AstContinueException : AstControlException;
public sealed class AstReturnException(PyObject value) : AstControlException { public PyObject Value { get; } = value ?? throw new ArgumentNullException(nameof(value)); }

public sealed class ExprNode : AstStmtNode
{
    public AstExprNode Value { get; }

    internal ExprNode(AstExprNode value)
    {
        Value = value;
    }

    public override void ExecuteStmt(PyCallContext context, PyFrame frame)
    {
        _ = ExecuteExprStmt(context, frame);
    }

    internal PyObject ExecuteExprStmt(PyCallContext context, PyFrame frame)
    {
        var value = Value.GetExprValue(context, frame);
        if (context.IsInteractive && frame.IsRoot)
        {
            if (value is not PyNoneObject)
            {
                var repr = PySpecialMethods.Repr(context, value).PyUnwrap(context);
                context.Out.WriteLine(repr.Value);
            }
        }
        return value;
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

    public override void ExecuteStmt(PyCallContext context, PyFrame frame)
    {
        var value = Value.GetExprValue(context, frame);

        foreach (var target in Targets)
        {
            target.SetTargetValue(context, value, frame);
        }
    }
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

    public override void ExecuteStmt(PyCallContext context, PyFrame frame)
    {
        var left = Target.GetExprValue(context, frame);
        var right = Value.GetExprValue(context, frame);
        var value = EvalInplaceOperator(context, Op, left, right).PyUnwrap(context);
        Target.SetTargetValue(context, value, frame);
    }

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

    public override void ExecuteStmt(PyCallContext context, PyFrame frame)
    {
        // TODO: __annotations__ if simple

        if (Value is null)
            return;

        var value = Value.GetExprValue(context, frame);
        Target.SetTargetValue(context, value, frame);
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

    public override void ExecuteStmt(PyCallContext context, PyFrame frame)
    {
        var test = Test.GetExprValue(context, frame);
        var result = PySpecialMethods.Bool(context, test).PyUnwrap(context);

        if (result.BoolValue)
            return;

        using var withMetaInfo = new MetaInfoProviderSetter(frame, Test);

        if (Msg is null)
            throw context.AssertionError(string.Empty);

        var msg = Msg.GetExprValue(context, frame);
        throw context.AssertionError(msg);
    }

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

    public override void ExecuteStmt(PyCallContext context, PyFrame frame)
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

    public override void ExecuteStmt(PyCallContext context, PyFrame frame)
    {
        foreach (var target in Targets)
        {
            target.DeleteTargetValue(context, frame);
        }
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

    public override void ExecuteStmt(PyCallContext context, PyFrame frame)
    {
        throw new AstReturnException(Value?.GetExprValue(context, frame) ?? PyNoneObject.None);
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

    public override void ExecuteStmt(PyCallContext context, PyFrame frame)
    {
        var excObj = Exc?.GetExprValue(context, frame);
        var causeObj = Cause?.GetExprValue(context, frame);
        Raise(context, frame, excObj, causeObj);
    }

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

    public override void ExecuteStmt(PyCallContext context, PyFrame frame)
    {
        throw new AstBreakException();
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

    public override void ExecuteStmt(PyCallContext context, PyFrame frame)
    {
        throw new AstContinueException();
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

    public override void ExecuteStmt(PyCallContext context, PyFrame frame)
    {
        foreach (var name in Names)
        {
            frame.Import(context, name.Name, name.GetLocalName());
        }
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

    public override void ExecuteStmt(PyCallContext context, PyFrame frame)
    {
        if (Level > 0)
            // TODO: relative import
            throw new NotSupportedException($"Relative imports (level={Level}) are not supported");

        // Module must be not null when Level is 0
        Debug.Assert(Module is not null);

        if (!context.PyEnvironment.TryLoadModule(context, Module, out var module))
            throw context.ModuleNotFoundError(PySR.Runtime_Import_ModuleNotFound, Module);

        if (Names.Length is 1 && Names[0].Name is "*")
        {
            ImportAllFrom(context, frame, module);
            return;
        }

        foreach (var name in Names)
        {
            Debug.Assert(name.Name is not "*");

            if (!module.PyAttributes.TryGetValue(name.Name, out var value))
                throw context.ImportError(PySR.Runtime_Import_CannotImportName, name.Name, Module /* TODO: should be module.__name__ */ /* TODO: do you mean [possibleName] */);

            frame.SetVariable(name.AsName ?? name.Name, value).PyUnwrap(context);
        }
    }

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

    public override void ExecuteStmt(PyCallContext context, PyFrame frame)
    {
    }

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

    public override void ExecuteStmt(PyCallContext context, PyFrame frame)
    {
    }
    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        return [];
    }
}
