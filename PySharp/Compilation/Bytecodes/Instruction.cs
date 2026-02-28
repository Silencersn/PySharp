using System.Diagnostics;

namespace PySharp.Compilation.Bytecodes;

[DebuggerDisplay("opcode={OpCode}, arg={Arg}")]
internal readonly struct Instruction
{
    public readonly OpCode OpCode;
    public readonly byte Arg;

    public Instruction(OpCode opCode)
    {
        OpCode = opCode;
        Arg = 0;
    }
    public Instruction(OpCode opCode, byte arg)
    {
        OpCode = opCode;
        Arg = arg;
    }
}
