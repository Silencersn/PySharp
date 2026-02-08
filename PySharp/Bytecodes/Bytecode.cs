using PySharp.CodeAnalysis;
using System.Collections.Immutable;

namespace PySharp.Bytecodes;

internal sealed class Bytecode
{
    private readonly ImmutableArray<Instruction> _instructions;
    private readonly ImmutableArray<Label> _labels;
    private readonly ImmutableArray<KeyValuePair<int, CodeMetaInfo?>> _infos = [];

    public Bytecode(BytecodeGenerator generator)
    {
        _instructions = [.. generator.Instructions];
        _labels = [.. generator.Labels];
        _infos = [.. generator._infos.Reverse()];
    }

    internal ImmutableArray<Instruction> Instructions => _instructions;
    internal ImmutableArray<Label> Labels => _labels;
    internal ImmutableArray<KeyValuePair<int, CodeMetaInfo?>> MetaInfos => _infos;
}
