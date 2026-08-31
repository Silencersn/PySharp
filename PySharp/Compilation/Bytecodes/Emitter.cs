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
        Builder = new BytecodeBuilder(source);
        _context = context;
        _model = model;
        _source = source;
        var scope = _model.GetVariableScope<RootVariableScope>(_model.Root);
        Debug.Assert(scope is not null);
        VariableScope = scope;
    }

    private BytecodeBuilder Builder { get; set; }
    private SemanticModel Model => _model;
    private int OptimizationLevel => _context.PyEnvironment.Options.OptimizationLevel;
    private VariableScope VariableScope { get; set; }
    private Stack<(Label LoopBegin, Label LoopEnd)> Loops { get; } = [];
    private Stack<int> ForDepth { get; } = [];
    private int CurrentForDepth { get; set; }
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
                if (OptimizationLevel < 2 && TryGetDoc(n.Body, out var doc))
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
                throw new UnreachableException();
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

    /// <summary>
    /// Saves and restores Builder/VariableScope around a sub-scope emission.
    /// Usage:
    /// <code>
    /// using var sub = new EmitterSubScope(this, scope);
    /// // emit body...
    /// var codeObj = new PyCodeObject(name, scope, Builder.ToBytecode());
    /// </code>
    /// </summary>
    internal readonly ref struct EmitterSubScope
    {
        private readonly Emitter _emitter;
        private readonly BytecodeBuilder _savedBuilder;
        private readonly VariableScope _savedScope;

        public EmitterSubScope(Emitter emitter, VariableScope scope)
        {
            _emitter = emitter;
            _savedBuilder = emitter.Builder;
            _savedScope = emitter.VariableScope;
            emitter.Builder = new BytecodeBuilder(emitter._source);
            emitter.VariableScope = scope;
            if (scope is FunctionVariableScope or AsyncFunctionVariableScope)
            {
                emitter.ForDepth.Push(emitter.CurrentForDepth);
                emitter.CurrentForDepth = 0;
            }
        }

        public void Dispose()
        {
            if (_emitter.VariableScope is FunctionVariableScope or AsyncFunctionVariableScope)
                _emitter.CurrentForDepth = _emitter.ForDepth.Pop();

            _emitter.Builder = _savedBuilder;
            _emitter.VariableScope = _savedScope;
        }
    }
}
