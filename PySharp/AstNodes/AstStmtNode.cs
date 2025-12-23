using PySharp.PyModules;
using PySharp.PyModules.Builtins;
using PySharp.PyModules.CSharp;
using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;
using PySharp.PyRuntime.Metadata;
using PySharp.Tokenization;
using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Diagnostics;
using static PySharp.AstNodes.BreakNode;
using static PySharp.AstNodes.ContinueNode;
using static PySharp.AstNodes.ReturnNode;


namespace PySharp.AstNodes;

public abstract class AstStmtNode : AstNode
{
    public sealed override void Execute(PyCallContext context, PyFrame frame)
    {
        frame.StmtMetaInfoProvider = this;
        ExecuteStmt(context, frame);
    }

    public abstract void ExecuteStmt(PyCallContext context, PyFrame frame);
}

public class AssignNode : AstStmtNode
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
        Debug.Assert(value is not PyExceptionObject { Raised: true });

        foreach (var target in Targets)
        {
            target.SetTargetValue(context, value, frame);
        }
    }
    public override void EnumerateNodes(Action<AstNode> action)
    {
        base.EnumerateNodes(action);
        Targets.EnumerateNodes(action);
        Value.EnumerateNodes(action);
    }

}

public class AssertNode : AstStmtNode
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
        if (!PySpecialMethods.TryGetBool(context, test, out var b, out var result))
            result.PyUnwrap();

        if (b!.BoolValue)
            return;

        if (Msg is null)
        {
            PyVirtualMachine.RaiseAssertionError(null as string);
            throw new PyRuntimeException(PyVirtualMachine.CurrentException);
        }

        var msg = Msg.GetExprValue(context, frame) ?? throw new PyRuntimeException(PyVirtualMachine.CurrentException!);
        PyVirtualMachine.RaiseAssertionError(msg);
        throw new PyRuntimeException(PyVirtualMachine.CurrentException);
    }

    internal override void Dump(AstNodeDumper dumper)
    {
        dumper
            .Append("Assert")
            .AppendFields(("test", Test), ("msg", Msg));
    }

    public override void EnumerateNodes(Action<AstNode> action)
    {
        base.EnumerateNodes(action);
        Test.EnumerateNodes(action);
        Msg?.EnumerateNodes(action);
    }

}

public class DeleteNode : AstStmtNode
{
    public ImmutableArray<AstExprNode> Targets { get; }

    public DeleteNode(ImmutableArray<AstExprNode> targets)
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
}

public class AugAssignNode : AstStmtNode
{
    public AugAssignNode(AstExprNode target, AstOperatorNode op, AstExprNode value)
    {
        Target = target;
        Op = op;
        Value = value;
    }

    public AstExprNode Target { get; }
    public AstOperatorNode Op { get; }
    public AstExprNode Value { get; }

    public override void ExecuteStmt(PyCallContext context, PyFrame frame)
    {
        Target.SetTargetValue(context, Op.GetOpValue(context, Target.GetExprValue(context, frame), Value.GetExprValue(context, frame)).PyUnwrapIncludedNotImplemented(), frame);
    }

    public override void EnumerateNodes(Action<AstNode> action)
    {
        base.EnumerateNodes(action);
        Target.EnumerateNodes(action);
        Op.EnumerateNodes(action);
        Value.EnumerateNodes(action);
    }
}

public class ExprNode : AstStmtNode
{
    public AstExprNode Value { get; }

    public ExprNode(AstExprNode value)
    {
        ArgumentNullException.ThrowIfNull(value);

        Value = value;
    }

    public override void ExecuteStmt(PyCallContext context, PyFrame frame)
    {
        _ = ExecuteExprStmt(context, frame);
    }

