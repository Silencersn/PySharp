using PySharp.PyModules.Builtins;
using System;
using System.Collections.Generic;
using System.Text;

namespace PySharp.Bytecodes;

internal readonly record struct Instruction
{
    public readonly OpCode OpCode;
    public readonly int Arg;
    public readonly object? Operand;

    public string StringOperand => GetOperand<string>();
    public PyObject PyObjectOperand => GetOperand<PyObject>();
    public Label LabelOperand => GetOperand<Label>();

    public Instruction(OpCode opCode)
    {
        OpCode = opCode;
        Arg = 0;
        Operand = null;
    }
    public Instruction(OpCode opCode, int arg)
    {
        OpCode = opCode;
        Arg = arg;
        Operand = null;
    }
    public Instruction(OpCode opCode, object? operand)
    {
        OpCode = opCode;
        Arg = 0;
        Operand = operand;
    }

    internal T GetOperand<T>()
    {
        if (Operand is not T objOfT)
            throw new InvalidOperationException();

        return objOfT;
    }

    public override string ToString()
    {
        return $"{{opcode={OpCode},argval={Operand ?? Arg}}}";
    }
}

internal record class Label
{
    public readonly int Id;
    public int Offset;

    public Label(int id, int offset)
    {
        Id = id;
        Offset = offset;
    }
}