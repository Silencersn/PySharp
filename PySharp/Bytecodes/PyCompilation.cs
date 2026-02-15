using PySharp.AstNodes;
using PySharp.PyModules.Builtins;
using PySharp.PyRuntime.Calls;

namespace PySharp.Bytecodes;

internal abstract class PyCompilation
{
    public abstract void Execute(PyCallContext context);
    public abstract PyObject Evaluate(PyCallContext context);
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
        _ = Evaluate(context);
    }

    public override PyObject Evaluate(PyCallContext context)
    {
        var vm = new BytecodeVirtualMachine(context, Bytecode);
        return vm.Eval().PyUnwrap(context);
    }
}