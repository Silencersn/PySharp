using PySharp.AstNodes;
using PySharp.PyModules.Builtins;
using PySharp.PyRuntime.Calls;
using PySharp.PyRuntime.Metadata;
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

    internal IMetaInfoProvider? ExprMetaInfoProvider { get; set; }
    internal IMetaInfoProvider? StmtMetaInfoProvider { get; set; }
    private Dictionary<string, PyCellObject>? _closure = null;
    private IDictionary<string, PyObject?>? _locals = null;
    internal PyFrameGlobals _globals;

    private PyFrame(PyFrame? back)
    {
        Back = back;
        _globals = new PyFrameGlobals();
        _locals = Globals!;
        CallerName = "<module>";
        Caller = null;
        FrameType = back is null ? FrameType.MainRoot : FrameType.Module;
    }
    private PyFrame(PyFrameGlobals globals)
    {
        Back = null;
        _globals = globals;
        _locals = null;
        CallerName = $"<thread-{Environment.CurrentManagedThreadId}>";
        Caller = null;
        FrameType = FrameType.ThreadRoot;
    }
    private PyFrame(
        PyFrame back,
        PyFrameGlobals globals,
        Dictionary<string, PyObject?>? locals,
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
    public IDictionary<string, PyObject?> Locals => _locals ??= new Dictionary<string, PyObject?>();
    public Dictionary<string, PyCellObject> Closures => _closure ??= [];
    internal Dictionary<string, PyCellObject>? InternalClosure => _closure;
    public Stack<PyExceptionObject> Exceptions => field ??= [];
    public PyExceptionObject CurrentException => Exceptions.Peek();

    internal FrozenDictionary<string, PyVariableType>? _variables = null;
    internal DictAdapter GlobalsAdapter => _globals.GlobalsAdapter;
    internal DictAdapter LocalsAdapter => field ??= new DictAdapter(Locals);
    internal FrameType FrameType { get; }
    internal (IReadOnlyList<PyObject> Args, IReadOnlyDictionary<string, PyObject> Kwargs)? CallingArguments { get; init; }

    internal static PyFrame CreateModuleFrame(PyCallContext context, PyFrame? back)
    {
        var frame = new PyFrame(back);
        var builtins = context.PyEnvironment.LoadBuiltinModule(context, "builtins");
        frame.SetValue(PySpecialNames.Builtins, builtins);
        if (back is null)
            frame.SetValue(PySpecialNames.Name, PyStrObject.FromString(PySpecialNames.Main));

        // TODO: add flag to control whether adding site
        _ = context.PyEnvironment.LoadBuiltinModule(context, "site");

        return frame;
    }
    internal PyFrame CreateFuncCallOrClassBuildFrame(string callerName, PyObject caller, FrameType frameType,
        (IReadOnlyList<PyObject> Args, IReadOnlyDictionary<string, PyObject> Kwargs)? callingArguments = null,
        PyFrameGlobals? globals = null)
    {
        Debug.Assert(frameType is FrameType.Function or FrameType.Lambda or FrameType.Class or FrameType.YieldFunction or FrameType.YieldLambda);
        return new PyFrame(this, globals ?? _globals, null, null, callerName, caller, frameType) { CallingArguments = callingArguments };
    }
    internal PyFrame CreateThreadRootFrame()
    {
        return new PyFrame(_globals);
    }

    internal PyFrame TempFrame(FrameType frameType)
    {
        Debug.Assert(frameType is FrameType.Comprehension or FrameType.Exec or FrameType.Eval);
        var tempFrame = new PyFrame(this, _globals, _locals?.ToDictionary(), _closure, CallerName, Caller, frameType)
        {
            _variables = _variables
        };
        return tempFrame;
    }

    internal void InitArgs(PyArgsDef def, PyArguments arguments)
    {
        for (int i = 0; i < def.PosonlyArgs.Length; i++)
        {
            SetValue(def.PosonlyArgs[i], arguments.Args[i]);
        }
        for (int i = 0; i < def.Args.Length; i++)
        {
            var index = i + def.PosonlyArgs.Length;
            SetValue(def.Args[i], arguments.Args[index]);
        }
        foreach (var kwarg in arguments.Kwargs)
        {
            SetValue(kwarg.Key, kwarg.Value);
        }

        if (def.VarArg is not null)
            SetValue(def.VarArg, PyTupleObject.CreateTuple(arguments.ExtraArgs));
        if (def.KwArg is not null)
            SetValue(def.KwArg, PyDictObject.CreateDict(arguments.ExtraKwargs.Select(static kvp => KeyValuePair.Create((PyObject)PyStrObject.FromString(kvp.Key), kvp.Value))));
    }

    private bool TryGetValueFromBuiltins(string identifier, [NotNullWhen(true)] out PyObject? value)
    {
        value = null;
        if (!Globals.TryGetValue(PySpecialNames.Builtins, out var builtins))
            return false;

        if (!builtins.PyAttributes.TryGetValue(identifier, out value))
            return false;

        return true;
    }

    internal PyObject GetValue(string identifier)
    {
        if (_variables is not null)
        {
            return GetVariableValue(identifier, _variables[identifier]);
        }
        else
        {
            return GetVariableValue(identifier, PyVariableType.Global);
        }
    }

    internal void SetValue(string identifier, PyObject value)
    {
        if (_variables is not null)
        {
            SetVariableValue(identifier, _variables[identifier], value);
        }
        else
        {
            SetVariableValue(identifier, PyVariableType.Global, value);
        }
        return;
    }

    internal PyObject GetVariableValue(string name, PyVariableType variableType)
    {
        if (variableType is PyVariableType.Local or PyVariableType.Parameter)
        {
            if (_locals is not null && _locals.TryGetValue(name, out var value))
            {
                if (value is null)
                {
                    throw PyCallContext.ThrowException(PyStandardExceptionTypes.UnboundLocalError, $"cannot access local variable '{name}' where it is not associated with a value");
                }

                return value;
            }

            if (_closure is not null && _closure.TryGetValue(name, out var cell))
            {
                if (cell.Value is null)
                {
                    throw PyCallContext.ThrowException(PyStandardExceptionTypes.UnboundLocalError, $"cannot access local variable '{name}' where it is not associated with a value");
                }

                return cell.Value;
            }

            throw PyCallContext.ThrowException(PyStandardExceptionTypes.NameError, $"name '{name}' is not defined");
        }
        else if (variableType is PyVariableType.Global)
        {
            if (Globals.TryGetValue(name, out var value))
                return value;

            if (TryGetValueFromBuiltins(name, out value))
                return value;

            throw PyCallContext.ThrowException(PyStandardExceptionTypes.NameError, $"name '{name}' is not defined");
        }
        else if (variableType is PyVariableType.Closure)
        {
            //Debug.Assert(_capturedFrames is not null);
            //return _capturedFrames[name].GetVariableValue(name, PyVariableType.Local);

            var value = Closures[name].Value;
            if (value is not null)
                return value;

            throw PyCallContext.ThrowException(PyStandardExceptionTypes.NameError, $"cannot access free variable '{name}' where it is not associated with a value in enclosing scope");
        }

        throw new NotImplementedException();
    }
    internal void SetVariableValue(string name, PyVariableType variableType, PyObject value)
    {
        if (variableType is PyVariableType.Local or PyVariableType.Parameter)
        {
            if (Closures.TryGetValue(name, out PyCellObject? cell))
                cell.Value = value;
            else
                Locals[name] = value;
        }
        else if (variableType is PyVariableType.Global)
        {
            Globals[name] = value;
        }
        else if (variableType is PyVariableType.Closure)
        {
            //Debug.Assert(_capturedFrames is not null);
            //_capturedFrames[name].SetVariableValue(name, PyVariableType.Local, value);

            Closures[name].Value = value;
        }
        else
        {
            throw new NotImplementedException();
        }
    }

    public void FromModuleImportAll(PyModuleObject module)
    {
        foreach (var attr in module.PyAttributes)
        {
            SetValue(attr.Key, attr.Value);
        }
    }

    public void Import(PyCallContext context, string name, string? alias = null)
    {
        if (!context.PyEnvironment.TryLoadModule(context, name, out var module))
        {
            context.RaiseException(PyStandardExceptionTypes.ModuleNotFoundError, $"No module named '{name}'");
            throw new PyRuntimeException(context.CurrentException);
        }

        SetValue(alias ?? name, module);
    }

    public void RemoveValue(string identifier)
    {
        ArgumentNullException.ThrowIfNull(identifier);

        Locals.Remove(identifier);
    }

}
