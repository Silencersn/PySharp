using PySharp.AstNodes;
using PySharp.Bytecodes;
using PySharp.PyModules.Builtins;
using PySharp.PyRuntime.Calls;
using System;
using System.Collections.Generic;
using System.Text;

namespace PySharp.Compilation;

internal abstract class PyCompilation
{
    public abstract void Execute(PyCallContext context);
    public abstract PyObject Evaluate(PyCallContext context);
}

internal sealed class PyAstCompilation : PyCompilation
{
    private readonly SemanticModel _model;
    internal SemanticModel Model => _model;

    public PyAstCompilation(SemanticModel model)
    {
        _model = model;
    }

    public override void Execute(PyCallContext context)
    {
        context.CurrentFrame.SemanticModel = _model;
        _model.Root.Execute(context, context.CurrentFrame);
    }

    public override PyObject Evaluate(PyCallContext context)
    {
        if (_model.Root is not ExpressionNode expr)
            throw new InvalidOperationException();

        context.CurrentFrame.SemanticModel = _model;
        return expr.GetExprValue(context, context.CurrentFrame);
    }
}

internal sealed class PyBytecodeCompilation : PyCompilation
{
    private readonly Bytecode _bytecode;
    internal Bytecode Bytecode => _bytecode;

    public PyBytecodeCompilation(Bytecode bytecode)
    {
        _bytecode = bytecode;
    }

    public override void Execute(PyCallContext context)
    {
        BytecodeVirtualMachine vm = new(context, Bytecode);
        vm.Eval().PyUnwrap(context);
    }

    public override PyObject Evaluate(PyCallContext context)
    {
        throw new NotImplementedException();
    }
}