    internal PyObject ExecuteExprStmt(PyCallContext context, PyFrame frame)
    {
        var value = Value.GetExprValue(context, frame);
        if (PyVirtualMachine.IsInteractive)
        {
            if (value is not PyNoneObject)
            {
                var repr = (PyStrObject)PySpecialMethods.GetRepr(context, value).PyUnwrap();
                PyVirtualMachine.Out.WriteLine(repr.Value);
            }
        }
        return value;
    }
    internal override void Dump(AstNodeDumper dumper)
    {
        dumper
            .Append("Expr")
            .AppendFields(("value", Value));
    }

    public override void EnumerateNodes(Action<AstNode> action)
    {
        base.EnumerateNodes(action);
        Value.EnumerateNodes(action);
    }

}

public class IfNode : AstStmtNode
{
    public AstExprNode Test { get; }
    public List<AstStmtNode> Body { get; } = [];
    public List<AstStmtNode> OrElse { get; } = [];

    public IfNode(AstExprNode test)
    {
        ArgumentNullException.ThrowIfNull(test);

        Test = test;
    }

    public IfNode(AstExprNode test, List<AstStmtNode> body, List<AstStmtNode> orElse)
    {
        Test = test;
        Body = body;
        OrElse = orElse;
    }

    public override void ExecuteStmt(PyCallContext context, PyFrame frame)
    {
        if (Test.GetBoolValue(context, frame))
        {
            foreach (var stmt in Body)
            {
                stmt.Execute(context, frame);
            }
        }
        else
        {
            foreach (var stmt in OrElse)
            {
                stmt.Execute(context, frame);
            }
        }
    }

    public override void EnumerateNodes(Action<AstNode> action)
    {
        base.EnumerateNodes(action);
        Test.EnumerateNodes(action);
        Body.EnumerateNodes(action);
        OrElse.EnumerateNodes(action);
    }

}

public class BreakNode : AstStmtNode
{
    public override void ExecuteStmt(PyCallContext context, PyFrame frame)
    {
        throw new AstBreakException();
    }

    public sealed class AstBreakException : AstException;
}
public class ContinueNode : AstStmtNode
{
    public override void ExecuteStmt(PyCallContext context, PyFrame frame)
    {
        throw new AstContinueException();
    }

    public sealed class AstContinueException : AstException;
}
public class ReturnNode : AstStmtNode
{
    public AstExprNode? Value { get; set; }

    public ReturnNode(AstExprNode? value = null)
    {
        Value = value;
    }

    public override void ExecuteStmt(PyCallContext context, PyFrame frame)
    {
        throw new AstReturnException(Value?.GetExprValue(context, frame) ?? PyNoneObject.None);
    }

    public override void EnumerateNodes(Action<AstNode> action)
    {
        base.EnumerateNodes(action);
        Value?.EnumerateNodes(action);
    }


    public sealed class AstReturnException : AstException
    {
        public PyObject Value { get; }

        internal AstReturnException(PyObject value)
        {
            Value = value;
        }
    }
}
public class PassNode : AstStmtNode
{
    public override void ExecuteStmt(PyCallContext context, PyFrame frame)
    {
    }
}

public class WhileNode : AstStmtNode
{
    public AstExprNode Test { get; }
    public List<AstStmtNode> Body { get; } = [];
    public List<AstStmtNode> OrElse { get; } = [];

    public WhileNode(AstExprNode test)
    {
        ArgumentNullException.ThrowIfNull(test);

        Test = test;
    }

    public override void ExecuteStmt(PyCallContext context, PyFrame frame)
    {
        bool isBreak = false;

        try
        {
            while (Test.GetBoolValue(context, frame))
            {
                try
                {
                    foreach (var stmt in Body)
                    {
                        stmt.Execute(context, frame);
                    }
                }
                catch (AstContinueException)
                {

                }
            }
        }
        catch (AstBreakException)
        {
            isBreak = true;
        }

        if (!isBreak)
        {
            foreach (var stmt in OrElse)
            {
                stmt.Execute(context, frame);
            }
        }
    }

    public override void EnumerateNodes(Action<AstNode> action)
    {
        base.EnumerateNodes(action);
        Test.EnumerateNodes(action);
        Body.EnumerateNodes(action);
        OrElse.EnumerateNodes(action);
    }

}

