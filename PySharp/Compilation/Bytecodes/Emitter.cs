using PySharp.Compilation.AstNodes;
using PySharp.Compilation.CodeAnalysis;
using PySharp.Modules.Builtins;
using PySharp.Runtime;
using PySharp.Runtime.Calls;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace PySharp.Compilation.Bytecodes;

internal sealed partial class Emitter
{
    public static Bytecode Emit(PyCallContext context, SemanticModel model, CodeSource source, bool onlyAsName = false)
    {
        var emitter = new Emitter(context, model, source) { OnlyAsName = onlyAsName };
        emitter.Emit();
        return emitter.Builder.ToBytecode();
    }

    private readonly PyCallContext _context;
    private readonly SemanticModel _model;
    private readonly CodeSource _source;

    internal Emitter(PyCallContext context, SemanticModel model, CodeSource source)
    {
        Builder = BytecodeBuilder.Create(source);
        _context = context;
        _model = model;
        _source = source;
        var scope = _model.GetVariableScope<RootVariableScope>(_model.Root);
        Debug.Assert(scope is not null);
        VariableScope = scope;
    }

    private BytecodeBuilder Builder { get; set; }
    private SemanticModel Model => _model;
    private int OptimizationLevel => _context.PyEnvironment.OptimizationLevel;
    private VariableScope VariableScope { get; set; }
    private Stack<(Label LoopBegin, Label LoopEnd)> Loops { get; } = [];
    private bool IsInteractive { get; set; }
    private bool OnlyAsName { get; set; }

    public void Emit()
    {
        EmitMod(Model.Root);
    }

    private void EmitMod(AstModNode node)
    {
        Debug.Assert(VariableScope is RootVariableScope);
        Builder.PushMetaInfo(node.MetaInfo);
        switch (node)
        {
            case ModuleNode n:
                if (TryGetDoc(n.Body, out var doc))
                {
                    Builder.Emit(OpCode.LoadConst, doc);
                    StoreName(PySpecialNames.Doc);
                }
                EmitStmts(n.Body);
                break;

            case ExpressionNode n:
                LoadExpr(n.Body);
                Builder.Emit(OpCode.ReturnValue);
                break;

            case InteractiveNode n:
                IsInteractive = true;
                EmitStmts(n.Body);
                break;

            default:
                throw new NotImplementedException();
        }
        Builder.PopMetaInfo();
    }

    private static bool TryGetDoc(IReadOnlyList<AstStmtNode> stmtNodes, [NotNullWhen(true)] out PyStrObject? doc)
    {
        if (stmtNodes.Count > 0 &&
            stmtNodes[0] is ExprNode exprNode &&
            exprNode.Value is ConstantNode constantNode &&
            constantNode.Value is PyStrObject strObj)
        {
            doc = strObj;
            return true;
        }

        doc = null;
        return false;
    }

    /// <summary>
    /// Emit bundled defaults and kwdefaults tuples, matching CPython convention.
    /// Stack: [defaults_tuple, kwdefaults_tuple]  (ready for <c>_MakeFunctionWithPyArgsDef</c>)
    /// </summary>
    private void EmitFunctionDefaults(AstArgumentsNode args)
    {
        int defCount = args.Defaults.Length;
        int kwDefCount = args.KwDefaults.Length;

        if (defCount > 0)
        {
            foreach (var d in args.Defaults)
                LoadExpr(d);
            Builder.Emit(OpCode.BuildTuple, defCount);
        }
        else
        {
            Builder.Emit(OpCode.LoadConst, PyTupleObject.Empty);
        }

        if (kwDefCount > 0)
        {
            foreach (var d in args.KwDefaults)
            {
                if (d is not null)
                    LoadExpr(d);
                else
                    Builder.Emit(OpCode.PushNull);
            }
            Builder.Emit(OpCode.BuildTuple, kwDefCount);
        }
        else
        {
            Builder.Emit(OpCode.LoadConst, PyTupleObject.Empty);
        }
    }
}
