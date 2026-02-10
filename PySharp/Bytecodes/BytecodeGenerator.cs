using PySharp.CodeAnalysis;
using PySharp.PyModules.Builtins;
using PySharp.PyRuntime.Comparison;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PySharp.Bytecodes;

internal abstract class BytecodeGenerator
{
    internal abstract OrderedDictionary<PyObject, int> Consts { get; }
    internal abstract OrderedDictionary<int, CodeMetaInfo?> Infos { get; }
    internal abstract List<Instruction> Instructions { get; }
    internal abstract List<int> LabelOffsets { get; }
    internal abstract OrderedDictionary<string, int> Names { get; }

    internal abstract void PushMetaInfo(CodeMetaInfo? info);
    internal abstract void PopMetaInfo();
    internal abstract void Complete();
    public abstract void Emit(OpCode opCode);
    public abstract void Emit(OpCode opCode, int arg);
    public abstract void Emit(OpCode opCode, Label label);
    public abstract void Emit(OpCode opCode, PyObject pyObject);
    public abstract void Emit(OpCode opCode, string name);
    public abstract Label DefineLabel();
    public abstract void MarkLabel(Label label);

    public static BytecodeGenerator Create()
    {
        return new DefaultBytecodeGenerator();
    }
}

internal sealed class DefaultBytecodeGenerator : BytecodeGenerator
{
    private readonly List<Instruction> _instructions = [];
    private readonly List<int> _labelOffsets = [];
    private readonly OrderedDictionary<int, CodeMetaInfo?> _infos = [];
    private readonly OrderedDictionary<PyObject, int> _consts = new(PyObjectConstEqualityComparer.Shared);
    private readonly OrderedDictionary<string, int> _names = new(StringComparer.Ordinal);

    private Stack<CodeMetaInfo?> MetaInfoStack { get; } = [];
    internal override List<Instruction> Instructions => _instructions;
    internal override List<int> LabelOffsets => _labelOffsets;
    internal override OrderedDictionary<int, CodeMetaInfo?> Infos => _infos;
    internal override OrderedDictionary<PyObject, int> Consts => _consts;
    internal override OrderedDictionary<string, int> Names => _names;

    internal override void PushMetaInfo(CodeMetaInfo? info)
    {
        MetaInfoStack.Push(info);
        _infos[_instructions.Count] = info;
    }

    internal override void PopMetaInfo()
    {
        MetaInfoStack.Pop();
        _infos[_instructions.Count] = MetaInfoStack.TryPeek(out var info) ? info : null;
    }

    internal override void Complete()
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

    public override void Emit(OpCode opCode)
    {
        _instructions.Add(new Instruction(opCode));
    }

    public override void Emit(OpCode opCode, int arg)
    {
        Debug.Assert(arg >= 0, "Negative arg is used for label.");

        _instructions.Add(new Instruction(opCode, arg));
    }

    public override void Emit(OpCode opCode, Label label)
    {
        Debug.Assert(label.Id > 0);

        _instructions.Add(new Instruction(opCode, -label.Id));
    }

    public override void Emit(OpCode opCode, PyObject pyObject)
    {
        if (!_consts.TryGetValue(pyObject, out var index))
            _consts[pyObject] = index = _consts.Count;
        _instructions.Add(new Instruction(opCode, index));
    }

    public override void Emit(OpCode opCode, string name)
    {
        if (!_names.TryGetValue(name, out var index))
            _names[name] = index = _names.Count;
        _instructions.Add(new Instruction(opCode, index));
    }

    public override Label DefineLabel()
    {
        var label = new Label(_labelOffsets.Count + 1);
        _labelOffsets.Add(-1);
        return label;
    }

    public override void MarkLabel(Label label)
    {
        Debug.Assert(label.Id > 0);
        Debug.Assert(_labelOffsets[label.Id - 1] < 0);

        _labelOffsets[label.Id - 1] = Instructions.Count;
    }
}
