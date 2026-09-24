using PySharp.Modules.Builtins;
using PySharp.Runtime.Calls;
using System.Collections.Frozen;
using System.Diagnostics;

namespace PySharp.Runtime;

internal enum FrameType
{
    Unknown = 0,

    MainRoot,
    ThreadRoot,
    Module,
    Function,
    Class,
    Comprehension,
    Eval,
    Exec,
}

internal partial struct PyInternalFrame
{
    internal PyVariables Variables;
    internal PyObject? Caller;
    internal PyCodeObject? CodeObject;
    internal FrameType FrameType;
    internal int InstructionIndex;

    // currently, only thread root frame do not have code object at runtime
    internal readonly string CallerName => CodeObject is null ?
        $"<thread-{Environment.CurrentManagedThreadId}>" :
        CodeObject.Flags is CodeObjectFlags.Module ?
        "<module>" :
        CodeObject.Name;

    private PyInternalFrame(bool isRoot)
    {
        Variables = PyVariables.CreateGlobal();
        Caller = null;
        FrameType = isRoot ? FrameType.MainRoot : FrameType.Module;
    }
    private PyInternalFrame(PyVariables variables)
    {
        Variables = variables.CreatePlaceholder();
        Caller = null;
        FrameType = FrameType.ThreadRoot;
    }
    private PyInternalFrame(
        PyVariables variables,
        PyObject? caller,
        FrameType frameType)
    {
        Variables = variables;
        Caller = caller;
        FrameType = frameType;
    }

    public readonly void Dispose(PyCallContext context)
    {
        Variables.Dispose(context);
    }

    internal static PyInternalFrame CreateModuleFrame(PyCallContext context, bool isRoot, string moduleQualifiedName)
    {
        var frame = new PyInternalFrame(isRoot);
        var builtins = context.PyEnvironment.LoadBuiltinModule(context, "builtins");
        // CPython installs the builtins *module* in __main__ only
        // (pylifecycle.c add_main_module); every other module gets
        // interp->builtins, i.e. the builtins module's own namespace dict
        // (import.c module_dict_for_exec). Both forms are accepted on lookup
        // (_PyDict_LoadBuiltinsFromGlobals unwraps a module), so only the
        // entry read back from globals differs.
        frame.Variables.Globals[PySpecialNames.Builtins] = isRoot
            ? builtins
            : builtins.PyAttributesDict;
        frame.Variables.Globals[PySpecialNames.Name] = PyStrObject.FromString(moduleQualifiedName);

        if (!context.PyEnvironment.Options.NotImplyImportSite)
            _ = context.PyEnvironment.LoadBuiltinModule(context, "site");

        return frame;
    }
    internal static PyInternalFrame CreateFuncCallFrame(PyCallContext context, PyObject caller,
        FrameType frameType, PyDictObject globals,
        PyCodeObject code)
    {
        Debug.Assert(frameType is FrameType.Function);

        var variables = code.Flags is CodeObjectFlags.Function ?
            PyVariables.CreateUsingStackMemoryAllocator(globals, context, code) :
            PyVariables.CreateUsingArrayPool(globals, code.LocalsTable);

        return new PyInternalFrame(
            variables,
            caller,
            frameType)
        { CodeObject = code };
    }

    internal readonly PyInternalFrame CreateClassBuildFrame(PyCodeObject code, PyTupleObject? closure, IPyVariablesLocalsDict? classLocals = null)
    {
        var variables = Variables.CreateForBuildingClass(code, closure, classLocals);

        return new PyInternalFrame(
            variables,
            caller: null, // deferred assignment
            FrameType.Class)
        { CodeObject = code };
    }

    // PEP 649: the __annotate__ code object is a class-body variant, so it
    // runs in a class frame — but with the class's own module globals instead
    // of the accessing frame's.
    internal static PyInternalFrame CreateAnnotateFrame(
        PyCodeObject code, PyTupleObject? closure, IPyVariablesLocalsDict classLocals, PyDictObject globals)
    {
        var variables = PyVariables.CreateForAnnotate(code, closure, classLocals, globals);

        return new PyInternalFrame(
            variables,
            caller: null, // deferred assignment
            FrameType.Class)
        { CodeObject = code };
    }

    internal readonly PyInternalFrame CreateThreadRootFrame()
    {
        return new PyInternalFrame(Variables);
    }

