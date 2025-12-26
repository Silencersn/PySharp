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
            result.PyUnwrap(context);

        if (b!.BoolValue)
            return;

        if (Msg is null)
            throw context.ThrowableAssertionError(null as string);

        var msg = Msg.GetExprValue(context, frame);
        throw context.ThrowableAssertionError(msg);
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
        Target.SetTargetValue(context, Op.GetOpValue(context, Target.GetExprValue(context, frame), Value.GetExprValue(context, frame)).PyUnwrapIncludedNotImplemented(context), frame);
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
        if (context.IsInteractive)
        {
            if (value is not PyNoneObject)
            {
                var repr = (PyStrObject)PySpecialMethods.GetRepr(context, value).PyUnwrap(context);
                context.Out.WriteLine(repr.Value);
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
        if (!Utils.TryEnumerateIterable(context, iter, out var list, out var err))
        {
            err.Value.PyThrow(context);
        }

        try
        {
            foreach (var item in list)
            {
                Target.SetTargetValue(context, item.PyUnwrap(context), frame);
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
            context.CurrentException = frame.CurrentException;
            throw new PyRuntimeException(context, frame.CurrentException);
        }

        var obj = Exc.GetExprValue(context, frame);
        PyExceptionObject exc;
        if (obj is PyExceptionObject excObj)
            exc = excObj;
        else
            exc = obj.PyCastExceptionType(context).Create();

        if (Cause is not null)
        {
            var cause = Cause.GetExprValue(context, frame);
            if (cause is PyExceptionObject exObj)
                exc.Cause = exObj;
            else
                exc.Cause = cause.PyCastExceptionType(context).Create();
            exc.CauseReason = "The above exception was the direct cause of the following exception:";
        }

        context.CurrentException = exc;
        throw new PyRuntimeException(context, exc);
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
            e.PyException.WithTraceback(context);
            while (context.CurrentFrame != frame)
                context.ExitFrame();

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
            context.ClearException();
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
            frame.Import(context, name.Name, name.AsName ?? GetName(name.Name));
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

        if (!context.PyEnvironment.TryLoadModule(context, Module, out var module))
            throw context.ThrowableModuleNotFoundError($"No module named '{Module}'");

        if (Names.Count is 1 && Names[0].Name is "*")
        {
            // if module has __all__, import only those names
            // item in __all__ must be str
            if (module.PyAttributes.TryGetValue(PySpecialNames.All, out var all))
            {
                // unlike cpython, allows iterable
                if (!Utils.TryEnumeratedIterable(context, all, out var list, out _))
                {
                    throw context.ThrowableTypeError($"{Module /* TODO: should be module.__name__ */}.__all__ must be iterable");
                }

                foreach (var item in list)
                {
                    if (item is not PyStrObject strObj)
                    {
                        throw context.ThrowableTypeError($"Item in {Module /* TODO: should be module.__name__ */}.__all__ must be str, not {item.PyType.Name}");
                    }

                    var attr = module.GetAttribute(context, strObj.Value).PyUnwrap(context);
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
                throw context.ThrowableException(PyStandardExceptionTypes.ImportError, $"cannot import name '{name.Name}' from '{Module /* TODO: should be module.__name__ */}'");

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
    bool HasYield { get; set; }
}

internal interface IFunctionOrClass : IAstVariableScopeOwner
{
    public string Name { get; }
    public string QualifiedName { get; set; }
}

internal interface ICaller
{
    internal PyFunctionObject Func { get; set; }

    PyResult Call(PyCallContext context, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs);
}

internal sealed class FunctionCaller : ICaller
{
    private readonly IFunctionOrLambda _node;
    private readonly PyArgsDef _def;
    private readonly Func<PyCallContext, PyFrame, PyResult> _getResult;
    private readonly FrameType _frameType;
    public PyFunctionObject Func { get; set; }

    internal FunctionCaller(PyCallContext context, IFunctionOrLambda node, PyFrame frame, Func<PyCallContext, PyFrame, PyResult> getResult)
    {
        _node = node;
        _def = PyArgsDef.FromAst(node.Args, context, frame);
        _getResult = getResult;
        _frameType = _node is FunctionDefNode ? FrameType.Function : FrameType.Lambda;

        // deferred init
        Func = null!;
    }

    public PyResult Call(PyCallContext context, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        return CallGeneral(context, args, kwargs);
    }

    private PyResult CallGeneral(PyCallContext context, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        if (!_def.TryParse(args, kwargs, out var arguments))
            return PyResult.RaiseTypeError("wrong arguments");

        var backFrame = context.CurrentFrame;
        var frame = backFrame.CreateFuncCallOrClassBuildFrame(Func.Name, Func, _frameType, (args, kwargs), Func._globals);
        frame._variables = _node.Variables;

        foreach (var localVariable in _node.LocalVariables)
            frame.Locals[localVariable] = null;
        foreach (var capturedVariable in _node.CapturedVariables)
            frame.Closures[capturedVariable] = PyCellObject.CreateCell(capturedVariable, null);
        foreach (var cell in Func.CapturedVariables)
            frame.Closures.Add(cell.Name, cell);

        context.EnterFrame(frame);


        frame.InitArgs(_def, arguments);

        var result = _getResult(context, frame);

        context.ExitFrame();
        return result;
    }
}


internal sealed class GeneratorCaller : ICaller
{
    private readonly IFunctionOrLambda _node;
    private readonly PyArgsDef _def;
    private readonly Func<PyCallContext, PyFrame, PyResult> _getResult;
    private readonly FrameType _frameType;
    public PyFunctionObject Func { get; set; }

    internal GeneratorCaller(PyCallContext context, IFunctionOrLambda node, PyFrame frame, Func<PyCallContext, PyFrame, PyResult> getResult)
    {
        _node = node;
        _def = PyArgsDef.FromAst(node.Args, context, frame);
        _getResult = getResult;
        _frameType = _node is FunctionDefNode ? FrameType.YieldFunction : FrameType.YieldLambda;

        // deferred init
        Func = null!;
    }

    public PyResult Call(PyCallContext context, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        return CallGeneral(context, args, kwargs);
    }

    private PyResult CallGeneral(PyCallContext context, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        if (!_def.TryParse(args, kwargs, out var arguments))
            return PyResult.RaiseTypeError("wrong arguments");

        var backFrame = context.CurrentFrame;
        var frame = backFrame.CreateFuncCallOrClassBuildFrame(Func.Name, Func, _frameType, (args, kwargs), Func._globals);
        frame._variables = _node.Variables;

        foreach (var localVariable in _node.LocalVariables)
            frame.Locals[localVariable] = null;
        foreach (var capturedVariable in _node.CapturedVariables)
            frame.Closures[capturedVariable] = PyCellObject.CreateCell(capturedVariable, null);
        foreach (var cell in Func.CapturedVariables)
            frame.Closures.Add(cell.Name, cell);


        frame.Back = null;
        frame._tcsWaitAtStartOrYield = new TaskCompletionSource<YieldCallerAction>();
        frame.InitArgs(_def, arguments);

        var task = new Task(() =>
        {
            try
            {
                var result = _getResult(context, frame);
                Debug.Assert(frame._tcsWaitAtSend is not null);
                frame._generatorCompleted = true;
                frame._tcsWaitAtSend.SetResult(result);
            }
            catch (PyRuntimeException e)
            {
                Debug.Assert(frame._tcsWaitAtSend is not null);
                frame._generatorCompleted = true;
                frame._tcsWaitAtSend.SetResult(PyResult.FromException(e.PyException));
            }
        });

        var name = (_node as FunctionDefNode)?.Identifier ?? "<lambda>";
        return new PyUserDefinedGeneratorObject(name, frame, task);
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
    bool IFunctionOrLambda.HasYield { get; set; } = false;
    string IFunctionOrClass.QualifiedName { get; set; } = null!;
    string IFunctionOrClass.Name => Identifier;
    internal bool IncludeSuper { get; set; } = false;

    public override void ExecuteStmt(PyCallContext context, PyFrame frame)
    {
        ICaller caller = ((IFunctionOrLambda)this).HasYield ?
            new GeneratorCaller(context, this, frame, GetResult) :
            new FunctionCaller(context, this, frame, GetResult);
        var func = new PyFunctionObject(Identifier, caller.Call,
            IncludeSuper && frame.FrameType is FrameType.Class
            ? ((IEnumerable<PyCellObject>?)frame.InternalClosure?.Values ?? [])
                .Append(PyCellObject.CreateCell(PySpecialNames.Class, frame.Caller))
            : frame.InternalClosure?.Values,
            frame._globals);
        func.PyAttributes.Add(PySpecialNames.QualName, PyStrObject.FromString(((IFunctionOrClass)this).QualifiedName));
        if (AstUtils.TryGetDoc(Body, out var doc))
            func.PyAttributes[PySpecialNames.Doc] = doc;
        caller.Func = func;

        frame.SetValue(Identifier, AstUtils.ApplyDeractors(func, DecoratorList, context, frame));
    }

    private PyResult GetResult(PyCallContext context, PyFrame frame)
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
        catch (PyRuntimeException e)
        {
            return PyResult.FromException(e.PyException);
        }

        return PyNoneObject.None;
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

        PyTypeObject.ValidateBases(context, bases, out var layoutType);
        var type = UserDefinedType.Create(layoutType, Name, ((IFunctionOrClass)this).QualifiedName, bases);

        if (AstUtils.TryGetDoc(Body, out var doc))
            type.PyAttributes[PySpecialNames.Doc] = doc;

        var newFrame = frame.CreateFuncCallOrClassBuildFrame(Name, type, FrameType.Class);
        newFrame._variables = ((IAstVariableScopeOwner)this).Variables;
        foreach (var (name, cell) in frame.Closures)
        {
            newFrame.Closures.Add(name, cell);
        }
        context.EnterFrame(newFrame);

        foreach (var stmt in Body)
        {
            stmt.Execute(context, newFrame);
        }
        context.ExitFrame();

        var attrs = ((IAstVariableScopeOwner)this).Variables.Keys.ToDictionary(static member => member, newFrame.GetValue);
        foreach (var attr in attrs)
            type.PyAttributes[attr.Key] = attr.Value;

        foreach (var (name, value) in attrs)
        {
            if (PyObject.PyObjectHasAttribute(value.PyType, PySpecialNames.SetName))
                value.SetName(context, type, PyStrObject.FromString(name)).PyUnwrap(context);
        }

        frame.SetValue(Name, AstUtils.ApplyDeractors(type, DecoratorList, context, frame));
    }
}