public class ForNode : AstStmtNode
{
    public ForNode(AstExprNode target, AstExprNode iter)
    {
        Target = target;
        Iter = iter;
    }

    public AstExprNode Target { get; }
    public AstExprNode Iter { get; }
    public List<AstStmtNode> Body { get; } = [];
    public List<AstStmtNode> OrElse { get; } = [];

    public override void ExecuteStmt(PyCallContext context, PyFrame frame)
    {
        bool isBreak = false;

        var iter = Iter.GetExprValue(context, frame);
        var list = Utils.EnumerateIterable(iter) ?? throw new PyRuntimeException(PyVirtualMachine.CurrentException!);

        try
        {
            foreach (var item in list)
            {
                Target.SetTargetValue(context, item.PyThrowIfNull(), frame);
                try
                {
                    foreach (var stmt in Body)
                    {
                        stmt.Execute(context, frame);
                    }
                }
                catch (AstContinueException)
                {

                }
            }
        }
        catch (AstBreakException)
        {
            isBreak = true;
        }

        if (!isBreak)
        {
            foreach (var stmt in OrElse)
            {
                stmt.Execute(context, frame);
            }
        }
    }

    public override void EnumerateNodes(Action<AstNode> action)
    {
        base.EnumerateNodes(action);
        Target.EnumerateNodes(action);
        Iter.EnumerateNodes(action);
        Body.EnumerateNodes(action);
        OrElse.EnumerateNodes(action);
    }

}

public class RaiseNode : AstStmtNode
{
    public RaiseNode(AstExprNode? exc, AstExprNode? cause)
    {
        Exc = exc;
        Cause = cause;
    }

    public AstExprNode? Exc { get; }
    public AstExprNode? Cause { get; }

    public override void ExecuteStmt(PyCallContext context, PyFrame frame)
    {
        if (Exc is null)
        {
            PyVirtualMachine.CurrentException = frame.CurrentException;
            throw new PyRuntimeException(frame.CurrentException);
        }

        var obj = Exc.GetExprValue(context, frame);
        PyExceptionObject exc;
        if (obj is PyExceptionObject excObj)
            exc = excObj;
        else
            exc = obj.PyCastExceptionType().Create();

        if (Cause is not null)
        {
            var cause = Cause.GetExprValue(context, frame);
            if (cause is PyExceptionObject exObj)
                exc.Cause = exObj;
            else
                exc.Cause = cause.PyCastExceptionType().Create();
            exc.CauseReason = "The above exception was the direct cause of the following exception:";
        }

        PyVirtualMachine.CurrentException = exc;
        throw new PyRuntimeException(exc);
    }

    public override void EnumerateNodes(Action<AstNode> action)
    {
        base.EnumerateNodes(action);
        Exc?.EnumerateNodes(action);
        Cause?.EnumerateNodes(action);
    }

}

public class TryNode : AstStmtNode
{
    public List<AstStmtNode> Body { get; } = [];
    public List<AstExceptHandlerNode> Exceptors { get; } = [];
    public List<AstStmtNode> OrElse { get; } = [];
    public List<AstStmtNode> FinalBody { get; } = [];

    public override void ExecuteStmt(PyCallContext context, PyFrame frame)
    {
        bool catched = false;
        try
        {
            foreach (var stmt in Body)
            {
                stmt.Execute(context, frame);
            }
        }
        catch (PyRuntimeException e)
        {
            e.PyException.WithTraceback();
            while (PyVirtualMachine.CurrentFrame != frame)
                PyVirtualMachine.ExitFrame();

            frame.Exceptions.Push(e.PyException);
            catched = true;
            bool handled = false;
            foreach (var exceptor in Exceptors)
            {
                if (handled = exceptor.TryHandle(context, frame, e.PyException))
                    break;
            }
            frame.Exceptions.Pop();
            if (!handled)
                throw;
            PyVirtualMachine.ClearException();
        }
        finally
        {
            if (!catched)
            {
                foreach (var stmt in OrElse)
                {
                    stmt.Execute(context, frame);
                }
            }

            foreach (var stmt in FinalBody)
            {
                stmt.Execute(context, frame);
            }
        }
    }

