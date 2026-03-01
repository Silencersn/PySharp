using PySharp.Compilation.CodeAnalysis;
using PySharp.Modules.Builtins;
using PySharp.Runtime.Comparison;
using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Diagnostics;

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
    private readonly ImmutableArray<Instruction>.Builder _instructions = ImmutableArray.CreateBuilder<Instruction>();
    private readonly List<int> _labelOffsets = [];
    private readonly LineTableBuilder _lineTableBuilder = new();
    private readonly OrderedDictionary<PyObject, int> _consts = new(PyObjectConstEqualityComparer.Shared);
    private readonly OrderedDictionary<string, int> _names = new(StringComparer.Ordinal);
    private readonly Stack<CodeMetaInfo?> _metaInfoStack = [];
    private Instruction _lastInstruction;

    internal override Bytecode ToBytecode()
    {
        Complete();

        // set Capacity to Count
        // to ensure MoveToImmutable do not throw the exception

        if (_instructions.Count != _instructions.Capacity)
        {
            // uninitialized items are regarded as NOP
            // OpCode.__BytecodeEnd just skip them to prevent evaling lots of NOP

            _instructions.Add(new Instruction(OpCode.__BytecodeEnd));
            _instructions.Count = _instructions.Capacity;
        }

        return new Bytecode(_instructions.MoveToImmutable(), _lineTableBuilder.ToLineTable(), [.. _consts.Keys], [.. _names.Keys]);
    }
    internal override void PushMetaInfo(CodeMetaInfo? info)
    {
        _metaInfoStack.Push(info);
        _lineTableBuilder.Write(_instructions.Count, info);
    }

    internal override void PopMetaInfo()
    {
        _metaInfoStack.Pop();
        _lineTableBuilder.Write(_instructions.Count, _metaInfoStack.TryPeek(out var info) ? info : null);
    }

    private void Complete()
    {
        Span<byte> bytes = stackalloc byte[4];

        for (int i = 0; i < _instructions.Count; i++)
        {
            var instruction = _instructions[i];

            if (!instruction.OpCode.HasFlag(OpCode.__LabelFlag))
                continue;

            bytes.Clear();
            bytes[0] = instruction.Arg;

            for (int j = 1; j < 4; j++)
                bytes[j] = _instructions[i + j].Arg;

            var labelId = BinaryPrimitives.ReadInt32BigEndian(bytes);
            var arg = _labelOffsets[labelId - 1];

            BinaryPrimitives.WriteInt32BigEndian(bytes, arg);
            for (int j = 0; j < 3; j++)
                _instructions[i + j] = new Instruction(OpCode.ExtendedArg, bytes[j]);
            _instructions[i + 3] = new Instruction(instruction.OpCode & ~OpCode.__LabelFlag, bytes[3]);

            i += 3;
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

    private void InternalEmit(OpCode opCode, int arg)
    {
        if (arg > byte.MaxValue)
        {
            Span<byte> bytes = stackalloc byte[4];
            BinaryPrimitives.WriteInt32BigEndian(bytes, arg);
            arg = bytes[3];

            foreach (var b in bytes[..3].TrimStart(default(byte)))
                _instructions.Add(new Instruction(OpCode.ExtendedArg, b));
        }

        var instruction = new Instruction(opCode, (byte)arg);
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

        InternalEmit(opCode, arg: default);
    }

    public override void Emit(OpCode opCode, int arg)
    {
        Debug.Assert(arg >= 0, "Negative arg is used for label.");

        InternalEmit(opCode, arg);
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

        opCode |= OpCode.__LabelFlag;
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(bytes, label.Id);
        InternalEmit(opCode, bytes[0]);
        for (int i = 1; i < 4; i++)
            InternalEmit(OpCode.__LabelFlag, bytes[i]);
    }

    public override void Emit(OpCode opCode, PyObject pyObject)
    {
        if (!_consts.TryGetValue(pyObject, out var index))
            _consts[pyObject] = index = _consts.Count;
        InternalEmit(opCode, index);
    }

    public override void Emit(OpCode opCode, string name)
    {
        if (!_names.TryGetValue(name, out var index))
            _names[name] = index = _names.Count;
        InternalEmit(opCode, index);
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