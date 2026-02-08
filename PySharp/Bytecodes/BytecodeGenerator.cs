using PySharp.CodeAnalysis;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PySharp.Bytecodes;

internal sealed class BytecodeGenerator
{
    private readonly List<Instruction> _instructions = [];
    private readonly List<int> _labelOffsets = [];
    internal readonly OrderedDictionary<int, CodeMetaInfo?> _infos = [];

    private Stack<CodeMetaInfo?> MetaInfoStack { get; } = [];
    internal List<Instruction> Instructions => _instructions;
    internal List<int> LabelOffsets => _labelOffsets;

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

    internal void FillWithLabelOffsets()
    {
        var instructions = CollectionsMarshal.AsSpan(_instructions);
        foreach (ref Instruction instruction in instructions)
        {
            if (instruction.Operand is Label label)
            {
                instruction = new Instruction(instruction.OpCode, LabelToOffset(label));
            }
            else if (instruction.Operand is ValueTuple<Label, Label> tuple)
            {
                var item1 = tuple.Item1.Id > 0 ? LabelToOffset(tuple.Item1) : -1;
                var item2 = LabelToOffset(tuple.Item2);

                instruction = new Instruction(instruction.OpCode, (item1, item2));
            }
        }

        int LabelToOffset(Label label)
        {
            Debug.Assert(label.Id is not 0);
            Debug.Assert(_labelOffsets[label.Id - 1] >= 0);

            return _labelOffsets[label.Id - 1];
        }
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
        var label = new Label(_labelOffsets.Count + 1);
        _labelOffsets.Add(-1);
        return label;
    }

    public void MarkLabel(Label label)
    {
        Debug.Assert(label.Id is not 0);
        Debug.Assert(_labelOffsets[label.Id - 1] < 0);

        _labelOffsets[label.Id - 1] = Instructions.Count;
    }
}