    public override void EnumerateNodes(Action<AstNode> action)
    {
        base.EnumerateNodes(action);
        Body.EnumerateNodes(action);
        Exceptors.EnumerateNodes(action);
        OrElse.EnumerateNodes(action);
        FinalBody.EnumerateNodes(action);
    }

}

public enum PyVariableType
{
    Unknown,
    Local,
    Global,
    Closure,
    Nonlocal = Closure,
    Parameter
}

public class ImportNode : AstStmtNode
{
    public List<AstAliasNode> Names { get; } = [];

    public override void ExecuteStmt(PyCallContext context, PyFrame frame)
    {
        foreach (var name in Names)
        {
            frame.Import(name.Name, name.AsName ?? GetName(name.Name));
        }

        static string GetName(string module)
        {
            if (!module.Contains('.'))
                return module;

            return module.Split('.')[0];
        }
    }

    public override void EnumerateNodes(Action<AstNode> action)
    {
        base.EnumerateNodes(action);
        Names.EnumerateNodes(action);
    }

}

public class ImportFromNode : AstStmtNode
{
    public ImportFromNode(string? module, List<AstAliasNode> names, int level)
    {
        Module = module;
        Names = names;
        Level = level;
    }

    public string? Module { get; }
    public List<AstAliasNode> Names { get; }
    public int Level { get; }

    public override void ExecuteStmt(PyCallContext context, PyFrame frame)
    {
        if (Level > 0)
            // TODO: relative import
            throw new NotSupportedException($"Relative imports (level={Level}) are not supported");

        // Module must be not null when Level is 0
        Debug.Assert(Module is not null);

        if (!PyVirtualMachine.PyEnvironment.TryLoadModule(Module, out var module))
        {
            PyVirtualMachine.RaiseException(PyStandardExceptionTypes.ModuleNotFoundError, $"No module named '{Module}'");
            throw new PyRuntimeException(PyVirtualMachine.CurrentException);
        }

        if (Names.Count is 1 && Names[0].Name is "*")
        {
            // if module has __all__, import only those names
            // item in __all__ must be str
            if (module.PyAttributes.TryGetValue(PySpecialNames.All, out var all))
            {
                // unlike cpython, allows iterable
                var list = Utils.EnumeratedIterable(all);
                if (list is null)
                {
                    PyVirtualMachine.RaiseTypeError($"{Module /* TODO: should be module.__name__ */}.__all__ must be iterable");
                    throw new PyRuntimeException(PyVirtualMachine.CurrentException);
                }

                foreach (var item in list)
                {
                    if (item is not PyStrObject strObj)
                    {
                        PyVirtualMachine.RaiseTypeError($"Item in {Module /* TODO: should be module.__name__ */}.__all__ must be str, not {item.PyType.Name}");
                        throw new PyRuntimeException(PyVirtualMachine.CurrentException);
                    }

                    var attr = module.GetAttribute(strObj.Value).PyThrowIfNull();
                    frame.SetValue(strObj.Value, attr);
                }
            }
            else
            {
                foreach (var kvp in module.PyAttributes)
                {
                    // only import names that do not start with '_'
                    if (!kvp.Key.StartsWith('_'))
                        frame.SetValue(kvp.Key, kvp.Value);
                }
            }
            return;
        }

        foreach (var name in Names)
        {
            Debug.Assert(name.Name is not "*");

            if (!module.PyAttributes.TryGetValue(name.Name, out var value))
            {
                PyVirtualMachine.RaiseException(PyStandardExceptionTypes.ImportError, $"cannot import name '{name.Name}' from '{Module /* TODO: should be module.__name__ */}'");
                throw new PyRuntimeException(PyVirtualMachine.CurrentException);
            }

            frame.SetValue(name.AsName ?? name.Name, value);
        }
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

    internal override void Dump(AstNodeDumper dumper)
    {
        dumper
            .Append("Global")
            .AppendFields(("names", Names));
    }
}

public sealed class NonlocalNode : AstStmtNode
{
    internal NonlocalNode(ImmutableArray<string> names)
    {
        Names = names;
    }

