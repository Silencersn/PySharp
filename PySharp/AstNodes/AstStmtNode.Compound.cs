using PySharp.PyModules;
using PySharp.PyModules.Builtins;
using PySharp.PyModules.CSharp;
using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;
using System.Collections.Immutable;
using System.Diagnostics;

namespace PySharp.AstNodes;

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
            e.PyException.WithTraceback(context, overwriteExisting: false);
            context.EnsureFrameState(frame);

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

public sealed class WithNode : AstStmtNode
{
    internal WithNode(ImmutableArray<AstWithItemNode> items, ImmutableArray<AstStmtNode> body)
    {
        Items = items;
        Body = body;
    }

    public ImmutableArray<AstWithItemNode> Items { get; }
    public ImmutableArray<AstStmtNode> Body { get; }

    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        foreach (var item in Items)
            yield return item;
        foreach (var stmt in Body)
            yield return stmt;
    }

    public override void ExecuteStmt(PyCallContext context, PyFrame frame)
    {
        With(0);

        void With(int i)
        {
            if (i == Items.Length)
            {
                foreach (var stmt in Body)
                    stmt.Execute(context, frame);

                return;
            }

            var manager = Items[i].ContextExpr.GetExprValue(context, frame);
            var enter = manager.PyType.Slots.Enter ??
                throw context.ThrowableTypeError($"'{manager.PyType.FullName}' object does not support the context manager protocol (missed {PySpecialNames.Enter} method)");
            var exit = manager.PyType.Slots.Exit ??
                throw context.ThrowableTypeError($"'{manager.PyType.FullName}' object does not support the context manager protocol (missed {PySpecialNames.Exit} method)");
            var value = enter(context, manager).PyUnwrap(context);
            var hitExcept = false;

            try
            {
                Items[i].OptionalVars?.SetTargetValue(context, value, frame);
                With(i + 1);
            }
            catch (PyRuntimeException e)
            {
                e.PyException.WithTraceback(context, overwriteExisting: false);
                context.EnsureFrameState(frame);

                hitExcept = true;
                var exc = e.PyException;
                var handled = exit(context, manager, exc.PyType, exc, PyTraceback.CaptureCurrentFrame(context)).PyUnwrap(context);
                if (PyOperators.Not(context, handled).PyUnwrap(context).BoolValue)
                    throw;
            }
            finally
            {
                if (!hitExcept)
                    exit(context, manager, PyNoneObject.None, PyNoneObject.None, PyNoneObject.None).PyUnwrap(context);
            }
        }
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

        foreach (var capturedVariable in _variableScope.CellVars)
            frame.Closures[capturedVariable] = PyCellObject.CreateCell(null);

        var cells = Func.Closure;
        var names = _variableScope.FreeVars;
        Debug.Assert(cells.Length == names.Length, "Closure cells count must match free variable names count");
        for (int i = 0; i < cells.Length; i++)
            frame.Closures.Add(names[i], cells[i]);

        frame.InitArgs(_def, arguments);
        return frame;
    }

    public IEnumerable<PyCellObject> GetFreeVars(PyFrame frame)
    {
        bool takeClassCell = false;
        if (frame.ClassCell is not null && _variableScope.FreeVars.Contains(PySpecialNames.Class))
        {
            takeClassCell = true;
            yield return frame.ClassCell;
        }

        if (_variableScope.FreeVars.Length is 0 ||
            (takeClassCell && _variableScope.FreeVars is [PySpecialNames.Class]))
            yield break;

        Debug.Assert(frame.InternalClosure is not null);

        foreach (var name in _variableScope.FreeVars)
        {
            if (name is PySpecialNames.Class && takeClassCell)
                continue;

            yield return frame.InternalClosure[name];
        }
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

        using var withFrame = context.WithFrame(frame);

        var result = _getResult(context, frame);

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
                e.PyException.WithTraceback(context, overwriteExisting: false);
                context.EnsureFrameState(frame);

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
    internal FunctionDefNode(string name, AstArgumentsNode args, ImmutableArray<AstStmtNode> body, ImmutableArray<AstExprNode> decoratorList, AstExprNode? returns)
    {
        Name = name;
        Args = args;
        Body = body;
        DecoratorList = decoratorList;
        Returns = returns;
    }

    public string Name { get; }
    public AstArgumentsNode Args { get; }
    public ImmutableArray<AstStmtNode> Body { get; }
    public ImmutableArray<AstExprNode> DecoratorList { get; }
    public AstExprNode? Returns { get; }

    internal FunctionVariableScope? VariableScope { get; set; }

    public override void ExecuteStmt(PyCallContext context, PyFrame frame)
    {
        if (VariableScope is null)
            throw new InvalidOperationException();

        Caller caller = VariableScope.HasYield ?
            new GeneratorCaller(context, VariableScope, frame, GetResult) :
            new FunctionCaller(context, VariableScope, frame, GetResult);

        var func = new PyFunctionObject(
            Name,
            caller.Call,
            caller.GetFreeVars(frame),
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
            e.PyException.WithTraceback(context, overwriteExisting: false);
            context.EnsureFrameState(frame);

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

        if (VariableScope.ClassCaptured)
            newFrame.ClassCell = PyCellObject.CreateCell(type);

        using (var withFrame = context.WithFrame(newFrame))
        {
            foreach (var stmt in Body)
                stmt.Execute(context, newFrame);
        }

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

                case PySpecialNames.Enter: type.Slots.Enter = value.ToUnaryFunction(); break;
                case PySpecialNames.Exit: type.Slots.Exit = value.ToQuaternaryFunction(); break;

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

                // In-place binary operators
                case PySpecialNames.IAdd: type.Slots.IAdd = value.ToBinaryFunction(); break;
                case PySpecialNames.ISub: type.Slots.ISub = value.ToBinaryFunction(); break;
                case PySpecialNames.IMul: type.Slots.IMul = value.ToBinaryFunction(); break;
                case PySpecialNames.IMatMul: type.Slots.IMatMul = value.ToBinaryFunction(); break;
                case PySpecialNames.ITrueDiv: type.Slots.ITrueDiv = value.ToBinaryFunction(); break;
                case PySpecialNames.IFloorDiv: type.Slots.IFloorDiv = value.ToBinaryFunction(); break;
                case PySpecialNames.IMod: type.Slots.IMod = value.ToBinaryFunction(); break;
                case PySpecialNames.ILShift: type.Slots.ILShift = value.ToBinaryFunction(); break;
                case PySpecialNames.IRShift: type.Slots.IRShift = value.ToBinaryFunction(); break;
                case PySpecialNames.IAnd: type.Slots.IAnd = value.ToBinaryFunction(); break;
                case PySpecialNames.IXor: type.Slots.IXor = value.ToBinaryFunction(); break;
                case PySpecialNames.IOr: type.Slots.IOr = value.ToBinaryFunction(); break;

                // Ternary operators
                case PySpecialNames.Pow: type.Slots.Pow = value.ToTernaryFunction(); break;
                case PySpecialNames.RPow: type.Slots.RPow = value.ToTernaryFunction(); break;
                case PySpecialNames.IPow: type.Slots.IPow = value.ToTernaryFunction(); break;

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