    internal readonly PyInternalFrame CreateExecEvalFrame(PyCallContext context, FrameType frameType, PyDictObject? globals, PyDictObject? locals, PyCodeObject? code = null, PyTupleObject? closure = null)
    {
        Debug.Assert(frameType is FrameType.Exec or FrameType.Eval);

        // Mirror CPython: builtins are resolved from globals["__builtins__"] at
        // frame creation (_PyDict_LoadBuiltinsFromGlobals); when the key is
        // missing the running frame's builtins are injected (_PyEval_EnsureBuiltins
        // writes PyEval_GetBuiltins()). A user-provided __builtins__ value is
        // never overwritten.
        if (globals is not null)
        {
            var builtinsKey = PySpecialNames.Builtins;
            if (!globals.ContainsKey(builtinsKey))
                globals[builtinsKey] = ResolveRunningBuiltins(context);
        }

        var pyGlobals = globals ?? Variables.Globals;

        IPyVariablesLocalsDict? localsDictionary = locals;

        // CPython builtin_eval/exec_impl: when globals is omitted, locals
        // also defaults to the calling frame's locals (fromframe), not to
        // the (also defaulted) globals — a snapshot for optimized frames,
        // the live mapping for unoptimized ones (class namespace); locals
        // defaults to globals only when globals was passed explicitly.
        if (locals is null && globals is null)
        {
            ref var owner = ref context.FrameState.FindOuterNonInlineFrame();

            // An inlined comprehension frame (PEP 709): eval sees the
            // comprehension's own targets together with the owner function's
            // locals — they share one frame in CPython. Other frames keep
            // the classic defaulting: the owner function's locals snapshot,
            // or this frame's live mapping (class namespace).
            if (FrameType is FrameType.Comprehension)
            {
                PyDictObject? merged = owner.FrameType is FrameType.Function && owner.Variables.HasLocals
                    ? owner.Variables.GetLocals(context)
                    : null;
                if (Variables.HasLocals)
                {
                    var own = Variables.LocalsMapping as PyDictObject ?? Variables.GetLocals(context);
                    merged ??= new PyDictObject();
                    foreach (var pair in own.Entries)
                        merged.SetItem(context, pair.Key, pair.Value);
                }
                localsDictionary = merged;
            }
            else if (owner.FrameType is FrameType.Function && owner.Variables.HasLocals)
            {
                localsDictionary = owner.Variables.GetLocals(context);
            }
            else if (Variables.HasLocals)
            {
                localsDictionary = Variables.LocalsMapping as PyDictObject ?? Variables.GetLocals(context);
            }
        }

        if (closure is not null)
        {
            Debug.Assert(code is not null);
            var localsTable = code.FreeVars.Index().ToFrozenDictionary(static tuple => tuple.Item, static tuple => tuple.Index);
            localsDictionary = new PyFrameLocalsProxyObject(localsTable, closure.InternalArray);
        }

        var variables = PyVariables.CreateExecEval(pyGlobals, localsDictionary);
        return new PyInternalFrame(variables, Caller, frameType) { CodeObject = code };
    }

    // The value CPython's PyEval_GetBuiltins() yields for this (the running)
    // frame: the mapping _PyDict_LoadBuiltinsFromGlobals derives from the
    // frame's own globals["__builtins__"], where a module is unwrapped to its
    // namespace dict, and anything else — a user mapping, None — is taken
    // as-is. A frame without the entry falls back to interp->builtins, the
    // builtins module's dict.
    private readonly PyObject ResolveRunningBuiltins(PyCallContext context)
    {
        if (Variables.Globals.TryGetValue(PySpecialNames.Builtins, out var builtins))
            return builtins is PyModuleObject module ? module.PyAttributesDict : builtins;
        return context.PyEnvironment.LoadBuiltinModule(context, "builtins").PyAttributesDict;
    }

    internal readonly PyInternalFrame CreateInlineFrame()
    {
        var variables = Variables.CreateInline();
        var inlineFrame = new PyInternalFrame(variables, Caller, FrameType.Comprehension)
        {
            CodeObject = CodeObject
        };
        return inlineFrame;
    }

    internal readonly void InitArgs(PyArgsDef def, PyCodeObject code, PyArguments arguments, ReadOnlySpan<PyCellObject> closure)
    {
        var localsSpan = Variables.LocalsSpan;
        arguments.ArgsAndKwargs.CopyTo(localsSpan!);
        ReadOnlySpan<PyObject>.CastUp(closure).CopyTo(localsSpan[^closure.Length..]!);

        var index = code.ArgCount + code.KwOnlyArgCount;
        if (def.VarArg is not null)
            localsSpan[index++] = arguments.InternalExtraArgs.Length is 0 ? PyTupleObject.Empty : PyTupleObject.CreateProxy(arguments.InternalExtraArgs);
        if (def.KwArg is not null)
            localsSpan[index] = PyDictObject.CreateDict(arguments.ExtraKwargs);
    }
}
