using PySharp.CodeAnalysis;
using PySharp.PyModules;
using PySharp.PyModules.Builtins;
using PySharp.PyModules.CSharp;
using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;
using System.Collections.Frozen;
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

        using var withMetaInfo = new MetaInfoProviderSetter(frame, Test);

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

    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        yield return Test;
        if (Msg is not null)
            yield return Msg;
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
    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        foreach (var t in Targets) yield return t;
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

    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        yield return Target;
        yield return Op;
        yield return Value;
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
        if (context.IsInteractive && frame.IsRoot)
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

    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        yield return Value;
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

    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        yield return Test;
        foreach (var stmt in Body) yield return stmt;
        foreach (var stmt in OrElse) yield return stmt;
    }
}

public abstract class AstControlException : Exception;
public sealed class AstBreakException : AstControlException;
public sealed class AstContinueException : AstControlException;
public sealed class AstReturnException(PyObject value) : AstControlException { public PyObject Value { get; } = value ?? throw new ArgumentNullException(nameof(value)); }

public class BreakNode : AstStmtNode
{
    public override void ExecuteStmt(PyCallContext context, PyFrame frame)
    {
        throw new AstBreakException();
    }
    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        return [];
    }
}

public class ContinueNode : AstStmtNode
{
    public override void ExecuteStmt(PyCallContext context, PyFrame frame)
    {
        throw new AstContinueException();
    }
    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        return [];
    }
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

    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        if (Value is not null)
            yield return Value;
    }
}

public class PassNode : AstStmtNode
{
    public override void ExecuteStmt(PyCallContext context, PyFrame frame)
    {
    }
    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        return [];
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

    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        yield return Test;
        foreach (var stmt in Body) yield return stmt;
        foreach (var stmt in OrElse) yield return stmt;
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

    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        yield return Target;
        yield return Iter;
        foreach (var stmt in Body) yield return stmt;
        foreach (var stmt in OrElse) yield return stmt;
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
            throw new PyRuntimeException(context, frame.CurrentException);

        var obj = Exc.GetExprValue(context, frame);
        var exc = ToException(context, obj);

        if (Cause is not null)
        {
            var cause = Cause.GetExprValue(context, frame);
            if (cause is PyNoneObject)
            {
                exc.SuppressContext = true;
            }
            else
            {
                exc.Cause = ToException(context, cause);
                exc.CauseReason = "The above exception was the direct cause of the following exception:";
            }
        }

        if (frame.Exceptions.TryPeek(out var pre))
        {
            exc.Context = pre;
        }

        throw new PyRuntimeException(context, exc);

        static PyExceptionObject ToException(PyCallContext context, PyObject pyObj)
        {
            if (pyObj is PyExceptionObject excObj)
                return excObj;

            else if (pyObj is PyTypeObject typeObj && typeObj.IsSubclassOf(PyBaseExceptionObjectType.Shared))
                return new PyExceptionObject(typeObj);

            else
                throw context.ThrowableTypeError($"exceptions must derive from BaseException");
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

    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        foreach (var stmt in Body) yield return stmt;
        foreach (var ex in Exceptors) yield return ex;
        foreach (var stmt in OrElse) yield return stmt;
        foreach (var stmt in FinalBody) yield return stmt;
    }
}

public enum PyVariableType
{
    Unknown,
    Local,
    Global,
    Closure,
    Nonlocal = Closure,
    Parameter,

