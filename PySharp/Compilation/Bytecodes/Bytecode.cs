using PySharp.Compilation.CodeAnalysis;
using PySharp.Modules.Builtins;
using System.Collections.Immutable;

namespace PySharp.Compilation.Bytecodes;

internal sealed class Bytecode
{
    private readonly ImmutableArray<Instruction> _instructions;
    private readonly ImmutableArray<KeyValuePair<int, CodeMetaInfo?>> _infos = [];
    private readonly ImmutableArray<PyObject> _consts;
    private readonly ImmutableArray<string> _names;

    public Bytecode(ImmutableArray<Instruction> instructions, ImmutableArray<KeyValuePair<int, CodeMetaInfo?>> infos, ImmutableArray<PyObject> consts, ImmutableArray<string> names)
    {
        _instructions = instructions;
        _infos = infos;
        _consts = consts;
        _names = names;
    }

    internal ImmutableArray<Instruction> Instructions => _instructions;
    internal ImmutableArray<KeyValuePair<int, CodeMetaInfo?>> MetaInfos => _infos;
    internal ImmutableArray<PyObject> Consts => _consts;
    internal ImmutableArray<string> Names => _names;
}
