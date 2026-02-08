using PySharp.CodeAnalysis;

namespace PySharp.Bytecodes;

internal sealed class BytecodeGenerator
{
    private readonly List<Instruction> _instructions = [];
    private readonly List<Label> _labels = [];
    internal readonly OrderedDictionary<int, CodeMetaInfo?> _infos = [];

    private Stack<CodeMetaInfo?> MetaInfoStack { get; } = [];
    internal List<Instruction> Instructions => _instructions;
    internal List<Label> Labels => _labels;

    internal void PushMetaInfo(CodeMetaInfo? info)
    {
        MetaInfoStack.Push(info);
        _infos[_instructions.Count] = info;
    }

    internal void PopMetaInfo()
    {
        MetaInfoStack.Pop();
        _infos[_instructions.Count] = MetaInfoStack.TryPeek(out var info) ? info : null;
    }

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
