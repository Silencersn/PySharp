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
    private readonly OrderedDictionary<int, CodeMetaInfo?> _infos = [];
    private readonly OrderedDictionary<PyObject, int> _consts = new(PyObjectConstEqualityComparer.Shared);
    private readonly OrderedDictionary<string, int> _names = new(StringComparer.Ordinal);

    private Stack<CodeMetaInfo?> MetaInfoStack { get; } = [];
    internal List<Instruction> Instructions => _instructions;
    internal List<int> LabelOffsets => _labelOffsets;
    internal OrderedDictionary<int, CodeMetaInfo?> Infos => _infos;
    internal OrderedDictionary<PyObject, int> Consts => _consts;
    internal OrderedDictionary<string, int> Names => _names;

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

        foreach (ref Instruction instruction in instructions)
        {
            if (instruction.Arg >= 0)
                continue;

            instruction = new Instruction(instruction.OpCode, LabelToOffset(-instruction.Arg));
        }

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
        if (!_consts.TryGetValue(pyObject, out var index))
            _consts[pyObject] = index = _consts.Count;
        _instructions.Add(new Instruction(opCode, index));
    }

    public void Emit(OpCode opCode, string name)
    {
        if (!_names.TryGetValue(name, out var index))
            _names[name] = index = _names.Count;
        _instructions.Add(new Instruction(opCode, index));
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
