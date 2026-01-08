using PySharp.PyModules;
using PySharp.PyModules.Builtins;
using PySharp.PyModules.CSharp;
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
            throw context.ThrowableAssertionError(null as string);

        var msg = Msg.GetExprValue(context, frame);
        throw context.ThrowableAssertionError(msg);
    }

    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        yield return Test;
        if (Msg is not null)
            yield return Msg;
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
        // TODO: __iadd__ ...

        var left = Target.GetExprValue(context, frame);
        var right = Value.GetExprValue(context, frame);
        var value = BinOpNode.EvalOperator(context, Op, left, right).PyUnwrap(context);
        Target.SetTargetValue(context, value, frame);
    }

    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        yield return Target;
        yield return Value;
    }
}

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

public sealed class IfNode : AstStmtNode
{
    public AstExprNode Test { get; }
    public ImmutableArray<AstStmtNode> Body { get; }
    public ImmutableArray<AstStmtNode> OrElse { get; }

    internal IfNode(AstExprNode test, ImmutableArray<AstStmtNode> body, ImmutableArray<AstStmtNode> orElse)
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

public sealed class WhileNode : AstStmtNode
{
    public AstExprNode Test { get; }
    public ImmutableArray<AstStmtNode> Body { get; }
    public ImmutableArray<AstStmtNode> OrElse { get; }

