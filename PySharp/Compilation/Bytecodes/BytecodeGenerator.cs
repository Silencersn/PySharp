using PySharp.Compilation.CodeAnalysis;
using PySharp.Modules.Builtins;
using PySharp.Runtime.Comparison;
using System.Collections.Immutable;
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
    private readonly Stack<CodeMetaInfo?> _metaInfoStack = [];
    private Instruction _lastInstruction;

    internal override Bytecode ToBytecode()
    {
        Complete();
        return new Bytecode(_instructions.ToImmutableArray(), [.. _infos.Reverse()], [.. _consts.Keys], [.. _names.Keys]);
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
        for (int i = 0; i < _instructions.Count; i++)
        {
            var instruction = _instructions[i];

            if (instruction.Arg >= 0)
                continue;

            _instructions[i] = new Instruction(instruction.OpCode, LabelToOffset(-instruction.Arg));
        }

        int LabelToOffset(int labelId)
        {
            Debug.Assert(labelId > 0);
            Debug.Assert(_labelOffsets[labelId - 1] >= 0);

            return _labelOffsets[labelId - 1];
        }
    }

    private static bool IsStackTopBool(Instruction instruction)
    {
        return instruction.OpCode switch
        {
            OpCode.ToBool or
            OpCode.IsOp or
            OpCode.UnaryNot => true,

            _ => false
        };
    }

    private void InternalEmit(Instruction instruction)
    {
        _instructions.Add(instruction);
        _lastInstruction = instruction;
    }

    public override void Emit(OpCode opCode)
    {
        if (opCode is OpCode.ToBool)
        {
            if (IsStackTopBool(_lastInstruction))
                return;
        }

        InternalEmit(new Instruction(opCode));
    }

    public override void Emit(OpCode opCode, int arg)
    {
        Debug.Assert(arg >= 0, "Negative arg is used for label.");

        InternalEmit(new Instruction(opCode, arg));
    }

    public override void Emit(OpCode opCode, Label label)
    {
        Debug.Assert(label.Id > 0);

        if (opCode is OpCode.PopJumpIfFalse or OpCode.PopJumpIfTrue)
        {
            if (_lastInstruction.OpCode is OpCode.UnaryNot)
            {
                _instructions.RemoveAt(_instructions.Count - 1);
                opCode = opCode is OpCode.PopJumpIfFalse ? OpCode.PopJumpIfTrue : OpCode.PopJumpIfFalse;
            }
        }

        InternalEmit(new Instruction(opCode, -label.Id));
    }

    public override void Emit(OpCode opCode, PyObject pyObject)
    {
        if (!_consts.TryGetValue(pyObject, out var index))
            _consts[pyObject] = index = _consts.Count;
        InternalEmit(new Instruction(opCode, index));
    }

    public override void Emit(OpCode opCode, string name)
    {
        if (!_names.TryGetValue(name, out var index))
            _names[name] = index = _names.Count;
        InternalEmit(new Instruction(opCode, index));
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