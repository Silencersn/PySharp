namespace PySharp.Bytecodes;

internal sealed class BytecodeGenerator
{
    private readonly List<Instruction> _instructions = [];
    private readonly List<Label> _labels = [];

    internal List<Instruction> Instructions => _instructions;
    internal List<Label> Labels => _labels;

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

    public Label DefineLabel()
    {
        var label = new Label(_labels.Count + 1, offset: -1);
        _labels.Add(label);
        return label;
    }

    public void MarkLabel(Label label)
    {
        if (label.Offset >= 0)
            throw new InvalidOperationException();
        label.Offset = Instructions.Count;
    }
}
