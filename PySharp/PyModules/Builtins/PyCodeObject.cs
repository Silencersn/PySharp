using PySharp.AstNodes;
using System.Collections.Immutable;
using System.Diagnostics;

namespace PySharp.PyModules.Builtins;

public sealed class PyCodeObject : PyObject
{
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

    internal PyCodeObject(CallableVariableScope scope)
    {
        Debug.Assert(scope.Name is not null);
        Debug.Assert(scope.QualName is not null);

        Name = scope.Name;
        QualName = scope.QualName;
        var arg = scope.ArgumentsNode;
        ArgCount = arg.PosonlyArgs.Count + arg.Args.Count;
        PosOnlyArgCount = arg.PosonlyArgs.Count;
        KwOnlyArgCount = arg.KwonlyArgs.Count;
        NLocals = scope.VarNames.Length;
        VarNames = [.. scope.VarNames];
        CellVars = [.. scope.CellVars];
        FreeVars = [.. scope.FreeVars];

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
}

public sealed class PyCodeObjectType : PyTypeObject<PyCodeObjectType, PyCodeObject>
{
    public override string Module => "builtins";
    public override string Name => "code";
}
