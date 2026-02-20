using PySharp.Compilation.AstNodes;
using PySharp.Compilation.Bytecodes;
using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Diagnostics;

namespace PySharp.Modules.Builtins;

public sealed class PyCodeObject : PyObject
{
    internal FrozenDictionary<string, int> LocalsTable { get; }
    internal Bytecode Bytecode { get; }

    internal string? VarArg { get; }
    internal string? KwArg { get; }
    internal int DefaultsCount { get; }
    internal int KwDefaultsCount { get; }


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

        Name = scope.Name;
        QualName = scope.QualName;

        var arg = scope.ArgumentsNode;
        VarArg = arg.VarArg?.Arg;
        KwArg = arg.KwArg?.Arg;
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
    }

    internal PyCodeObject(ClassVariableScope scope, Bytecode bytecode)
    {
        Debug.Assert(scope.Name is not null);
        Debug.Assert(scope.QualName is not null);

        LocalsTable = FrozenDictionary<string, int>.Empty;
        Bytecode = bytecode;

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
    }

    internal PyCodeObject(string name, Bytecode bytecode)
    {
        // used by generator expression

        LocalsTable = FrozenDictionary<string, int>.Empty;
        Bytecode = bytecode;

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
    }
}

public sealed class PyCodeObjectType : PyTypeObject<PyCodeObjectType, PyCodeObject>
{
    public override string Module => "builtins";
    public override string Name => "code";
}
