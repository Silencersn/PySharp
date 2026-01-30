using PySharp.AstNodes;
using System;
using System.Collections.Generic;
using System.Text;

namespace PySharp.Bytecodes;

internal sealed class Bytecode
{
    private readonly List<Instruction> _instructions;
    private readonly List<Label> _labels;

    public Bytecode(BytecodeGenerator generator)
    {
        _instructions = [.. generator.Instructions];
        _labels = [.. generator.Labels];
    }

    internal List<Instruction> Instructions => _instructions;
    internal List<Label> Labels => _labels;
}
