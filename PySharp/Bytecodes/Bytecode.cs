using System.Collections.Immutable;

namespace PySharp.Bytecodes;

internal sealed class Bytecode
{
    private readonly ImmutableArray<Instruction> _instructions;
    private readonly ImmutableArray<Label> _labels;

    public Bytecode(BytecodeGenerator generator)
    {
        _instructions = [.. generator.Instructions];
        _labels = [.. generator.Labels];
    }

    internal ImmutableArray<Instruction> Instructions => _instructions;
    internal ImmutableArray<Label> Labels => _labels;
}
