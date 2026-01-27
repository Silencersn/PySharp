using PySharp.AstNodes;
using System;
using System.Collections.Generic;
using System.Text;

namespace PySharp.Bytecodes;

internal sealed class Bytecode
{
    private readonly SemanticModel _model;
    private readonly List<Instruction> _instructions;
    private readonly List<Label> _labels;

    public Bytecode(SemanticModel model, BytecodeGenerator generator)
    {
        _model = model;
        _instructions = [.. generator.Instructions];
        _labels = [.. generator.Labels];
    }

    internal SemanticModel Model => _model;
    internal List<Instruction> Instructions => _instructions;
    internal List<Label> Labels => _labels;
}
