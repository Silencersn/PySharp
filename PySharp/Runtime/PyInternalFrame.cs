using PySharp.Compilation.AstNodes;
using PySharp.Compilation.Bytecodes;
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
    internal PyVariables Variables;
    internal PyObject? Caller;
    internal PyCodeObject? CodeObject;
    internal Stack<PyExceptionObject>? _exceptions;
    internal FrameType FrameType;
    internal int InstructionIndex;

    internal Stack<PyExceptionObject> Exceptions => _exceptions ??= [];
    internal PyExceptionObject CurrentException => Exceptions.Peek();

    private PyInternalFrame(bool isRoot)
    {
        Variables = PyVariables.CreateGlobal();
        CallerName = "<module>";
        Caller = null;
        FrameType = isRoot ? FrameType.MainRoot : FrameType.Module;
    }
    private PyInternalFrame(PyVariables variables)
    {
        Variables = variables.CreatePlaceholder();
        CallerName = $"<thread-{Environment.CurrentManagedThreadId}>";
        Caller = null;
        FrameType = FrameType.ThreadRoot;
    }
    private PyInternalFrame(
        PyVariables variables,
        string callerName,
        PyObject? caller,
        FrameType frameType)
    {
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
        var frame = new PyInternalFrame(isRoot);
        var builtins = context.PyEnvironment.LoadBuiltinModule(context, "builtins");
        frame.Variables.Globals.Dict[PySpecialNames.Builtins] = builtins;
        frame.Variables.Globals.Dict[PySpecialNames.Name] = PyStrObject.FromString(moduleQualifiedName);

        // TODO: add flag to control whether adding site
        _ = context.PyEnvironment.LoadBuiltinModule(context, "site");

        return frame;
    }
    internal static PyInternalFrame CreateFuncCallFrame(PyCallContext context, string callerName,
        PyObject caller, FrameType frameType,
        PyGlobals globals, PyCodeObject code)
    {
        Debug.Assert(frameType is FrameType.Function);

        var variables = code.Flags is CodeObjectFlags.Function ?
            PyVariables.CreateUsingStackMemoryAllocator(globals, context, code) :
            PyVariables.CreateUsingArrayPool(globals, code.LocalsTable);

        return new PyInternalFrame(
            variables,
            callerName,
            caller,
            frameType)
        { CodeObject = code };
    }

    internal readonly PyInternalFrame CreateClassBuildFrame(PyTypeObject buildingClass, PyCodeObject code)
    {
        var variables = Variables.CreateForBuildingClass(code);

        return new PyInternalFrame(
            variables,
            buildingClass.Name,
            buildingClass,
            FrameType.Class)
        { CodeObject = code };
    }

    internal readonly PyInternalFrame CreateThreadRootFrame()
    {
        return new PyInternalFrame(Variables);
    }

    internal readonly PyInternalFrame CreateExecEvalFrame(FrameType frameType, PyDictObject? globals, PyDictObject? locals, PyCodeObject? code = null, PyTupleObject? closure = null)
    {
        Debug.Assert(frameType is FrameType.Exec or FrameType.Eval);

        PyGlobals pyGlobals = globals is null ?
            Variables.Globals :
            new(new StringKeyDict(globals));

        IDictionary<string, PyObject?>? localsDictionary = (locals is null ?
            null :
            new StringKeyDict(locals))!;

        if (closure is not null)
        {
            Debug.Assert(code is not null);
            var localsTable = code.FreeVars.Index().ToFrozenDictionary(static tuple => tuple.Item, static tuple => tuple.Index);
            localsDictionary = new PyVariables.LocalDictionary(localsTable, closure.InternalArray);
        }

        var variables = PyVariables.CreateExecEval(pyGlobals, localsDictionary);
        return new PyInternalFrame(variables, CallerName, Caller, frameType);
    }

    internal readonly PyInternalFrame CreateInlineFrame(FrameType frameType)
    {
        Debug.Assert(frameType is FrameType.Comprehension);

        var variables = Variables.CreateInline();
        var inlineFrame = new PyInternalFrame(variables, CallerName, Caller, frameType)
        {
            CodeObject = CodeObject
        };
        return inlineFrame;
    }

    internal readonly void InitArgs(PyArgsDef def, PyCodeObject code, PyArguments arguments, ReadOnlySpan<PyCellObject> closure)
    {
        var localsSpan = Variables.LocalsSpan;
        arguments.Args.CopyTo(localsSpan!);
        ReadOnlySpan<PyObject>.CastUp(closure).CopyTo(localsSpan[^closure.Length..]!);

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
}