    // only appears during or after the semantic analysis phase
    CapturedLocal,
    CapturedParameter
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

    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        foreach (var n in Names) yield return n;
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
            return;
        }

        foreach (var name in Names)
        {
            Debug.Assert(name.Name is not "*");

            if (!module.PyAttributes.TryGetValue(name.Name, out var value))
                throw context.ThrowableImportError($"cannot import name '{name.Name}' from '{Module /* TODO: should be module.__name__ */}'");

            frame.SetVariable(name.AsName ?? name.Name, value).PyUnwrap(context);
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

    internal override void Dump(AstNodeDumper dumper)
    {
        dumper
            .Append("Global")
            .AppendFields(("names", Names));
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

    internal override void Dump(AstNodeDumper dumper)
    {
        dumper
            .Append("Nonlocal")
            .AppendFields(("names", Names));
    }

    public override void ExecuteStmt(PyCallContext context, PyFrame frame)
    {
    }
    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        return [];
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
    FrozenDictionary<string, int> LocalVariablesToIndex { get; set; }
    string[] CapturedVariables { get; set; }
    bool HasYield { get; set; }
}

internal interface IFunctionOrClass : IAstVariableScopeOwner
{
    public string Name { get; }
    public string QualifiedName { get; set; }
}

internal abstract class Caller
{
    protected readonly IFunctionOrLambda _node;
    protected readonly PyArgsDef _def;
    protected readonly Func<PyCallContext, PyFrame, PyResult> _getResult;
    protected readonly FrameType _frameType;
    public PyFunctionObject Func { get; set; }

    internal Caller(PyCallContext context, IFunctionOrLambda node, PyFrame frame, Func<PyCallContext, PyFrame, PyResult> getResult)
    {
        _node = node;
        _def = PyArgsDef.FromAst(node.Args, context, frame);
        _getResult = getResult;
        if (this is FunctionCaller)
            _frameType = _node is FunctionDefNode ? FrameType.Function : FrameType.Lambda;
        else
            _frameType = _node is FunctionDefNode ? FrameType.YieldFunction : FrameType.YieldLambda;

        // deferred init
        Func = null!;
    }

    public abstract PyResult Call(PyCallContext context, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs);

    public PyFrame CreateCallingFrame(PyCallContext context, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs, PyArguments arguments)
    {
        var backFrame = context.CurrentFrame;
        var frame = backFrame.CreateFuncCallOrClassBuildFrame(Func.Name, Func, _frameType, (args, kwargs), Func._globals, _node.LocalVariablesToIndex);
        frame._variables = _node.Variables;

        foreach (var capturedVariable in _node.CapturedVariables)
            frame.Closures[capturedVariable] = PyCellObject.CreateCell(capturedVariable, null);
        foreach (var cell in Func.CapturedVariables)
            frame.Closures.Add(cell.Name, cell);

        frame.InitArgs(_def, arguments);
        return frame;
    }
}

internal sealed class FunctionCaller : Caller
{
    public FunctionCaller(PyCallContext context, IFunctionOrLambda node, PyFrame frame, Func<PyCallContext, PyFrame, PyResult> getResult) : base(context, node, frame, getResult)
    {
    }

    public override PyResult Call(PyCallContext context, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        return CallGeneral(context, args, kwargs);
    }

    private PyResult CallGeneral(PyCallContext context, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        if (!_def.TryParse(args, kwargs, out var arguments))
            return PyResult.RaiseTypeError("wrong arguments");

        var frame = CreateCallingFrame(context, args, kwargs, arguments);

        context.EnterFrame(frame);

        var result = _getResult(context, frame);

        context.ExitFrame();
        return result;
    }
}


internal sealed class GeneratorCaller : Caller
{
    public GeneratorCaller(PyCallContext context, IFunctionOrLambda node, PyFrame frame, Func<PyCallContext, PyFrame, PyResult> getResult) : base(context, node, frame, getResult)
    {
    }

    public override PyResult Call(PyCallContext context, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        return CallGeneral(context, args, kwargs);
    }

    private PyResult CallGeneral(PyCallContext context, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        if (!_def.TryParse(args, kwargs, out var arguments))
            return PyResult.RaiseTypeError("wrong arguments");

        var frame = CreateCallingFrame(context, args, kwargs, arguments);

        frame.Back = null;
        frame._tcsWaitAtStartOrYield = new TaskCompletionSource<YieldCallerAction>();

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

        var name = (_node as FunctionDefNode)?.Name ?? "<lambda>";
        return new PyUserDefinedGeneratorObject(name, frame, task);
    }
}


public class FunctionDefNode : AstStmtNode, IFunctionOrLambda, IFunctionOrClass
{
    public FunctionDefNode(string identifier, AstArgumentsNode args)
    {
        Name = identifier;
        Args = args;
    }

    public string Name { get; }
    public AstArgumentsNode Args { get; }
    public List<AstStmtNode> Body { get; } = [];
    public List<AstExprNode> DecoratorList { get; } = [];

    FrozenDictionary<string, PyVariableType> IAstVariableScopeOwner.Variables { get; set; } = null!;
    string[] IFunctionOrLambda.CapturedVariables { get; set; } = null!;
    string[] IFunctionOrLambda.LocalVariables { get; set; } = null!;
    FrozenDictionary<string, int> IFunctionOrLambda.LocalVariablesToIndex { get; set; } = null!;
    bool IFunctionOrLambda.HasYield { get; set; } = false;
    string IFunctionOrClass.QualifiedName { get; set; } = null!;
    string IFunctionOrClass.Name => Name;
    internal bool IncludeSuper { get; set; } = false;
    internal FunctionVariableScope? VariableScope { get; set; }

    public override void ExecuteStmt(PyCallContext context, PyFrame frame)
    {
        if (VariableScope is null)
            throw new InvalidOperationException();

        Caller caller = ((IFunctionOrLambda)this).HasYield ?
            new GeneratorCaller(context, this, frame, GetResult) :
            new FunctionCaller(context, this, frame, GetResult);

        var func = new PyFunctionObject(Name, caller.Call,
            VariableScope.HasSuper && frame.FrameType is FrameType.Class
            ? ((IEnumerable<PyCellObject>?)frame.InternalClosure?.Values ?? [])
                .Append(PyCellObject.CreateCell(PySpecialNames.Class, frame.Caller))
            : frame.InternalClosure?.Values,
            frame._globals);

        func.PyAttributes.Add(PySpecialNames.QualName, PyStrObject.FromString(((IFunctionOrClass)this).QualifiedName));
        if (AstUtils.TryGetDoc(Body, out var doc))
            func.PyAttributes[PySpecialNames.Doc] = doc;
        caller.Func = func;

        frame.SetVariable(Name, AstUtils.ApplyDecorators(func, DecoratorList, context, frame)).PyUnwrap(context);
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

    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        yield return Args;
        foreach (var stmt in Body)
            yield return stmt;
        foreach (var d in DecoratorList)
            yield return d;
    }
}

public sealed class ClassDefNode : AstStmtNode, IFunctionOrClass
{
    public string Name { get; }

    internal ClassDefNode(CodeMetaInfo metaInfo, string name)
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
    internal ClassVariableScope? VariableScope { get; set; }

    public override void ExecuteStmt(PyCallContext context, PyFrame frame)
    {
        if (VariableScope is null)
            throw new InvalidOperationException();

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

        var attrs = ((IAstVariableScopeOwner)this).Variables.Keys.ToDictionary(static member => member, member => newFrame.GetVariable(member).PyUnwrap(context));
        foreach (var attr in attrs)
            type.PyAttributes[attr.Key] = attr.Value;

        foreach (var (name, value) in attrs)
        {
            if (PyObject.PyObjectHasAttribute(value.PyType, PySpecialNames.SetName))
                value.SetName(context, type, PyStrObject.FromString(name)).PyUnwrap(context);
        }

        frame.SetVariable(Name, AstUtils.ApplyDecorators(type, DecoratorList, context, frame)).PyUnwrap(context);
    }
    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        foreach (var b in Bases) yield return b;
        foreach (var k in Keywords) yield return k;
        foreach (var stmt in Body) yield return stmt;
        foreach (var d in DecoratorList) yield return d;
    }
}