using PySharp.AstNodes;
using PySharp.Compilation;
using System.Diagnostics;

namespace PySharp.Bytecodes;

internal sealed partial class BytecodeCompiler
{
    public static PyBytecodeCompilation Compile(SemanticModel model)
    {
        var compiler = new BytecodeCompiler(model);
        compiler.Compile();
        return new PyBytecodeCompilation(model, compiler.Generator.Instructions);
    }

    private readonly BytecodeGenerator _generator;
    private readonly SemanticModel _model;

    internal BytecodeCompiler(SemanticModel model)
    {
        _generator = new();
        _model = model;
        var scope = _model.GetVariableScope<RootVariableScope>(_model.Root);
        Debug.Assert(scope is not null);
        CurrentScope = scope;
    }

    private BytecodeGenerator Generator => _generator;
    private SemanticModel Model => _model;
    private VariableScope CurrentScope { get; set; }

    public void Compile()
    {
        CompileMod(Model.Root);
    }

    private void CompileMod(AstModNode node)
    {
        Debug.Assert(CurrentScope is RootVariableScope);
        switch (node)
        {
            case ModuleNode n:
                foreach (var stmt in n.Body)
                    CompileStmt(stmt);
                break;

            default:
                throw new NotImplementedException();
        }
    }
}
