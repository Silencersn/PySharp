using PySharp.Compilation.AstNodes;
using PySharp.Compilation.CodeAnalysis;
using PySharp.Modules.Builtins;
using PySharp.Runtime;
using PySharp.Runtime.Calls;
using System.Collections.Immutable;
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
    private Stack<EmitterRegion> Regions { get; } = [];
    private bool IsInteractive { get; set; }
    private bool OnlyAsName { get; set; }

    /// <summary>
    /// A compile-time region covering a loop, with-item or try record, mirroring
    /// the runtime handler records a jump out of the region must dispose of.
    /// break/continue/return unwind these inline (codegen.c fblock analogue).
    /// </summary>
    private sealed class EmitterRegion
    {
        public required EmitterRegionKind Kind;
        // loops: jump targets (the loop's own stack residency is handled there)
        public Label? LoopBegin;
        public Label? LoopEnd;
        // TryStatement: finally body copied inline on unwind
        public ImmutableArray<AstStmtNode> FinalBody;
        // ExceptHandlerBody: `except E as name` implicit del on unwind
        public string? ExceptName;
    }

    private enum EmitterRegionKind
    {
        ForLoop,            // 1 resident iterator, popped by the jump target / return unwind
        WhileLoop,          // no stack residency
        WithItem,           // [exit, manager] resident + 1 handler
        AsyncWithItem,      // [aexit, manager] resident + 1 handler, exit awaits
        TryStatement,       // 1 handler; unwind copies the finally body inline
        ExceptHandlerBody,  // inside a running except handler (+ name-cleanup handler when named)
        FinallyBody,        // the finally body itself: 1 handler, nothing copied
        AsyncForAwait,      // async-for await wrapper around the body: 1 handler
        SavedValue,         // a TOS value riding below an inline finally copy
    }

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
        private readonly EmitterRegion[] _savedRegions;

        public EmitterSubScope(Emitter emitter, VariableScope scope)
        {
            _emitter = emitter;
            _savedBuilder = emitter.Builder;
            _savedScope = emitter.VariableScope;
            _savedRegions = [.. emitter.Regions];
            emitter.Builder = new BytecodeBuilder(emitter._source);
            emitter.VariableScope = scope;
            // a sub scope emits a separate code object: its jumps can never
            // unwind the enclosing code object's regions
            emitter.Regions.Clear();
        }

        public void Dispose()
        {
            _emitter.Regions.Clear();
            // ToArray() is top-first, so replay it back-to-front
            for (int i = _savedRegions.Length - 1; i >= 0; i--)
                _emitter.Regions.Push(_savedRegions[i]);

            _emitter.Builder = _savedBuilder;
            _emitter.VariableScope = _savedScope;
        }
    }

    /// <summary>
    /// Temporarily switches VariableScope (only), e.g. while emitting an
    /// inlined comprehension body; the builder and code object stay the
    /// enclosing ones. Stack-only like <see cref="EmitterSubScope"/>.
    /// </summary>
    internal readonly ref struct EmitterVariableScopeSwitch
    {
        private readonly Emitter _emitter;
        private readonly VariableScope _savedScope;

        public EmitterVariableScopeSwitch(Emitter emitter, VariableScope scope)
        {
            _emitter = emitter;
            _savedScope = emitter.VariableScope;
            emitter.VariableScope = scope;
        }

        public void Dispose()
        {
            _emitter.VariableScope = _savedScope;
        }
    }
}
