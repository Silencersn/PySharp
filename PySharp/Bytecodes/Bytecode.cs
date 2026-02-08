using PySharp.CodeAnalysis;
using System.Collections.Immutable;

namespace PySharp.Bytecodes;

internal sealed class Bytecode
{
    private readonly ImmutableArray<Instruction> _instructions;
    private readonly ImmutableArray<KeyValuePair<int, CodeMetaInfo?>> _infos = [];

    public Bytecode(BytecodeGenerator generator)
    {
        generator.FillWithLabelOffsets();
        _instructions = [.. generator.Instructions];
        _infos = [.. generator._infos.Reverse()];
    }

    internal ImmutableArray<Instruction> Instructions => _instructions;
    internal ImmutableArray<KeyValuePair<int, CodeMetaInfo?>> MetaInfos => _infos;
}
