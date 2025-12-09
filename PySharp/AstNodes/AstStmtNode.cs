using PySharp.PyModules;
using PySharp.PyModules.Builtins;
using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;
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
    public override AstStmtNode? Reduce(OptimizationOptions options)
    {
        return this;
    }
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

    public override void Execute(PyFrame frame)
    {
        var value = Value.GetExprValue(frame);
        Debug.Assert(value is not PyExceptionObject { Raised: true });

        foreach (var target in Targets)
        {
            target.SetTargetValue(value, frame);
        }
    }

    public override AssignNode Reduce(OptimizationOptions options)
    {
        if (options.NoOptimization)
            return this;

        return Assign(Value.Reduce(options), Targets);
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

    public override void Execute(PyFrame frame)
    {
        var test = Test.GetExprValue(frame);
        if (!PySpecialMethods.TryGetBool(test, out var b))
            throw new PyRuntimeException(PyVirtualMachine.CurrentException!);

        if (b.BoolValue)
            return;

        if (Msg is null)
        {
            PyVirtualMachine.RaiseAssertionError(null as string);
            throw new PyRuntimeException(PyVirtualMachine.CurrentException);
        }

        var msg = Msg.GetExprValue(frame) ?? throw new PyRuntimeException(PyVirtualMachine.CurrentException!);
        PyVirtualMachine.RaiseAssertionError(msg);
        throw new PyRuntimeException(PyVirtualMachine.CurrentException);
    }

    public override AssertNode? Reduce(OptimizationOptions options)
    {
        if (options.NoOptimization)
            return this;

        return null;
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

    public override void Execute(PyFrame frame)
    {
        foreach (var target in Targets)
        {
            target.DeleteTargetValue(frame);
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

    public override void Execute(PyFrame frame)
    {
        Target.SetTargetValue(Op.GetOpValue(Target.GetExprValue(frame), Value.GetExprValue(frame)).PyThrowIfNullOrNotImplemented(), frame);
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

    public override void Execute(PyFrame frame)
    {
        var value = Value.GetExprValue(frame);
        if (PyVirtualMachine.IsInteractive)
        {
            if (value is not PyNoneObject)
            {
                var repr = PySpecialMethods.GetRepr(value).PyThrowIfNull();
                PyVirtualMachine.Out.WriteLine(repr.Value);
            }
        }
    }

    public override ExprNode? Reduce(OptimizationOptions options)
    {
        if (options.NoOptimization)
            return this;

        var reducedValue = Value.Reduce(options);

        if (options.CodeCleanup && reducedValue.NoSideEffects() is true)
            return null;

        return new ExprNode(reducedValue);
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

    public override void Execute(PyFrame frame)
    {
        if (Test.GetBoolValue(frame))
        {
            foreach (var stmt in Body)
            {
                stmt.Execute(frame);
            }
        }
        else
        {
            foreach (var stmt in OrElse)
            {
                stmt.Execute(frame);
            }
        }
    }

    public override AstStmtNode? Reduce(OptimizationOptions options)
    {
        if (options.NoOptimization)
            return this;

        var reducedTest = Test.Reduce(options);
        var testResult = reducedTest.TrgGetConstantBoolValue();

        if (options.DeadCodeElimination)
        {
            if (testResult is false)
                return null;
        }

        var reducedBody = Body.Reduce(options).ToList();
        var reducedOrElse = OrElse.Reduce(options).ToList();

        if (options.CodeCleanup)
        {
            if (reducedBody.Count is 0 && reducedOrElse.Count is 0)
            {
                if (testResult is not null)
                    return null;
            }
        }

        return new IfNode(reducedTest, [.. reducedBody], [.. reducedOrElse]);
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
    public override void Execute(PyFrame frame)
    {
        throw new AstBreakException();
    }

    public sealed class AstBreakException : AstException;
}
public class ContinueNode : AstStmtNode
{
    public override void Execute(PyFrame frame)
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

    public override void Execute(PyFrame frame)
    {
        throw new AstReturnException(Value?.GetExprValue(frame) ?? PyNoneObject.None);
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
    public override void Execute(PyFrame frame)
    {
    }

    public override PassNode? Reduce(OptimizationOptions options)
    {
        if (options.CodeCleanup)
            return null;

        return this;
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

    public override void Execute(PyFrame frame)
    {
        bool isBreak = false;

        try
        {
            while (Test.GetBoolValue(frame))
            {
                try
                {
                    foreach (var stmt in Body)
                    {
                        stmt.Execute(frame);
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
                stmt.Execute(frame);
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

    public override void Execute(PyFrame frame)
    {
        bool isBreak = false;

        var iter = Iter.GetExprValue(frame);
        var list = Utils.EnumerateIterable(iter) ?? throw new PyRuntimeException(PyVirtualMachine.CurrentException!);

        try
        {
            foreach (var item in list)
            {
                Target.SetTargetValue(item.PyThrowIfNull(), frame);
                try
                {
                    foreach (var stmt in Body)
                    {
                        stmt.Execute(frame);
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
                stmt.Execute(frame);
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

    public override void Execute(PyFrame frame)
    {
        if (Exc is null)
        {
            PyVirtualMachine.CurrentException = frame.CurrentException;
            throw new PyRuntimeException(frame.CurrentException);
        }

        var obj = Exc.GetExprValue(frame);
        PyExceptionObject exc;
        if (obj is PyExceptionObject excObj)
            exc = excObj;
        else
            exc = obj.PyCastExceptionType().Create();

        if (Cause is not null)
        {
            var cause = Cause.GetExprValue(frame);
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

    public override void Execute(PyFrame frame)
    {
        bool catched = false;
        try
        {
            foreach (var stmt in Body)
            {
                stmt.Execute(frame);
            }
        }
        catch (PyRuntimeException e)
        {
            while (PyVirtualMachine.CurrentFrame != frame)
                PyVirtualMachine.ExitFrame();

            frame.Exceptions.Push(e.PyException);
            catched = true;
            bool handled = false;
            foreach (var exceptor in Exceptors)
            {
                if (handled = exceptor.TryHandle(frame, e.PyException))
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
                    stmt.Execute(frame);
                }
            }

            foreach (var stmt in FinalBody)
            {
                stmt.Execute(frame);
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

    public override void Execute(PyFrame frame)
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

    public override void Execute(PyFrame frame)
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

    public override void Execute(PyFrame frame)
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

    public override void Execute(PyFrame frame)
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

internal sealed class FunctionCaller
{
    private readonly IFunctionOrLambda _node;
    private readonly PyArgsDef _def;
    private readonly Func<PyFrame, PyObject> _getResult;
    internal PyFunctionObject _func;
    internal FrameInfo _info;

    internal FunctionCaller(IFunctionOrLambda node, PyFrame frame, Func<PyFrame, PyObject> getResult)
    {
        _node = node;
        _def = PyArgsDef.FromAst(node.Args, frame);
        _getResult = getResult;

        // deferred init
        _func = null!;
        _info = null!;
    }

    public PyObject? Call(IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        return CallGeneral(args, kwargs);
    }

    private PyObject? CallGeneral(IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        var backFrame = PyVirtualMachine.CurrentFrame;
        var frame = backFrame.CreateFuncCallFrame(_info);
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
            return PyVirtualMachine.RaiseTypeError("wrong arguments");
        }

        frame.InitArgs(_def, arguments);

        PyObject result = _getResult(frame);

        PyVirtualMachine.ExitFrame();
        return result;
    }
}

public class FunctionDefNode : AstStmtNode, IFunctionOrLambda
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

    public override void Execute(PyFrame frame)
    {
        var caller = new FunctionCaller(this, frame, frame =>
        {
            try
            {
                foreach (var stmt in Body)
                {
                    stmt.Execute(frame);
                }
            }
            catch (AstReturnException e)
            {
                return e.Value;
            }
            return PyNoneObject.None;
        });
        var func = new PyFunctionObject(Identifier, caller.Call, frame.IntenalClosure?.Values);
        caller._func = func;
        caller._info = new FrameInfo(MetaInfo, func.Name);

        frame.SetValue(Identifier, AstUtils.ApplyDeractors(func, DecoratorList, frame));
    }

    public override void EnumerateNodes(Action<AstNode> action)
    {
        base.EnumerateNodes(action);
        Args.EnumerateNodes(action);
        Body.EnumerateNodes(action);
        DecoratorList.EnumerateNodes(action);
    }
}

public sealed class ClassDefNode : AstStmtNode, IAstVariableScopeOwner
{
    public new string Name { get; }

    internal ClassDefNode(MetaInfo metaInfo, string name)
    {
        MetaInfo = metaInfo;
        Name = name;
        _info = new FrameInfo(metaInfo, name);
    }

    public List<AstExprNode> Bases { get; } = [];
    public List<AstKeywordNode> Keywords { get; } = [];
    public List<AstStmtNode> Body { get; } = [];
    public List<AstExprNode> DecoratorList { get; } = [];

    FrozenDictionary<string, PyVariableType> IAstVariableScopeOwner.Variables { get; set; } = null!;
    private readonly FrameInfo _info;

    public override void Execute(PyFrame frame)
    {
        var newFrame = frame.CreateFuncCallFrame(_info);
        newFrame._variables = ((IAstVariableScopeOwner)this).Variables;
        foreach (var (name, cell) in frame.Closures)
        {
            newFrame.Closures.Add(name, cell);
        }
        PyVirtualMachine.EnterFrame(newFrame);

        foreach (var stmt in Body)
        {
            stmt.Execute(newFrame);
        }
        PyVirtualMachine.ExitFrame();

        var bases = Bases.Select(baseExpr =>
        {
            var baseType = baseExpr.GetExprValue(frame);
            if (baseType is not PyTypeObject typeObj)
                throw new NotSupportedException();
            return typeObj;
        }).ToList();
        if (bases.Count is 0)
            bases.Add(PyBuiltinTypes.Object);

        var attrs = ((IAstVariableScopeOwner)this).Variables.Keys.ToDictionary(static member => member, newFrame.GetValue);
        var type = new CustomObjectType(Name, bases, attrs);

        foreach (var (name, value) in attrs)
            value.SetName(type, PyStrObject.FromString(name)).PyThrowIfNull();

        frame.SetValue(Name, AstUtils.ApplyDeractors(type, DecoratorList, frame));
    }

    private sealed class CustomObjectType : PyTypeObject
    {
        public override string Name { get; }
        public override IReadOnlyList<PyTypeObject> Bases { get; }

        internal CustomObjectType(string name, IReadOnlyList<PyTypeObject> bases, IEnumerable<KeyValuePair<string, PyObject>> attributes) : base(name, bases)
        {
            Name = name;
            Bases = bases;
            AppendMethodDescriptor(PySpecialNames.Repr, PyObjectRepr, PySpecialMethodParametersType.NoArgs);
            AppendMethodDescriptor(PySpecialNames.Str, PyObjectStr, PySpecialMethodParametersType.NoArgs);
            AppendMethodDescriptor(PySpecialNames.Hash, PyObjectHash, PySpecialMethodParametersType.NoArgs);
            AppendMethodDescriptor(PySpecialNames.Bool, PyObjectBool, PySpecialMethodParametersType.NoArgs);
            PyAttributes[PySpecialNames.Name] = PyStrObject.FromString(Name);
            foreach (var attribute in attributes)
            {
                PyAttributes[attribute.Key] = attribute.Value;
            }
        }

        public override PyObject? New(IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
        {
            return new CustomObject(this);
        }
    }

    private sealed class CustomObject : PyObject
    {
        public override PyTypeObject PyType { get; }

        internal CustomObject(PyTypeObject pyType)
        {
            PyType = pyType;
        }

        private PyObject? CallSpecialMethodOrBase(
            string methodName,
            Func<PyObject?> baseCall,
            IReadOnlyList<PyObject> args,
            IReadOnlyDictionary<string, PyObject>? kwargs = null)
        {
            var method = PyObjectGetAttribute(this, methodName);
            if (method is null)
            {
                if (!PyVirtualMachine.IsExceptionOfTypeRaised(PyStandardExceptionTypes.AttributeError))
                    return null;

                PyVirtualMachine.ClearException();
                return baseCall();
            }
            return method.Call(args, kwargs ?? FrozenDictionary<string, PyObject>.Empty);
        }

        #region Methods

        public override PyObject? Str()
        {
            return CallSpecialMethodOrBase(PySpecialNames.Str, () => base.Str(), []);
        }

        public override PyObject? SetAttr(string key, PyObject value)
        {
            return CallSpecialMethodOrBase(PySpecialNames.SetAttr, () => base.SetAttr(key, value), [PyStrObject.FromString(key), value]);
        }

        public override PyObject? DelAttr(string key)
        {
            return CallSpecialMethodOrBase(PySpecialNames.DelAttr, () => base.DelAttr(key), [PyStrObject.FromString(key)]);
        }

        public override PyObject? Add(PyObject other)
        {
            return CallSpecialMethodOrBase(PySpecialNames.Add, () => base.Add(other), [other]);
        }

        public override PyObject? Sub(PyObject other)
        {
            return CallSpecialMethodOrBase(PySpecialNames.Sub, () => base.Sub(other), [other]);
        }

        public override PyObject? Mul(PyObject other)
        {
            return CallSpecialMethodOrBase(PySpecialNames.Mul, () => base.Mul(other), [other]);
        }

        public override PyObject? TrueDiv(PyObject other)
        {
            return CallSpecialMethodOrBase(PySpecialNames.TrueDiv, () => base.TrueDiv(other), [other]);
        }

        public override PyObject? FloorDiv(PyObject other)
        {
            return CallSpecialMethodOrBase(PySpecialNames.FloorDiv, () => base.FloorDiv(other), [other]);
        }

        public override PyObject? Mod(PyObject other)
        {
            return CallSpecialMethodOrBase(PySpecialNames.Mod, () => base.Mod(other), [other]);
        }

        public override PyObject? Pow(PyObject other, PyObject modulo)
        {
            return CallSpecialMethodOrBase(PySpecialNames.Pow, () => base.Pow(other, modulo), [other, modulo]);
        }

        public override PyObject? LShift(PyObject other)
        {
            return CallSpecialMethodOrBase(PySpecialNames.LShift, () => base.LShift(other), [other]);
        }

        public override PyObject? RShift(PyObject other)
        {
            return CallSpecialMethodOrBase(PySpecialNames.RShift, () => base.RShift(other), [other]);
        }

        public override PyObject? And(PyObject other)
        {
            return CallSpecialMethodOrBase(PySpecialNames.And, () => base.And(other), [other]);
        }

        public override PyObject? Or(PyObject other)
        {
            return CallSpecialMethodOrBase(PySpecialNames.Or, () => base.Or(other), [other]);
        }

        public override PyObject? Xor(PyObject other)
        {
            return CallSpecialMethodOrBase(PySpecialNames.Xor, () => base.Xor(other), [other]);
        }

        public override PyObject? Eq(PyObject other)
        {
            return CallSpecialMethodOrBase(PySpecialNames.Eq, () => base.Eq(other), [other]);
        }

        public override PyObject? Ne(PyObject other)
        {
            return CallSpecialMethodOrBase(PySpecialNames.Ne, () => base.Ne(other), [other]);
        }

        public override PyObject? Lt(PyObject other)
        {
            return CallSpecialMethodOrBase(PySpecialNames.Lt, () => base.Lt(other), [other]);
        }

        public override PyObject? Le(PyObject other)
        {
            return CallSpecialMethodOrBase(PySpecialNames.Le, () => base.Le(other), [other]);
        }

        public override PyObject? Gt(PyObject other)
        {
            return CallSpecialMethodOrBase(PySpecialNames.Gt, () => base.Gt(other), [other]);
        }

        public override PyObject? Ge(PyObject other)
        {
            return CallSpecialMethodOrBase(PySpecialNames.Ge, () => base.Ge(other), [other]);
        }

        public override PyObject? GetItem(PyObject key)
        {
            return CallSpecialMethodOrBase(PySpecialNames.GetItem, () => base.GetItem(key), [key]);
        }

        public override PyObject? SetItem(PyObject key, PyObject value)
        {
            return CallSpecialMethodOrBase(PySpecialNames.SetItem, () => base.SetItem(key, value), [key, value]);
        }

        public override PyObject? DelItem(PyObject key)
        {
            return CallSpecialMethodOrBase(PySpecialNames.DelItem, () => base.DelItem(key), [key]);
        }

        public override PyObject? Len()
        {
            return CallSpecialMethodOrBase(PySpecialNames.Len, () => base.Len(), []);
        }

        public override PyObject? Iter()
        {
            return CallSpecialMethodOrBase(PySpecialNames.Iter, () => base.Iter(), []);
        }

        public override PyObject? Next()
        {
            return CallSpecialMethodOrBase(PySpecialNames.Next, () => base.Next(), []);
        }

        public override PyObject? Contains(PyObject item)
        {
            return CallSpecialMethodOrBase(PySpecialNames.Contains, () => base.Contains(item), [item]);
        }

        public override PyObject? Repr()
        {
            return CallSpecialMethodOrBase(PySpecialNames.Repr, () => base.Repr(), []);
        }

        public override PyObject? Bool()
        {
            return CallSpecialMethodOrBase(PySpecialNames.Bool, () => base.Bool(), []);
        }

        public override PyObject? Hash()
        {
            return CallSpecialMethodOrBase(PySpecialNames.Hash, () => base.Hash(), []);
        }

        public override PyObject? Int()
        {
            return CallSpecialMethodOrBase(PySpecialNames.Int, () => base.Int(), []);
        }

        public override PyObject? Float()
        {
            return CallSpecialMethodOrBase(PySpecialNames.Float, () => base.Float(), []);
        }

        public override PyObject? Complex()
        {
            return CallSpecialMethodOrBase(PySpecialNames.Complex, () => base.Complex(), []);
        }

        public override PyObject? Index()
        {
            return CallSpecialMethodOrBase(PySpecialNames.Index, () => base.Index(), []);
        }

        public override PyObject? Neg()
        {
            return CallSpecialMethodOrBase(PySpecialNames.Neg, () => base.Neg(), []);
        }

        public override PyObject? Pos()
        {
            return CallSpecialMethodOrBase(PySpecialNames.Pos, () => base.Pos(), []);
        }

        public override PyObject? Invert()
        {
            return CallSpecialMethodOrBase(PySpecialNames.Invert, () => base.Invert(), []);
        }

        public override PyObject? Abs()
        {
            return CallSpecialMethodOrBase(PySpecialNames.Abs, () => base.Abs(), []);
        }

        public override PyObject? DivMod(PyObject other)
        {
            return CallSpecialMethodOrBase(PySpecialNames.DivMod, () => base.DivMod(other), [other]);
        }

        public override PyObject? RAdd(PyObject other)
        {
            return CallSpecialMethodOrBase(PySpecialNames.RAdd, () => base.RAdd(other), [other]);
        }

        public override PyObject? RSub(PyObject other)
        {
            return CallSpecialMethodOrBase(PySpecialNames.RSub, () => base.RSub(other), [other]);
        }

        public override PyObject? RMul(PyObject other)
        {
            return CallSpecialMethodOrBase(PySpecialNames.RMul, () => base.RMul(other), [other]);
        }

        public override PyObject? RTrueDiv(PyObject other)
        {
            return CallSpecialMethodOrBase(PySpecialNames.RTrueDiv, () => base.RTrueDiv(other), [other]);
        }

        public override PyObject? RFloorDiv(PyObject other)
        {
            return CallSpecialMethodOrBase(PySpecialNames.RFloorDiv, () => base.RFloorDiv(other), [other]);
        }

        public override PyObject? RMod(PyObject other)
        {
            return CallSpecialMethodOrBase(PySpecialNames.RMod, () => base.RMod(other), [other]);
        }

        public override PyObject? RDivMod(PyObject other)
        {
            return CallSpecialMethodOrBase(PySpecialNames.RDivMod, () => base.RDivMod(other), [other]);
        }

        public override PyObject? RPow(PyObject other, PyObject modulo)
        {
            return CallSpecialMethodOrBase(PySpecialNames.RPow, () => base.RPow(other, modulo), [other, modulo]);
        }

        public override PyObject? RLShift(PyObject other)
        {
            return CallSpecialMethodOrBase(PySpecialNames.RLShift, () => base.RLShift(other), [other]);
        }

        public override PyObject? RRShift(PyObject other)
        {
            return CallSpecialMethodOrBase(PySpecialNames.RRShift, () => base.RRShift(other), [other]);
        }

        public override PyObject? RAnd(PyObject other)
        {
            return CallSpecialMethodOrBase(PySpecialNames.RAnd, () => base.RAnd(other), [other]);
        }

        public override PyObject? RXor(PyObject other)
        {
            return CallSpecialMethodOrBase(PySpecialNames.RXor, () => base.RXor(other), [other]);
        }

        public override PyObject? ROr(PyObject other)
        {
            return CallSpecialMethodOrBase(PySpecialNames.ROr, () => base.ROr(other), [other]);
        }

        public override PyObject? Missing(PyObject key)
        {
            return CallSpecialMethodOrBase(PySpecialNames.Missing, () => base.Missing(key), [key]);
        }

        public override PyObject? Get(PyObject instance, PyObject owner)
        {
            return CallSpecialMethodOrBase(PySpecialNames.Get, () => base.Get(instance, owner), [instance, owner]);
        }

        public override PyObject? Set(PyObject instance, PyObject value)
        {
            return CallSpecialMethodOrBase(PySpecialNames.Set, () => base.Set(instance, value), [instance, value]);
        }

        public override PyObject? Delete(PyObject instance)
        {
            return CallSpecialMethodOrBase(PySpecialNames.Delete, () => base.Delete(instance), [instance]);
        }

        public override PyObject? Call(IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
        {
            return CallSpecialMethodOrBase(PySpecialNames.Call, () => base.Call(args, kwargs), args, kwargs);
        }

        public override PyObject? Init(IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
        {
            return CallSpecialMethodOrBase(PySpecialNames.Init, () => base.Init(args, kwargs), args, kwargs);
        }

        public override PyObject? GetAttr(string key)
        {
            return CallSpecialMethodOrBase(PySpecialNames.GetAttr, () => base.GetAttr(key), [PyStrObject.FromString(key)]);
        }

        public override PyObject? GetAttribute(string item)
        {
            return CallSpecialMethodOrBase(PySpecialNames.GetAttribute, () => base.GetAttribute(item), [PyStrObject.FromString(item)]);
        }

        public override PyObject? SetName(PyObject owner, PyObject name)
        {
            return CallSpecialMethodOrBase(PySpecialNames.SetName, () => base.SetName(owner, name), [owner, name]);
        }

        #endregion Methods
    }
}