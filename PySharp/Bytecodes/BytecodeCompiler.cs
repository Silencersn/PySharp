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
        return new PyBytecodeCompilation(new Bytecode(compiler.Generator));
    }

    private readonly SemanticModel _model;

    internal BytecodeCompiler(SemanticModel model)
    {
        Generator = new();
        _model = model;
        var scope = _model.GetVariableScope<RootVariableScope>(_model.Root);
        Debug.Assert(scope is not null);
        VariableScope = scope;
    }

    private BytecodeGenerator Generator { get; set; }
    private SemanticModel Model => _model;
    private VariableScope VariableScope { get; set; }
    private Stack<(Label LoopBegin, Label LoopEnd)> Loops { get; } = [];

    public void Compile()
    {
        CompileMod(Model.Root);
    }

    private void CompileMod(AstModNode node)
    {
        Debug.Assert(VariableScope is RootVariableScope);
        switch (node)
        {
            case ModuleNode n:
                foreach (var stmt in n.Body)
                    CompileStmt(stmt);
                break;

            case ExpressionNode n:
                LoadExpr(n.Body);
                Generator.Emit(OpCode.PopTop);
                break;

            case InteractiveNode n:
                foreach (var stmt in n.Body)
                    CompileStmt(stmt);
                break;

            default:
                throw new NotImplementedException();
        }
    }
}