    public ImmutableArray<string> Names { get; }

    internal override void Dump(AstNodeDumper dumper)
    {
        dumper
            .Append("Nonlocal")
            .AppendFields(("names", Names));
    }

    public override void ExecuteStmt(PyCallContext context, PyFrame frame)
    {
    }
}

internal interface IAstVariableScopeOwner
{
    FrozenDictionary<string, PyVariableType> Variables { get; set; }
}

internal interface IFunctionOrLambda : IAstVariableScopeOwner
{
    AstArgumentsNode Args { get; }
    string[] LocalVariables { get; set; }
    string[] CapturedVariables { get; set; }
}

internal interface IFunctionOrClass : IAstVariableScopeOwner
{
    public string Name { get; }
    public string QualifiedName { get; set; }
}

internal sealed class FunctionCaller
{
    private readonly IFunctionOrLambda _node;
    private readonly PyArgsDef _def;
    private readonly Func<PyCallContext, PyFrame, PyObject> _getResult;
    private readonly FrameType _frameType;
    internal PyFunctionObject _func;

    internal FunctionCaller(PyCallContext context, IFunctionOrLambda node, PyFrame frame, Func<PyCallContext, PyFrame, PyObject> getResult)
    {
        _node = node;
        _def = PyArgsDef.FromAst(node.Args, context, frame);
        _getResult = getResult;
        _frameType = _node is FunctionDefNode ? FrameType.Function : FrameType.Lambda;

        // deferred init
        _func = null!;
    }

    public PyResult Call(PyCallContext context, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        return CallGeneral(context, args, kwargs);
    }

    private PyResult CallGeneral(PyCallContext context, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        var backFrame = PyVirtualMachine.CurrentFrame;
        var frame = backFrame.CreateFuncCallOrClassBuildFrame(_func.Name, _func, _frameType, (args, kwargs), _func._globals);
        frame._variables = _node.Variables;

        foreach (var localVariable in _node.LocalVariables)
            frame.Locals[localVariable] = null;
        foreach (var capturedVariable in _node.CapturedVariables)
            frame.Closures[capturedVariable] = PyCellObject.CreateCell(capturedVariable, null);
        foreach (var cell in _func.CapturedVariables)
            frame.Closures.Add(cell.Name, cell);

        PyVirtualMachine.EnterFrame(frame);

        if (!_def.TryParse(args, kwargs, out var arguments))
        {
            PyVirtualMachine.ExitFrame();
            return PyResult.RaiseTypeError("wrong arguments");
        }

        frame.InitArgs(_def, arguments);

        PyObject result = _getResult(context, frame);

        PyVirtualMachine.ExitFrame();
        return result;
    }
}

public class FunctionDefNode : AstStmtNode, IFunctionOrLambda, IFunctionOrClass
{
    public FunctionDefNode(string identifier, AstArgumentsNode args)
    {
        Identifier = identifier;
        Args = args;
    }

    public string Identifier { get; }
    public AstArgumentsNode Args { get; }
    public List<AstStmtNode> Body { get; } = [];
    public List<AstExprNode> DecoratorList { get; } = [];

    FrozenDictionary<string, PyVariableType> IAstVariableScopeOwner.Variables { get; set; } = null!;
    string[] IFunctionOrLambda.CapturedVariables { get; set; } = null!;
    string[] IFunctionOrLambda.LocalVariables { get; set; } = null!;
    string IFunctionOrClass.QualifiedName { get; set; } = null!;
    string IFunctionOrClass.Name => Identifier;
    internal bool IncludeSuper { get; set; } = false;

