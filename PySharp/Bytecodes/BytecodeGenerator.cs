using PySharp.CodeAnalysis;
using PySharp.PyModules.Builtins;
using PySharp.PyRuntime.Comparison;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PySharp.Bytecodes;

internal sealed class BytecodeGenerator
{
    private readonly List<Instruction> _instructions = [];
    private readonly List<int> _labelOffsets = [];
    internal readonly OrderedDictionary<int, CodeMetaInfo?> _infos = [];
    internal ImmutableArray<PyObject> Consts = [];
    internal ImmutableArray<string> Names = [];

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

    internal void Complete()
    {
        var instructions = CollectionsMarshal.AsSpan(_instructions);

        OrderedDictionary<PyObject, int> consts = new(PyObjectConstEqualityComparer.Shared);
        OrderedDictionary<string, int> names = new(StringComparer.Ordinal);

        foreach (ref Instruction instruction in instructions)
        {
            if (instruction.Arg < 0)
            {
                instruction = new Instruction(instruction.OpCode, LabelToOffset(-instruction.Arg));
            }
            else if (instruction.Operand is PyObject constObj)
            {
                if (!consts.TryGetValue(constObj, out var index))
                    consts[constObj] = index = consts.Count;
                instruction = new Instruction(instruction.OpCode, index);
            }
            else if (instruction.Operand is string name)
            {
                if (!names.TryGetValue(name, out var index))
                    names[name] = index = names.Count;
                instruction = new Instruction(instruction.OpCode, index);
            }
            else
            {
                Debug.Assert(instruction.Operand is null);
            }
        }

        Consts = [.. consts.Keys];
        Names = [.. names.Keys];

        int LabelToOffset(int labelId)
        {
            Debug.Assert(labelId > 0);
            Debug.Assert(_labelOffsets[labelId - 1] >= 0);

            return _labelOffsets[labelId - 1];
        }
    }

    public void Emit(OpCode opCode)
    {
        _instructions.Add(new Instruction(opCode));
    }

    public void Emit(OpCode opCode, int arg)
    {
        Debug.Assert(arg >= 0, "Negative arg is used for label.");

        _instructions.Add(new Instruction(opCode, arg));
    }

    public void Emit(OpCode opCode, Label label)
    {
        Debug.Assert(label.Id > 0);

        _instructions.Add(new Instruction(opCode, -label.Id));
    }

    public void Emit(OpCode opCode, PyObject pyObject)
    {
        _instructions.Add(new Instruction(opCode, pyObject));
    }

    public void Emit(OpCode opCode, string name)
    {
        _instructions.Add(new Instruction(opCode, name));
    }

    public Label DefineLabel()
    {
        var label = new Label(_labelOffsets.Count + 1);
        _labelOffsets.Add(-1);
        return label;
    }

    public void MarkLabel(Label label)
    {
        Debug.Assert(label.Id > 0);
        Debug.Assert(_labelOffsets[label.Id - 1] < 0);

        _labelOffsets[label.Id - 1] = Instructions.Count;
    }
}
