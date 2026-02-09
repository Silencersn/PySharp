using PySharp.AstNodes;
using PySharp.Compilation;
using PySharp.PyModules.Builtins;
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
    private bool IsInteractive { get; set; }

    public void Compile()
    {
        CompileMod(Model.Root);
    }

    private void CompileMod(AstModNode node)
    {
        Debug.Assert(VariableScope is RootVariableScope);
        Generator.PushMetaInfo(node.MetaInfo);
        switch (node)
        {
            case ModuleNode n:
                foreach (var stmt in n.Body)
                    CompileStmt(stmt);
                break;

            case ExpressionNode n:
                LoadExpr(n.Body);
                Generator.Emit(OpCode.ReturnValue);
                break;

            case InteractiveNode n:
                IsInteractive = true;
                foreach (var stmt in n.Body)
                    CompileStmt(stmt);
                Generator.Emit(OpCode.LoadConst, PyNoneObject.None);
                Generator.Emit(OpCode.ReturnValue);
                break;

            default:
                throw new NotImplementedException();
        }
        Generator.PopMetaInfo();
    }
}