    public WhileNode(AstExprNode test, ImmutableArray<AstStmtNode> body, ImmutableArray<AstStmtNode> orElse)
    {
        Test = test;
        Body = body;
        OrElse = orElse;
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

public sealed class ForNode : AstStmtNode
{
    internal ForNode(AstExprNode target, AstExprNode iter, ImmutableArray<AstStmtNode> body, ImmutableArray<AstStmtNode> orElse)
    {
        Target = target;
        Iter = iter;
        Body = body;
        OrElse = orElse;
    }

    public AstExprNode Target { get; }
    public AstExprNode Iter { get; }
    public ImmutableArray<AstStmtNode> Body { get; }
    public ImmutableArray<AstStmtNode> OrElse { get; }

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

public sealed class TryNode : AstStmtNode
{
    internal TryNode(ImmutableArray<AstStmtNode> body, ImmutableArray<ExceptHandlerNode> exceptors, ImmutableArray<AstStmtNode> orElse, ImmutableArray<AstStmtNode> finalBody)
    {
        Body = body;
        Exceptors = exceptors;
        OrElse = orElse;
        FinalBody = finalBody;
    }

    public ImmutableArray<AstStmtNode> Body { get; }
    public ImmutableArray<ExceptHandlerNode> Exceptors { get; }
    public ImmutableArray<AstStmtNode> OrElse { get; }
    public ImmutableArray<AstStmtNode> FinalBody { get; }

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
            throw context.ThrowableModuleNotFoundError($"No module named '{Module}'");

        if (Names.Length is 1 && Names[0].Name is "*")
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

internal abstract class Caller
{
    protected readonly PyArgsDef _def;
    protected readonly Func<PyCallContext, PyFrame, PyResult> _getResult;
    protected readonly FrameType _frameType;
    protected readonly CallableVariableScope _variableScope;
    public PyFunctionObject Func { get; set; }

    internal Caller(PyCallContext context, CallableVariableScope variableScope, PyFrame frame, Func<PyCallContext, PyFrame, PyResult> getResult)
    {
        _def = PyArgsDef.FromAst(variableScope.ArgumentsNode, context, frame);
        _getResult = getResult;
        _variableScope = variableScope;
        if (this is FunctionCaller)
            _frameType = _variableScope.Owner is FunctionDefNode ? FrameType.Function : FrameType.Lambda;
        else
            _frameType = _variableScope.Owner is FunctionDefNode ? FrameType.YieldFunction : FrameType.YieldLambda;

        // deferred init
        Func = null!;
    }

    public abstract PyResult Call(PyCallContext context, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs);

    public PyFrame CreateCallingFrame(PyCallContext context, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs, PyArguments arguments)
    {
        var backFrame = context.CurrentFrame;
        var frame = backFrame.CreateFuncCallOrClassBuildFrame(Func.Name, Func, _frameType, (args, kwargs), Func._globals, _variableScope.LocalsTable);
        frame._variables = _variableScope.Variables;

        foreach (var capturedVariable in _variableScope.CapturedVariables)
            frame.Closures[capturedVariable] = PyCellObject.CreateCell(capturedVariable, null);
        foreach (var cell in Func.CapturedVariables)
            frame.Closures.Add(cell.Name, cell);

        frame.InitArgs(_def, arguments);
        return frame;
    }
}

internal sealed class FunctionCaller : Caller
{
    public FunctionCaller(PyCallContext context, CallableVariableScope variableScope, PyFrame frame, Func<PyCallContext, PyFrame, PyResult> getResult) : base(context, variableScope, frame, getResult)
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
    public GeneratorCaller(PyCallContext context, CallableVariableScope variableScope, PyFrame frame, Func<PyCallContext, PyFrame, PyResult> getResult) : base(context, variableScope, frame, getResult)
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

        Debug.Assert(_variableScope.Name is not null);
        return new PyUserDefinedGeneratorObject(_variableScope.Name, frame, task);
    }
}

internal interface IScopedSubNodesProvider
{
    IEnumerable<AstNode> EnumerateSubNodesOuterScope();
    IEnumerable<AstNode> EnumerateSubNodesInnerScope();
}


public sealed class FunctionDefNode : AstStmtNode, IScopedSubNodesProvider
{
    internal FunctionDefNode(string name, AstArgumentsNode args, ImmutableArray<AstStmtNode> body, ImmutableArray<AstExprNode> decoratorList)
    {
        Name = name;
        Args = args;
        Body = body;
        DecoratorList = decoratorList;
    }

    public string Name { get; }
    public AstArgumentsNode Args { get; }
    public ImmutableArray<AstStmtNode> Body { get; }
    public ImmutableArray<AstExprNode> DecoratorList { get; }

    internal FunctionVariableScope? VariableScope { get; set; }

    public override void ExecuteStmt(PyCallContext context, PyFrame frame)
    {
        if (VariableScope is null)
            throw new InvalidOperationException();

        Caller caller = VariableScope.HasYield ?
            new GeneratorCaller(context, VariableScope, frame, GetResult) :
            new FunctionCaller(context, VariableScope, frame, GetResult);

        var func = new PyFunctionObject(Name, caller.Call,
            VariableScope.HasSuper && frame.FrameType is FrameType.Class
            ? ((IEnumerable<PyCellObject>?)frame.InternalClosure?.Values ?? [])
                .Append(PyCellObject.CreateCell(PySpecialNames.Class, frame.Caller))
            : frame.InternalClosure?.Values,
            frame._globals);

        Debug.Assert(VariableScope.QualName is not null);
        func.PyAttributes.Add(PySpecialNames.QualName, PyStrObject.FromString(VariableScope.QualName));
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
        foreach (var d in DecoratorList)
            yield return d;

        yield return Args;

        foreach (var stmt in Body)
            yield return stmt;
    }

    IEnumerable<AstNode> IScopedSubNodesProvider.EnumerateSubNodesOuterScope()
    {
        foreach (var d in DecoratorList)
            yield return d;

        foreach (var d in Args.KwDefaults)
            if (d is not null)
                yield return d;

        foreach (var d in Args.Defaults)
            yield return d;
    }

    IEnumerable<AstNode> IScopedSubNodesProvider.EnumerateSubNodesInnerScope()
    {
        foreach (var n in Args.PosonlyArgs)
            yield return n;

        foreach (var n in Args.Args)
            yield return n;

        if (Args.VarArg is not null)
            yield return Args.VarArg;

        foreach (var n in Args.KwonlyArgs)
            yield return n;

        if (Args.KwArg is not null)
            yield return Args.KwArg;

        foreach (var stmt in Body)
            yield return stmt;
    }
}

public sealed class ClassDefNode : AstStmtNode, IScopedSubNodesProvider
{
    internal ClassDefNode(string name, ImmutableArray<AstExprNode> bases, ImmutableArray<AstKeywordNode> keywords, ImmutableArray<AstStmtNode> body, ImmutableArray<AstExprNode> decoratorList)
    {
        Name = name;
        Bases = bases;
        Keywords = keywords;
        Body = body;
        DecoratorList = decoratorList;
    }

    public string Name { get; }
    public ImmutableArray<AstExprNode> Bases { get; } = [];
    public ImmutableArray<AstKeywordNode> Keywords { get; } = [];
    public ImmutableArray<AstStmtNode> Body { get; } = [];
    public ImmutableArray<AstExprNode> DecoratorList { get; } = [];

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
        Debug.Assert(VariableScope.QualName is not null);
        var type = UserDefinedType.Create(layoutType, Name, VariableScope.QualName, bases);

        if (AstUtils.TryGetDoc(Body, out var doc))
            type.PyAttributes[PySpecialNames.Doc] = doc;

        var newFrame = frame.CreateFuncCallOrClassBuildFrame(Name, type, FrameType.Class);
        newFrame._variables = VariableScope.Variables;
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

        var attrs = VariableScope.Variables.Keys.ToDictionary(static member => member, member => newFrame.GetVariable(member).PyUnwrap(context));
        foreach (var attr in attrs)
            type.PyAttributes[attr.Key] = attr.Value;

        foreach (var (name, value) in attrs)
        {
            var setNameFunc = value.PyType.Slots.SetName;
            if (setNameFunc is not null)
                setNameFunc(context, value, type, PyStrObject.FromString(name)).PyUnwrap(context);

            switch (name)
            {
                case PySpecialNames.New: type.Slots.New = value.ToClsArgsKwargsFunction(); break;

                case PySpecialNames.Str: type.Slots.Str = value.ToUnaryFunction(); break;
                case PySpecialNames.Repr: type.Slots.Repr = value.ToUnaryFunction(); break;
                case PySpecialNames.Bool: type.Slots.Bool = value.ToUnaryFunction(); break;
                case PySpecialNames.Hash: type.Slots.Hash = value.ToUnaryFunction(); break;
                case PySpecialNames.Len: type.Slots.Len = value.ToUnaryFunction(); break;
                case PySpecialNames.Index: type.Slots.Index = value.ToUnaryFunction(); break;
                case PySpecialNames.Int: type.Slots.Int = value.ToUnaryFunction(); break;
                case PySpecialNames.Float: type.Slots.Float = value.ToUnaryFunction(); break;
                case PySpecialNames.Call: type.Slots.Call = value.ToSelfArgsKwargsFunction(); break;

                case PySpecialNames.Iter: type.Slots.Iter = value.ToUnaryFunction(); break;
                case PySpecialNames.Next: type.Slots.Next = value.ToUnaryFunction(); break;
                case PySpecialNames.GetItem: type.Slots.GetItem = value.ToBinaryFunction(); break;
                case PySpecialNames.SetItem: type.Slots.SetItem = value.ToTernaryFunction(); break;
                case PySpecialNames.DelItem: type.Slots.DelItem = value.ToBinaryFunction(); break;
                case PySpecialNames.Contains: type.Slots.Contains = value.ToBinaryFunction(); break;

                case PySpecialNames.Get: type.Slots.Get = value.ToTernaryFunction(); break;
                case PySpecialNames.Set: type.Slots.Set = value.ToTernaryFunction(); break;
                case PySpecialNames.Delete: type.Slots.Delete = value.ToBinaryFunction(); break;
                case PySpecialNames.GetAttribute: type.Slots.GetAttribute = value.ToBinaryFunction(); break;
                case PySpecialNames.GetAttr: type.Slots.GetAttr = value.ToBinaryFunction(); break;
                case PySpecialNames.SetAttr: type.Slots.SetAttr = value.ToTernaryFunction(); break;
                case PySpecialNames.DelAttr: type.Slots.DelAttr = value.ToBinaryFunction(); break;

                // Binary operators
                case PySpecialNames.Add: type.Slots.Add = value.ToBinaryFunction(); break;
                case PySpecialNames.Sub: type.Slots.Sub = value.ToBinaryFunction(); break;
                case PySpecialNames.Mul: type.Slots.Mul = value.ToBinaryFunction(); break;
                case PySpecialNames.TrueDiv: type.Slots.TrueDiv = value.ToBinaryFunction(); break;
                case PySpecialNames.FloorDiv: type.Slots.FloorDiv = value.ToBinaryFunction(); break;
                case PySpecialNames.Mod: type.Slots.Mod = value.ToBinaryFunction(); break;
                case PySpecialNames.DivMod: type.Slots.DivMod = value.ToBinaryFunction(); break;
                case PySpecialNames.LShift: type.Slots.LShift = value.ToBinaryFunction(); break;
                case PySpecialNames.RShift: type.Slots.RShift = value.ToBinaryFunction(); break;
                case PySpecialNames.And: type.Slots.And = value.ToBinaryFunction(); break;
                case PySpecialNames.Xor: type.Slots.Xor = value.ToBinaryFunction(); break;
                case PySpecialNames.Or: type.Slots.Or = value.ToBinaryFunction(); break;

                // Reverse binary operators
                case PySpecialNames.RAdd: type.Slots.RAdd = value.ToBinaryFunction(); break;
                case PySpecialNames.RSub: type.Slots.RSub = value.ToBinaryFunction(); break;
                case PySpecialNames.RMul: type.Slots.RMul = value.ToBinaryFunction(); break;
                case PySpecialNames.RTrueDiv: type.Slots.RTrueDiv = value.ToBinaryFunction(); break;
                case PySpecialNames.RFloorDiv: type.Slots.RFloorDiv = value.ToBinaryFunction(); break;
                case PySpecialNames.RMod: type.Slots.RMod = value.ToBinaryFunction(); break;
                case PySpecialNames.RDivMod: type.Slots.RDivMod = value.ToBinaryFunction(); break;
                case PySpecialNames.RLShift: type.Slots.RLShift = value.ToBinaryFunction(); break;
                case PySpecialNames.RRShift: type.Slots.RRShift = value.ToBinaryFunction(); break;
                case PySpecialNames.RAnd: type.Slots.RAnd = value.ToBinaryFunction(); break;
                case PySpecialNames.RXor: type.Slots.RXor = value.ToBinaryFunction(); break;
                case PySpecialNames.ROr: type.Slots.ROr = value.ToBinaryFunction(); break;

                // Ternary operators
                case PySpecialNames.Pow: type.Slots.Pow = value.ToTernaryFunction(); break;
                case PySpecialNames.RPow: type.Slots.RPow = value.ToTernaryFunction(); break;

                // Rich comparison operators
                case PySpecialNames.Lt: type.Slots.Lt = value.ToBinaryFunction(); break;
                case PySpecialNames.Le: type.Slots.Le = value.ToBinaryFunction(); break;
                case PySpecialNames.Eq: type.Slots.Eq = value.ToBinaryFunction(); break;
                case PySpecialNames.Ne: type.Slots.Ne = value.ToBinaryFunction(); break;
                case PySpecialNames.Gt: type.Slots.Gt = value.ToBinaryFunction(); break;
                case PySpecialNames.Ge: type.Slots.Ge = value.ToBinaryFunction(); break;

                case PySpecialNames.Complex: type.Slots.Complex = value.ToUnaryFunction(); break;
                case PySpecialNames.Abs: type.Slots.Abs = value.ToUnaryFunction(); break;
                case PySpecialNames.Neg: type.Slots.Neg = value.ToUnaryFunction(); break;
                case PySpecialNames.Pos: type.Slots.Pos = value.ToUnaryFunction(); break;
                case PySpecialNames.Invert: type.Slots.Invert = value.ToUnaryFunction(); break;
                case PySpecialNames.SetName: type.Slots.SetName = value.ToTernaryFunction(); break;
                case PySpecialNames.Missing: type.Slots.Missing = value.ToBinaryFunction(); break;
                case PySpecialNames.Init: type.Slots.Init = value.ToSelfArgsKwargsFunction(); break;
                case PySpecialNames.Format: type.Slots.Format = value.ToBinaryFunction(); break;
            }
        }

        frame.SetVariable(Name, AstUtils.ApplyDecorators(type, DecoratorList, context, frame)).PyUnwrap(context);
    }
    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        foreach (var d in DecoratorList)
            yield return d;

        foreach (var b in Bases)
            yield return b;

        foreach (var k in Keywords)
            yield return k;

        foreach (var stmt in Body)
            yield return stmt;
    }

    IEnumerable<AstNode> IScopedSubNodesProvider.EnumerateSubNodesOuterScope()
    {
        foreach (var d in DecoratorList)
            yield return d;

        foreach (var b in Bases)
            yield return b;

        foreach (var k in Keywords)
            yield return k;
    }

    IEnumerable<AstNode> IScopedSubNodesProvider.EnumerateSubNodesInnerScope()
    {
        foreach (var stmt in Body)
            yield return stmt;
    }
}