    public override void ExecuteStmt(PyCallContext context, PyFrame frame)
    {
        var caller = new FunctionCaller(context, this, frame, (context, frame) =>
        {
            try
            {
                foreach (var stmt in Body)
                {
                    stmt.Execute(context, frame);
                }
            }
            catch (AstReturnException e)
            {
                return e.Value;
            }
            return PyNoneObject.None;
        });
        var func = new PyFunctionObject(Identifier, caller.Call,
            IncludeSuper && frame.FrameType is FrameType.Class
            ? ((IEnumerable<PyCellObject>?)frame.InternalClosure?.Values ?? [])
                .Append(PyCellObject.CreateCell(PySpecialNames.Class, frame.Caller))
            : frame.InternalClosure?.Values,
            frame._globals);
        func.PyAttributes.Add(PySpecialNames.QualName, PyStrObject.FromString(((IFunctionOrClass)this).QualifiedName));
        if (AstUtils.TryGetDoc(Body, out var doc))
            func.PyAttributes[PySpecialNames.Doc] = doc;
        caller._func = func;

        frame.SetValue(Identifier, AstUtils.ApplyDeractors(func, DecoratorList, context, frame));
    }

    public override void EnumerateNodes(Action<AstNode> action)
    {
        base.EnumerateNodes(action);
        Args.EnumerateNodes(action);
        Body.EnumerateNodes(action);
        DecoratorList.EnumerateNodes(action);
    }
}

public sealed class ClassDefNode : AstStmtNode, IFunctionOrClass
{
    public new string Name { get; }

    internal ClassDefNode(MetaInfo metaInfo, string name)
    {
        MetaInfo = metaInfo;
        Name = name;
    }

    public List<AstExprNode> Bases { get; } = [];
    public List<AstKeywordNode> Keywords { get; } = [];
    public List<AstStmtNode> Body { get; } = [];
    public List<AstExprNode> DecoratorList { get; } = [];

    FrozenDictionary<string, PyVariableType> IAstVariableScopeOwner.Variables { get; set; } = null!;
    string IFunctionOrClass.QualifiedName { get; set; } = null!;

    public override void ExecuteStmt(PyCallContext context, PyFrame frame)
    {
        var bases = Bases.Select(baseExpr =>
        {
            var baseType = baseExpr.GetExprValue(context, frame);

            if (baseType is not PyTypeObject typeObj)
                throw new NotSupportedException();

            return typeObj;
        }).ToList();
        if (bases.Count is 0)
            bases.Add(PyObjectType.Shared);

        PyTypeObject.ValidateBases(bases, out var layoutType);
        var type = UserDefinedType.Create(layoutType, Name, ((IFunctionOrClass)this).QualifiedName, bases);

        if (AstUtils.TryGetDoc(Body, out var doc))
            type.PyAttributes[PySpecialNames.Doc] = doc;

        var newFrame = frame.CreateFuncCallOrClassBuildFrame(Name, type, FrameType.Class);
        newFrame._variables = ((IAstVariableScopeOwner)this).Variables;
        foreach (var (name, cell) in frame.Closures)
        {
            newFrame.Closures.Add(name, cell);
        }
        PyVirtualMachine.EnterFrame(newFrame);

        foreach (var stmt in Body)
        {
            stmt.Execute(context, newFrame);
        }
        PyVirtualMachine.ExitFrame();

        var attrs = ((IAstVariableScopeOwner)this).Variables.Keys.ToDictionary(static member => member, newFrame.GetValue);
        foreach (var attr in attrs)
            type.PyAttributes[attr.Key] = attr.Value;

        foreach (var (name, value) in attrs)
        {
            if (PyObject.PyObjectHasAttribute(value.PyType, PySpecialNames.SetName))
                value.SetName(type, PyStrObject.FromString(name)).PyThrowIfNull();
        }

        frame.SetValue(Name, AstUtils.ApplyDeractors(type, DecoratorList, context, frame));
    }
}