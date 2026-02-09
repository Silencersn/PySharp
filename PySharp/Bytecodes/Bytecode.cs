using PySharp.CodeAnalysis;
using PySharp.PyModules.Builtins;
using System.Collections.Immutable;

namespace PySharp.Bytecodes;

internal sealed class Bytecode
{
    private readonly ImmutableArray<Instruction> _instructions;
    private readonly ImmutableArray<KeyValuePair<int, CodeMetaInfo?>> _infos = [];
    private readonly ImmutableArray<PyObject> _consts;
    private readonly ImmutableArray<string> _names;

    public Bytecode(BytecodeGenerator generator)
    {
        generator.Complete();
        _instructions = [.. generator.Instructions];
        _infos = [.. generator.Infos.Reverse()];
        _consts = [.. generator.Consts.Keys];
        _names = [..generator.Names.Keys];
    }

    internal ImmutableArray<Instruction> Instructions => _instructions;
    internal ImmutableArray<KeyValuePair<int, CodeMetaInfo?>> MetaInfos => _infos;
    internal ImmutableArray<PyObject> Consts => _consts;
    internal ImmutableArray<string> Names => _names;
}
