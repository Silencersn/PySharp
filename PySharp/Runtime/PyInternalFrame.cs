using PySharp.Compilation.CodeAnalysis;
using PySharp.Modules.Builtins;
using PySharp.Runtime.Calls;
using PySharp.Utility;
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
    internal string CallerName;
    internal PyFrameVariables Variables;
    internal PyObject? Caller;
    internal PyCodeObject? CodeObject;
    internal ICodeMetaInfoProvider? MetaInfoProvider;
    internal Stack<PyExceptionObject>? _exceptions;
    internal (IReadOnlyList<PyObject> Args, IReadOnlyDictionary<string, PyObject> Kwargs)? CallingArguments;
    internal int OuterNonInlineFrameIndex = -1;
    internal int BackFrameIndex = -1;
    internal FrameType FrameType;

    internal readonly bool IsRoot => BackFrameIndex is -1;
    internal Stack<PyExceptionObject> Exceptions => _exceptions ??= [];
    internal PyExceptionObject CurrentException => Exceptions.Peek();

    private PyInternalFrame(PyCallContext context, bool isRoot)
    {
        BackFrameIndex = isRoot ? -1 : context.FrameState.CurrentFrameIndex;
        Variables = PyFrameVariables.CreateModule();
        CallerName = "<module>";
        Caller = null;
        FrameType = isRoot ? FrameType.MainRoot : FrameType.Module;
    }
    private PyInternalFrame(PyFrameVariables variables)
    {
        BackFrameIndex = -1;
        Variables = PyFrameVariables.Create(variables._globals, FrozenDictionary<string, int>.Empty);
        CallerName = $"<thread-{Environment.CurrentManagedThreadId}>";
        Caller = null;
        FrameType = FrameType.ThreadRoot;
    }
    private PyInternalFrame(
        PyCallContext context,
        PyFrameVariables variables,
        string callerName,
        PyObject? caller,
        FrameType frameType)
    {
        BackFrameIndex = context.FrameState.CurrentFrameIndex;
        Variables = variables;
        CallerName = callerName;
        Caller = caller;
        FrameType = frameType;
    }

    public readonly void Dispose(PyCallContext context)
    {
        Variables.Dispose(context);
    }

    internal static PyInternalFrame CreateModuleFrame(PyCallContext context, bool isRoot, string moduleQualifiedName)
    {
        var frame = new PyInternalFrame(context, isRoot);
        var builtins = context.PyEnvironment.LoadBuiltinModule(context, "builtins");
        frame.SetVariable(PySpecialNames.Builtins, builtins);
        frame.SetVariable(PySpecialNames.Name, PyStrObject.FromString(moduleQualifiedName));

        // TODO: add flag to control whether adding site
        _ = context.PyEnvironment.LoadBuiltinModule(context, "site");

        return frame;
    }
    internal static PyInternalFrame CreateFuncCallFrame(PyCallContext context, string callerName, PyObject caller, FrameType frameType,
        (IReadOnlyList<PyObject> Args, IReadOnlyDictionary<string, PyObject> Kwargs) callingArguments,
        PyFrameGlobals globals,
        PyCodeObject code)
    {
        Debug.Assert(frameType is FrameType.Function);

        var variables = code.Flags is CodeObjectFlags.Function ?
            PyFrameVariables.CreateForCommonFunctionCall(context, globals, code) :
            PyFrameVariables.Create(globals, code.LocalsTable);

        return new PyInternalFrame(
            context,
            variables,
            callerName,
            caller,
            frameType)
        { CallingArguments = callingArguments, CodeObject = code };
    }

    internal readonly PyInternalFrame CreateClassBuildFrame(PyCallContext context, PyTypeObject buildingClass, PyCodeObject code)
    {
        var variables = PyFrameVariables.Create(Variables._globals,
            Variables._locals?.ToClassClosure(code) ?? new PyFrameLocals(FrozenDictionary<string, int>.Empty));

        return new PyInternalFrame(
            context,
            variables,
            buildingClass.Name,
            buildingClass,
            FrameType.Class);
    }

    internal readonly PyInternalFrame CreateThreadRootFrame()
    {
        return new PyInternalFrame(Variables);
    }

    internal readonly PyInternalFrame CreateExecEvalFrame(PyCallContext context, FrameType frameType, PyDictObject? globals, PyDictObject? locals, PyTupleObject? closure = null, PyCodeObject? code = null)
    {
        Debug.Assert(frameType is FrameType.Exec or FrameType.Eval);

        var globalVariables = globals is null ? Variables._globals : new PyFrameGlobals(globals);
        if (!globalVariables.Globals.ContainsKey(PySpecialNames.Builtins))
            globalVariables.Globals[PySpecialNames.Builtins] = new PyBuiltinsModuleObject();

        var localVariables = locals is null ? null : new PyFrameLocals(locals);
        if (closure is not null)
        {
            Debug.Assert(code is not null);
            localVariables ??= new PyFrameLocals(code.FreeVars.Index().ToFrozenDictionary(static tuple => tuple.Item, static tuple => tuple.Index));
            localVariables.InitCells(UnsafeUtils.CastReadOnlySpan<PyObject, PyCellObject>(closure.AsSpan()));
        }

        var variables = PyFrameVariables.Create(globalVariables, localVariables);
        return new PyInternalFrame(context, variables, CallerName, Caller, frameType);
    }

    internal readonly PyInternalFrame CreateInlineFrame(PyCallContext context, FrameType frameType)
    {
        Debug.Assert(frameType is FrameType.Comprehension);

        var variables = Variables.Clone();
        var inlineFrame = new PyInternalFrame(context, variables, CallerName, Caller, frameType)
        {
            OuterNonInlineFrameIndex = OuterNonInlineFrameIndex is -1 ? context.FrameState.CurrentFrameIndex : OuterNonInlineFrameIndex,
            MetaInfoProvider = MetaInfoProvider
        };
        return inlineFrame;
    }

    internal readonly void InitArgs(PyArgsDef def, PyCodeObject code, PyArguments arguments)
    {
        Debug.Assert(Variables._locals is not null);
        var localsSpan = Variables._locals.LocalsSpan;
        arguments.InternalArgs.CopyTo(localsSpan!);

        if (arguments.Kwargs is not null)
        {
            foreach (var kwarg in arguments.Kwargs)
                Variables.StoreLocal(kwarg.Key, kwarg.Value);
        }

        var index = code.ArgCount + code.KwOnlyArgCount;
        if (def.VarArg is not null)
            Variables.StoreFast(index++, PyTupleObject.CreateTuple(arguments.ExtraArgs));
        if (def.KwArg is not null)
            Variables.StoreFast(index, PyDictObject.CreateDict(arguments.ExtraKwargs.Select(static kvp => KeyValuePair.Create((PyObject)PyStrObject.FromString(kvp.Key), kvp.Value))));
    }

    public readonly PyResult SetVariable(string name, PyObject value)
    {
        if (CodeObject is not null && (CodeObject.CellVars.Contains(name) || CodeObject.FreeVars.Contains(name)))
            return Variables.StoreDeref(name, value);
        else
            return Variables.StoreLocal(name, value);
    }

}
