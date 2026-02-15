using PySharp.Compilation.CodeAnalysis;
using PySharp.Modules.Builtins;
using PySharp.Runtime.Comparison;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PySharp.Compilation.Bytecodes;

internal abstract class BytecodeGenerator
{
    internal abstract Bytecode ToBytecode();
    internal abstract void PushMetaInfo(CodeMetaInfo? info);
    internal abstract void PopMetaInfo();
    public abstract void Emit(OpCode opCode);
    public abstract void Emit(OpCode opCode, int arg);
    public abstract void Emit(OpCode opCode, Label label);
    public abstract void Emit(OpCode opCode, PyObject pyObject);
    public abstract void Emit(OpCode opCode, string name);
    public abstract Label DefineLabel();
    public abstract void MarkLabel(Label label);

    public static BytecodeGenerator Create(bool optimized = true)
    {
        if (optimized)
            return new OptimizedBytecodeGenerator();

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
    private readonly Stack<CodeMetaInfo?> _metaInfoStack = [];

    internal override Bytecode ToBytecode()
    {
        Complete();
        return new Bytecode([.. _instructions], [.. _infos.Reverse()], [.. _consts.Keys], [.. _names.Keys]);
    }
    internal override void PushMetaInfo(CodeMetaInfo? info)
    {
        _metaInfoStack.Push(info);
        _infos[_instructions.Count] = info;
    }

    internal override void PopMetaInfo()
    {
        _metaInfoStack.Pop();
        _infos[_instructions.Count] = _metaInfoStack.TryPeek(out var info) ? info : null;
    }

    private void Complete()
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

        _labelOffsets[label.Id - 1] = _instructions.Count;
    }
}