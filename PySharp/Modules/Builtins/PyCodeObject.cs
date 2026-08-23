using PySharp.Compilation.AstNodes;
using PySharp.Compilation.Bytecodes;
using PySharp.Runtime;
using PySharp.Runtime.Calls;
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
    AsyncGenerator = 1 << 7,
}

public sealed class PyCodeObject : PyObjectManagedDict
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
    public string Filename { get; }
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

    internal PyCodeObject(string filename, CallableVariableScope scope, Bytecode bytecode)
    {
        Debug.Assert(scope.Name is not null);
        Debug.Assert(scope.QualName is not null);

        LocalsTable = scope.LocalsTable;
        Bytecode = bytecode;

        Flags = CodeObjectFlags.Function;
        if (scope is AsyncFunctionVariableScope asyncFnScope)
        {
            if (asyncFnScope.IsAsyncGenerator)
                Flags |= CodeObjectFlags.AsyncGenerator;
            else
                Flags |= CodeObjectFlags.Coroutine;
        }
        else if (scope.IsGenerator)
        {
            Flags |= CodeObjectFlags.Generator;
        }
        if (scope is GeneratorExpVariableScope { IsAsyncGenerator: true })
            Flags |= CodeObjectFlags.AsyncGenerator;

        Name = scope.Name;
        Filename = filename;
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
    }

    internal PyCodeObject(string filename, ClassVariableScope scope, Bytecode bytecode)
    {
        Debug.Assert(scope.QualName is not null);

        LocalsTable = scope.FreeVars.Length is 0 ?
            FrozenDictionary<string, int>.Empty :
            scope.FreeVars.Index().ToFrozenDictionary(static tuple => tuple.Item, static tuple => tuple.Index);
        Bytecode = bytecode;
        Flags = CodeObjectFlags.Class;

        Name = scope.Name;
        Filename = filename;
        QualName = scope.QualName;
        NLocals = 0;
        VarNames = [];
        CellVars = [];
        FreeVars = scope.FreeVars;
    }

    internal PyCodeObject(string filename, GenericParamVariableScope scope, Bytecode bytecode)
    {
        Debug.Assert(scope.QualName is not null);

        LocalsTable = scope.LocalsTable;
        Bytecode = bytecode;
        Flags = CodeObjectFlags.Function;

        Name = scope.Name;
        Filename = filename;
        QualName = scope.QualName;
        VarNames = scope.VarNames;
        NLocals = VarNames.Length;
        CellVars = scope.CellVars;
        FreeVars = scope.FreeVars;
        ArgCount = scope.ArgCount;
        PosOnlyArgCount = 0;
        KwOnlyArgCount = 0;
        DefaultsCount = 0;
        KwDefaultsCount = 0;
    }

    internal PyCodeObject(string name, string filename, Bytecode bytecode, CodeObjectFlags flags)
    {
        LocalsTable = FrozenDictionary<string, int>.Empty;
        Bytecode = bytecode;
        Flags = flags;

        Name = name;
        Filename = filename;
        QualName = name;
        NLocals = 0;
        VarNames = [];
        CellVars = [];
        FreeVars = [];
    }

    internal PyStrObject CoName => field ??= PyStrObject.FromString(Name);
    internal PyStrObject CoFilename => field ??= PyStrObject.FromString(Filename);
    internal PyStrObject CoQualName => field ??= PyStrObject.FromString(QualName);
    internal PyIntObject CoArgCount => field ??= PyIntObject.FromInteger(ArgCount);
    internal PyIntObject CoPosOnlyArgCount => field ??= PyIntObject.FromInteger(PosOnlyArgCount);
    internal PyIntObject CoKwOnlyArgCount => field ??= PyIntObject.FromInteger(KwOnlyArgCount);
    internal PyIntObject CoNLocals => field ??= PyIntObject.FromInteger(NLocals);
    internal PyTupleObject CoVarnames => field ??= PyTupleObject.CreateTuple(VarNames.Select(PyStrObject.FromString));
    internal PyTupleObject CoCellvars => field ??= PyTupleObject.CreateTuple(CellVars.Select(PyStrObject.FromString));
    internal PyTupleObject CoFreevars => field ??= PyTupleObject.CreateTuple(FreeVars.Select(PyStrObject.FromString));
    internal PyIntObject CoStacksize => field ??= PyIntObject.FromInteger(Bytecode.StackSize);
}

[PyType("code")]
public sealed partial class PyCodeObjectType : PyTypeObject<PyCodeObject>
{
    [PyProperty("co_name")]
    private static PyResult Get_CoName(PyCallContext context, PyCodeObject self) => self.CoName;

    [PyProperty("co_filename")]
    private static PyResult Get_CoFilename(PyCallContext context, PyCodeObject self) => self.CoFilename;

    [PyProperty("co_qualname")]
    private static PyResult Get_CoQualName(PyCallContext context, PyCodeObject self) => self.CoQualName;

    [PyProperty("co_argcount")]
    private static PyResult Get_CoArgCount(PyCallContext context, PyCodeObject self) => self.CoArgCount;

    [PyProperty("co_posonlyargcount")]
    private static PyResult Get_CoPosOnlyArgCount(PyCallContext context, PyCodeObject self) => self.CoPosOnlyArgCount;

    [PyProperty("co_kwonlyargcount")]
    private static PyResult Get_CoKwOnlyArgCount(PyCallContext context, PyCodeObject self) => self.CoKwOnlyArgCount;

    [PyProperty("co_nlocals")]
    private static PyResult Get_CoNLocals(PyCallContext context, PyCodeObject self) => self.CoNLocals;

    [PyProperty("co_varnames")]
    private static PyResult Get_CoVarnames(PyCallContext context, PyCodeObject self) => self.CoVarnames;

    [PyProperty("co_cellvars")]
    private static PyResult Get_CoCellvars(PyCallContext context, PyCodeObject self) => self.CoCellvars;

    [PyProperty("co_freevars")]
    private static PyResult Get_CoFreevars(PyCallContext context, PyCodeObject self) => self.CoFreevars;

    [PyProperty("co_stacksize")]
    private static PyResult Get_CoStacksize(PyCallContext context, PyCodeObject self) => self.CoStacksize;
}
