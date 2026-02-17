using PySharp.Compilation.AstNodes;
using PySharp.Compilation.CodeAnalysis;
using PySharp.Modules.Builtins;
using PySharp.Runtime.Calls;
using PySharp.Utility;
using System.Collections.Frozen;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

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

public sealed partial class PyFrame
{
    internal string CallerName { get; }
    internal PyObject? Caller { get; }
    internal PyCodeObject? CodeObject { get; set; }

    internal ICodeMetaInfoProvider? MetaInfoProvider { get; set; }
    private readonly PyFrameVariables _frameVariables;
    internal PyFrame? _outerNonInlineFrame;
    internal PyFrameVariables Variables => _frameVariables;

    private PyFrame(PyFrame? back)
    {
        Back = back;
        _frameVariables = PyFrameVariables.CreateModule();
        CallerName = "<module>";
        Caller = null;
        FrameType = back is null ? FrameType.MainRoot : FrameType.Module;
    }
    private PyFrame(PyFrameVariables variables)
    {
        Back = null;
        _frameVariables = PyFrameVariables.Create(variables._globals, null, 0);
        CallerName = $"<thread-{Environment.CurrentManagedThreadId}>";
        Caller = null;
        FrameType = FrameType.ThreadRoot;
    }
    private PyFrame(
        PyFrame back,
        PyFrameVariables variables,
        string callerName,
        PyObject? caller,
        FrameType frameType)
    {
        Back = back;
        _frameVariables = variables;
        CallerName = callerName;
        Caller = caller;
        FrameType = frameType;
    }

    public PyFrame? Back { get; internal set; }
    [MemberNotNullWhen(false, nameof(Back))]
    public bool IsRoot => Back is null;
    public Stack<PyExceptionObject> Exceptions => field ??= [];
    public PyExceptionObject CurrentException => Exceptions.Peek();
    internal FrameType FrameType { get; }
    internal (IReadOnlyList<PyObject> Args, IReadOnlyDictionary<string, PyObject> Kwargs)? CallingArguments { get; init; }

    internal static PyFrame CreateModuleFrame(PyCallContext context, PyFrame? back)
    {
        var frame = new PyFrame(back);
        var builtins = context.PyEnvironment.LoadBuiltinModule(context, "builtins");
        frame.SetVariable(PySpecialNames.Builtins, builtins);
        if (back is null)
            frame.SetVariable(PySpecialNames.Name, PyStrObject.FromString(PySpecialNames.Main));

        // TODO: add flag to control whether adding site
        _ = context.PyEnvironment.LoadBuiltinModule(context, "site");

        return frame;
    }
    internal PyFrame CreateFuncCallFrame(string callerName, PyObject caller, FrameType frameType,
        (IReadOnlyList<PyObject> Args, IReadOnlyDictionary<string, PyObject> Kwargs) callingArguments,
        PyFrameGlobals globals,
        PyCodeObject code)
    {
        Debug.Assert(frameType is FrameType.Function);

        var variables = PyFrameVariables.Create(globals, code.LocalsTable, code.CellVars.Length + code.FreeVars.Length);

        return new PyFrame(
            this,
            variables,
            callerName,
            caller,
            frameType)
        { CallingArguments = callingArguments, CodeObject = code };
    }

    internal PyFrame CreateClassBuildFrame(PyTypeObject buildingClass)
    {
        var variables = PyFrameVariables.Create(_frameVariables._globals,
            _frameVariables._locals?.ToClassClosure() ?? new PyFrameLocals(FrozenDictionary<string, int>.Empty, 0));

        return new PyFrame(
            this,
            variables,
            buildingClass.Name,
            buildingClass,
            FrameType.Class);
    }

    internal PyFrame CreateThreadRootFrame()
    {
        return new PyFrame(_frameVariables);
    }

    internal PyFrame CreateExecEvalFrame(FrameType frameType, PyDictObject? globals, PyDictObject? locals, PyTupleObject? closure = null, PyCodeObject? code = null)
    {
        Debug.Assert(frameType is FrameType.Exec or FrameType.Eval);

        var globalVariables = globals is null ? _frameVariables._globals : new PyFrameGlobals(globals);
        if (!globalVariables.Globals.ContainsKey(PySpecialNames.Builtins))
            globalVariables.Globals[PySpecialNames.Builtins] = new PyBuiltinsModuleObject();

        var localVariables = locals is null ? null : new PyFrameLocals(locals);
        if (closure is not null)
        {
            Debug.Assert(code is not null);
            localVariables ??= new PyFrameLocals(code.FreeVars.Index().ToFrozenDictionary(static tuple => tuple.Item, static tuple => tuple.Index), code.FreeVars.Length);
            localVariables.InitCells(UnsafeUtils.CastReadOnlySpan<PyObject, PyCellObject>(closure._array));
        }

        var variables = PyFrameVariables.Create(globalVariables, localVariables);
        return new PyFrame(this, variables, CallerName, Caller, frameType);
    }

    internal PyFrame CreateInlineFrame(FrameType frameType)
    {
        Debug.Assert(frameType is FrameType.Comprehension);

        var variables = _frameVariables.Clone();
        var inlineFrame = new PyFrame(this, variables, CallerName, Caller, frameType)
        {
            _outerNonInlineFrame = _outerNonInlineFrame ?? this,
            MetaInfoProvider = MetaInfoProvider
        };
        return inlineFrame;
    }

    internal void InitArgs(PyArgsDef def, PyArguments arguments)
    {
        for (int i = 0; i < def.PosonlyArgs.Length; i++)
        {
            SetVariable(def.PosonlyArgs[i], arguments.Args[i]);
        }
        for (int i = 0; i < def.Args.Length; i++)
        {
            var index = i + def.PosonlyArgs.Length;
            SetVariable(def.Args[i], arguments.Args[index]);
        }
        foreach (var kwarg in arguments.Kwargs)
        {
            SetVariable(kwarg.Key, kwarg.Value);
        }

        if (def.VarArg is not null)
            SetVariable(def.VarArg, PyTupleObject.CreateTuple(arguments.ExtraArgs));
        if (def.KwArg is not null)
            SetVariable(def.KwArg, PyDictObject.CreateDict(arguments.ExtraKwargs.Select(static kvp => KeyValuePair.Create((PyObject)PyStrObject.FromString(kvp.Key), kvp.Value))));
    }

    public PyResult SetVariable(string name, PyObject value)
    {
        if (CodeObject is not null && (CodeObject.CellVars.Contains(name) || CodeObject.FreeVars.Contains(name)))
            return Variables.StoreDeref(name, value);
        else
            return Variables.StoreLocal(name, value);
    }
}
