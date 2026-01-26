using System;
using System.Collections.Generic;
using System.Text;

namespace PySharp.Bytecodes;

internal sealed class BytecodeGenerator
{
    private readonly List<Instruction> _instructions = [];

    internal List<Instruction> Instructions => _instructions;

    public void Emit(OpCode opCode)
    {
        _instructions.Add(new Instruction(opCode));
    }

    public void Emit(OpCode opCode, int arg)
    {
        _instructions.Add(new Instruction(opCode, arg));
    }

    public void Emit(OpCode opCode, object? operand)
    {
        _instructions.Add(new Instruction(opCode, operand));
    }
}
