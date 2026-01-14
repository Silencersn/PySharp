using PySharp.AstNodes;
using PySharp.CodeAnalysis;
using PySharp.PyModules.Builtins;
using PySharp.PyRuntime.Calls;
using PySharp.Utility;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace PySharp.PyRuntime;

internal enum FrameType
{
    Unknown = 0,

    MainRoot,
    ThreadRoot,
    Module,
    Function,
    Lambda,
    Class,
    Comprehension,
    Eval,
    Exec,
    YieldFunction,
    YieldLambda
}

public sealed partial class PyFrame
{
    internal string CallerName { get; }
    internal PyObject? Caller { get; }

    internal ICodeMetaInfoProvider? MetaInfoProvider { get; set; }
    private Dictionary<string, PyCellObject>? _closure = null;
    private PyFrameLocals _locals;
    internal PyFrameGlobals _globals;
    internal PyFrame? _outerNonInlineFrame;
    internal PyCellObject? ClassCell { get; set; }


    private PyFrame(PyFrame? back)
    {
        Back = back;
        _globals = new PyFrameGlobals();
        _locals = new PyFrameLocals(_globals);
        CallerName = "<module>";
        Caller = null;
        FrameType = back is null ? FrameType.MainRoot : FrameType.Module;
    }
    private PyFrame(PyFrameGlobals globals)
    {
        Back = null;
        _globals = globals;
        _locals = new PyFrameLocals(FrozenDictionary<string, int>.Empty);
        CallerName = $"<thread-{Environment.CurrentManagedThreadId}>";
        Caller = null;
        FrameType = FrameType.ThreadRoot;
    }
    private PyFrame(
        PyFrame back,
        PyFrameGlobals globals,
        PyFrameLocals locals,
        Dictionary<string, PyCellObject>? closure,
        string callerName,
        PyObject? caller,
        FrameType frameType)
    {
        Back = back;
        _globals = globals;
        _locals = locals;
        _closure = closure;
        CallerName = callerName;
        Caller = caller;
        FrameType = frameType;
    }

    public PyFrame? Back { get; internal set; }
    [MemberNotNullWhen(false, nameof(Back))]
    public bool IsRoot => Back is null;
    public ConcurrentDictionary<string, PyObject> Globals => _globals.Globals;
    public IDictionary<string, PyObject?> Locals => _locals.Locals;
    public Dictionary<string, PyCellObject> Closures => _closure ??= [];
    internal Dictionary<string, PyCellObject>? InternalClosure => _closure;
    public Stack<PyExceptionObject> Exceptions => field ??= [];
    public PyExceptionObject CurrentException => Exceptions.Peek();

    internal IReadOnlyDictionary<string, PyVariableType>? _variables = null;
    internal DictAdapter GlobalsAdapter => _globals.GlobalsAdapter;
    internal DictAdapter LocalsAdapter => field ??= new DictAdapter(Locals);
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
        FrozenDictionary<string, int> localVariablesToIndex)
    {
        Debug.Assert(frameType is FrameType.Function or FrameType.Lambda or FrameType.YieldFunction or FrameType.YieldLambda);
        return new PyFrame(
            this,
            globals ?? _globals,
            new(localVariablesToIndex ?? FrozenDictionary<string, int>.Empty),
            frameType is FrameType.Class ? _closure : null,
            callerName,
            caller,
            frameType)
        { CallingArguments = callingArguments };
    }

    internal PyFrame CreateClassBuildFrame(PyTypeObject buildingClass)
    {
        return new PyFrame(
            this,
            _globals,
            new(FrozenDictionary<string, int>.Empty),
            _closure,
            buildingClass.Name,
            buildingClass,
            FrameType.Class);
    }

    internal PyFrame CreateThreadRootFrame()
    {
        return new PyFrame(_globals);
    }

    internal PyFrame TempFrame(FrameType frameType)
    {
        Debug.Assert(frameType is FrameType.Exec or FrameType.Eval);

        var tempGlobals = _globals.Clone();
        PyFrameLocals tempLocals;
        if (ReferenceEquals(_locals._globals, _globals))
            tempLocals = new(tempGlobals);
        else
            tempLocals = _locals.Clone();
        var tempFrame = new PyFrame(this, tempGlobals, tempLocals, _closure, CallerName, Caller, frameType)
        {
            _variables = _variables
        };
        return tempFrame;
    }

    internal PyFrame CreateInlineFrame(FrameType frameType)
    {
        Debug.Assert(frameType is FrameType.Comprehension);

        var (globals, locals) = CloneGlobalsAndLocals(_globals, _locals);
        var inlineFrame = new PyFrame(this, globals, locals, _closure, CallerName, Caller, frameType)
        {
            _variables = _variables,
            _outerNonInlineFrame = _outerNonInlineFrame ?? this
        };
        return inlineFrame;

        static (PyFrameGlobals, PyFrameLocals) CloneGlobalsAndLocals(PyFrameGlobals globals, PyFrameLocals locals)
        {
            var cloneGlobals = globals.Clone();

            if (locals._globals is null)
                return (cloneGlobals, locals.Clone());

            // locals is created from globals in global scope
            Debug.Assert(ReferenceEquals(locals._globals, globals));
            var cloneLocals = new PyFrameLocals(cloneGlobals);
            return (cloneGlobals, cloneLocals);
        }
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

    public PyResult GetVariable(string name)
    {
        if (_variables is null)
            return LoadName(name);

        return _variables[name] switch
        {
            PyVariableType.Local or PyVariableType.Parameter => LoadLocal(name),
            PyVariableType.Global => LoadGlobal(name),
            PyVariableType.CapturedLocal or PyVariableType.CapturedParameter => LoadClosure(name, true),
            PyVariableType.Closure => LoadClosure(name, false),
            _ => throw new UnreachableException()
        };
    }

    public PyResult SetVariable(string name, PyObject value)
    {
        if (_variables is null)
            return StoreName(name, value);

        return _variables[name] switch
        {
            PyVariableType.Local or PyVariableType.Parameter => StoreLocal(name, value),
            PyVariableType.Global => StoreGlobal(name, value),
            PyVariableType.CapturedLocal or PyVariableType.CapturedParameter or PyVariableType.Closure => StoreClosure(name, value),
            _ => throw new UnreachableException()
        };
    }

    public PyResult DeleteVariable(string name)
    {
        if (_variables is null)
            return DeleteName(name);

        return _variables[name] switch
        {
            PyVariableType.Local or PyVariableType.Parameter => DeleteLocal(name),
            PyVariableType.Global => DeleteGlobal(name),
            PyVariableType.CapturedLocal or PyVariableType.CapturedParameter => DeleteClosure(name, true),
            PyVariableType.Closure => DeleteClosure(name, false),
            _ => throw new UnreachableException()
        };
    }

    public void Import(PyCallContext context, string name, string? alias = null)
    {
        if (!context.PyEnvironment.TryLoadModule(context, name, out var module))
            throw context.ThrowableModuleNotFoundError($"No module named '{name}'");

        SetVariable(alias ?? name, module);
    }
}
