using PySharp.Compilation.AstNodes;
using PySharp.Compilation.Bytecodes;
using PySharp.Runtime.PyAttributes;
using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Diagnostics;

namespace PySharp.Modules.Builtins;

[Flags]
public enum CodeObjectFlags
{
    None = 0,

    VarArgs = 1,
    VarKeywords = 1 << 1,
    Function = 1 << 2,
    Generator = 1 << 3,
    Class = 1 << 4,
    Module = 1 << 5,
    Coroutine = 1 << 6,
}

public sealed class PyCodeObject : PyObject
{
    internal FrozenDictionary<string, int> LocalsTable { get; }

    internal string? VarArg => Flags.HasFlag(CodeObjectFlags.VarArgs) ?
        VarNames[ArgCount + KwOnlyArgCount] : null;
    internal string? KwArg => Flags.HasFlag(CodeObjectFlags.VarKeywords) ?
        VarNames[ArgCount + KwOnlyArgCount + (VarArg is null ? 0 : 1)] : null;

    internal int DefaultsCount { get; }
    internal int KwDefaultsCount { get; }


    public Bytecode Bytecode { get; }
    public CodeObjectFlags Flags { get; }
    public string Name { get; }
    public string QualName { get; }
    public int ArgCount { get; }
    public int PosOnlyArgCount { get; }
    public int KwOnlyArgCount { get; }
    public int NLocals { get; }
    public ImmutableArray<string> VarNames { get; }
    public ImmutableArray<string> CellVars { get; }
    public ImmutableArray<string> FreeVars { get; }

    internal override bool IsImmutable => true;

    public override PyTypeObject DefaultPyType => PyCodeObjectType.Shared;

    internal PyCodeObject(CallableVariableScope scope, Bytecode bytecode)
    {
        Debug.Assert(scope.Name is not null);
        Debug.Assert(scope.QualName is not null);

        LocalsTable = scope.LocalsTable;
        Bytecode = bytecode;

        Flags = CodeObjectFlags.Function;
        if (scope.IsGenerator)
            Flags |= CodeObjectFlags.Generator;
        if (scope is AsyncFunctionVariableScope)
            Flags |= CodeObjectFlags.Coroutine;

        Name = scope.Name;
        QualName = scope.QualName;

        var arg = scope.ArgumentsNode;
        if (arg.VarArg is not null)
            Flags |= CodeObjectFlags.VarArgs;
        if (arg.KwArg is not null)
            Flags |= CodeObjectFlags.VarKeywords;
        DefaultsCount = arg.Defaults.Length;
        KwDefaultsCount = arg.KwDefaults.Length;

        ArgCount = arg.PosonlyArgs.Length + arg.Args.Length;
        PosOnlyArgCount = arg.PosonlyArgs.Length;
        KwOnlyArgCount = arg.KwonlyArgs.Length;
        NLocals = scope.VarNames.Length;
        VarNames = scope.VarNames;
        CellVars = scope.CellVars;
        FreeVars = scope.FreeVars;

        PyAttributes.Add("co_name", PyStrObject.FromString(Name));
        PyAttributes.Add("co_qualname", PyStrObject.FromString(QualName));
        PyAttributes.Add("co_argcount", PyIntObject.FromInteger(ArgCount));
        PyAttributes.Add("co_posonlyargcount", PyIntObject.FromInteger(PosOnlyArgCount));
        PyAttributes.Add("co_kwonlyargcount", PyIntObject.FromInteger(KwOnlyArgCount));
        PyAttributes.Add("co_nlocals", PyIntObject.FromInteger(NLocals));
        PyAttributes.Add("co_varnames", PyTupleObject.CreateTuple(VarNames.Select(PyStrObject.FromString)));
        PyAttributes.Add("co_cellvars", PyTupleObject.CreateTuple(CellVars.Select(PyStrObject.FromString)));
        PyAttributes.Add("co_freevars", PyTupleObject.CreateTuple(FreeVars.Select(PyStrObject.FromString)));
        PyAttributes.Add("co_stacksize", PyIntObject.FromInteger(bytecode.StackSize));
    }

    internal PyCodeObject(ClassVariableScope scope, Bytecode bytecode)
    {
        Debug.Assert(scope.Name is not null);
        Debug.Assert(scope.QualName is not null);

        LocalsTable = scope.FreeVars.Length is 0 ?
            FrozenDictionary<string, int>.Empty :
            scope.FreeVars.Index().ToFrozenDictionary(static tuple => tuple.Item, static tuple => tuple.Index);
        Bytecode = bytecode;
        Flags = CodeObjectFlags.Class;

        Name = scope.Name;
        QualName = scope.QualName;
        NLocals = 0;
        VarNames = [];
        CellVars = [];
        FreeVars = scope.FreeVars;

        PyAttributes.Add("co_name", PyStrObject.FromString(Name));
        PyAttributes.Add("co_qualname", PyStrObject.FromString(QualName));
        PyAttributes.Add("co_argcount", PyIntObject.Zero);
        PyAttributes.Add("co_posonlyargcount", PyIntObject.Zero);
        PyAttributes.Add("co_kwonlyargcount", PyIntObject.Zero);
        PyAttributes.Add("co_nlocals", PyIntObject.Zero);
        PyAttributes.Add("co_varnames", PyTupleObject.Empty);
        PyAttributes.Add("co_cellvars", PyTupleObject.Empty);
        PyAttributes.Add("co_freevars", PyTupleObject.CreateTuple(FreeVars.Select(PyStrObject.FromString)));
        PyAttributes.Add("co_stacksize", PyIntObject.FromInteger(bytecode.StackSize));
    }

    internal PyCodeObject(string name, Bytecode bytecode, CodeObjectFlags flags)
    {
        LocalsTable = FrozenDictionary<string, int>.Empty;
        Bytecode = bytecode;
        Flags = flags;

        Name = name;
        QualName = name;
        NLocals = 0;
        VarNames = [];
        CellVars = [];
        FreeVars = [];

        PyAttributes.Add("co_name", PyStrObject.FromString(Name));
        PyAttributes.Add("co_qualname", PyStrObject.FromString(QualName));
        PyAttributes.Add("co_argcount", PyIntObject.Zero);
        PyAttributes.Add("co_posonlyargcount", PyIntObject.Zero);
        PyAttributes.Add("co_kwonlyargcount", PyIntObject.Zero);
        PyAttributes.Add("co_nlocals", PyIntObject.Zero);
        PyAttributes.Add("co_varnames", PyTupleObject.Empty);
        PyAttributes.Add("co_cellvars", PyTupleObject.Empty);
        PyAttributes.Add("co_freevars", PyTupleObject.Empty);
        PyAttributes.Add("co_stacksize", PyIntObject.FromInteger(bytecode.StackSize));
    }
}

[PyType("code")]
public sealed partial class PyCodeObjectType : PyTypeObject<PyCodeObjectType, PyCodeObject>;
