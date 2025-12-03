using PySharp.PyObjects;
using PySharp.PyObjects.Builtins;
using PySharp.PyRuntime;
using PySharp.Tokenization;
using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Xml.Linq;
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
        var list = Utils.EnumerableIterable(iter) ?? throw new PyRuntimeException(PyVirtualMachine.CurrentException!);

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
            frame.Import(name.Name, name.AsName ?? name.Name);
        }
    }

    public override void EnumerateNodes(Action<AstNode> action)
    {
        base.EnumerateNodes(action);
        Names.EnumerateNodes(action);
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
    Dictionary<string, PyVariableType> Variables { get; set; }
}

public class FunctionDefNode : AstStmtNode, IAstVariableScopeOwner
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

    public Dictionary<string, PyVariableType> Variables { get; set; } = [];

    public override void Execute(PyFrame frame)
    {
        var caller = new CustomFunctionCaller(this, frame, AstUtils.CaptureFrames(frame, Variables));

        PyObject func = new PyFunctionObject(Identifier, caller.Call);
        func = AstUtils.ApplyDeractors(func, DecoratorList, frame);
        frame.SetValue(Identifier, func);
    }

    public override void EnumerateNodes(Action<AstNode> action)
    {
        base.EnumerateNodes(action);
        Args.EnumerateNodes(action);
        Body.EnumerateNodes(action);
        DecoratorList.EnumerateNodes(action);
    }

    private sealed class CustomFunctionCaller
    {
        private readonly FunctionDefNode _node;
        private readonly PyArgsDef _def;
        private readonly Dictionary<string, PyFrame> _capturedFrames;

        internal CustomFunctionCaller(FunctionDefNode node, PyFrame frame, Dictionary<string, PyFrame> capturedFrames)
        {
            _node = node;
            _def = PyArgsDef.FromAst(node.Args, frame);
            _capturedFrames = capturedFrames;
        }

        public PyObject? Call(IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
        {
            var backFrame = PyVirtualMachine.CurrentFrame;
            var frame = backFrame.CreateFrame();
            frame._variables = _node.Variables;
            foreach (var localName in _node.Variables.Where(pair => pair.Value is PyVariableType.Local).Select(pair => pair.Key))
            {
                frame.Locals[localName] = null;
            }
            frame._capturedFrames = _capturedFrames;
            PyVirtualMachine.EnterFrame(frame);

            if (!_def.TryParse(args, kwargs, out var arguments))
            {
                PyVirtualMachine.ExitFrame();
                return PyVirtualMachine.RaiseTypeError("wrong arguments");
            }

            frame.InitArgs(_def, arguments);

            var dict = PyDictObject.CreateDict(arguments.ExtraKwargs.Select(static kvp => KeyValuePair.Create((PyObject)PyStrObject.FromString(kvp.Key), kvp.Value)));

            try
            {
                foreach (var stmt in _node.Body)
                {
                    stmt.Execute(frame);
                }
            }
            catch (AstReturnException e)
            {
                PyVirtualMachine.ExitFrame();
                return e.Value;
            }

            PyVirtualMachine.ExitFrame();
            return PyNoneObject.None;
        }
    }
}

public sealed class ClassDefNode : AstStmtNode, IAstVariableScopeOwner
{
    public new string Name { get; }

    internal ClassDefNode(string name)
    {
        Name = name;
    }

    public List<AstExprNode> Bases { get; } = [];
    public List<AstKeywordNode> Keywords { get; } = [];
    public List<AstStmtNode> Body { get; } = [];
    public List<AstExprNode> DecoratorList { get; } = [];

    public Dictionary<string, PyVariableType> Variables { get; set; } = [];

    public override void Execute(PyFrame frame)
    {
        var newFrame = frame.CreateFrame();
        newFrame._variables = Variables;
        newFrame._capturedFrames = AstUtils.CaptureFrames(frame, Variables);
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

        PyObject type = new CustomObjectType(
            Name,
            bases,
            Variables.Keys.ToDictionary(static member => member, newFrame.GetValue));
        type = AstUtils.ApplyDeractors(type, DecoratorList, frame);
        frame.SetValue(Name, type);
    }

    private sealed class CustomObjectType : PyTypeObject
    {
        public override string Name { get; }
        public override IReadOnlyList<PyTypeObject> Bases { get; }

        internal CustomObjectType(string name, IReadOnlyList<PyTypeObject> bases, IEnumerable<KeyValuePair<string, PyObject>> attributes) : base(name, bases)
        {
            Name = name;
            Bases = bases;
            AppendDefaultSpecialMethodsAsDescriptors<CustomObject>();
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

        private PyObject? CallSpecialMethod(string name)
        {
            var method = PyOperators.GetAttr(this, name);
            if (method is null)
                return null;

            return method.Call([], (Dictionary<string, PyObject>)[]);
        }

        public override PyObject? Abs()
        {
            return CallSpecialMethod(PySpecialNames.Abs);
        }
    }
}