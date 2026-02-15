using PySharp.AstNodes;
using PySharp.PyRuntime;
using System.Diagnostics;

namespace PySharp.Bytecodes;

internal sealed partial class BytecodeCompiler
{
    public static Bytecode Compile(SemanticModel model, bool onlyAsName = false)
    {
        var compiler = new BytecodeCompiler(model) { OnlyAsName = onlyAsName };
        compiler.Compile();
        return compiler.Generator.ToBytecode();
    }

    private readonly SemanticModel _model;

    internal BytecodeCompiler(SemanticModel model)
    {
        Generator = BytecodeGenerator.Create();
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
    private bool OnlyAsName { get; set; }

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
                if (AstUtils.TryGetDoc(n.Body, out var doc))
                {
                    Generator.Emit(OpCode.LoadConst, doc);
                    StoreName(PySpecialNames.Doc);
                }
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
                break;

            default:
                throw new NotImplementedException();
        }
        Generator.PopMetaInfo();
    }
}
