using PySharp.Modules;
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
        frame.Variables.Globals[PySpecialNames.Builtins] = builtins;
        frame.Variables.Globals[PySpecialNames.Name] = PyStrObject.FromString(moduleQualifiedName);

        // TODO: add flag to control whether adding site
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

    internal readonly PyInternalFrame CreateClassBuildFrame(PyCodeObject code, PyTupleObject? closure)
    {
        var variables = Variables.CreateForBuildingClass(code, closure);

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
        // missing the interpreter's builtins are injected (_PyEval_EnsureBuiltins).
        // A user-provided __builtins__ value is never overwritten.
        if (globals is not null)
        {
            var builtinsKey = PySpecialNames.Builtins;
            if (!globals.ContainsKey(builtinsKey))
                globals[builtinsKey] = context.PyEnvironment.LoadBuiltinModule(context, "builtins");
        }

        var pyGlobals = globals ?? Variables.Globals;

        IPyStringKeyDict? localsDictionary = locals;

        if (closure is not null)
        {
            Debug.Assert(code is not null);
            var localsTable = code.FreeVars.Index().ToFrozenDictionary(static tuple => tuple.Item, static tuple => tuple.Index);
            localsDictionary = new PyFrameLocalsProxyObject(localsTable, closure.InternalArray);
        }

        var variables = PyVariables.CreateExecEval(pyGlobals, localsDictionary);
        return new PyInternalFrame(variables, Caller, frameType) { CodeObject = code };
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
            // TODO: perf
            localsSpan[index] = PyDictObject.CreateDict(PyCallContext.NotImplemented, arguments.ExtraKwargs.Select(static kvp => KeyValuePair.Create((PyObject)PyStrObject.FromString(kvp.Key), kvp.Value))).Value!;
    }
}
