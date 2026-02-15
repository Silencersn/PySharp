using PySharp.Compilation.CodeAnalysis;
using PySharp.Modules.Builtins;
using PySharp.Runtime.Comparison;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PySharp.Compilation.Bytecodes;

internal sealed class OptimizedBytecodeGenerator : BytecodeGenerator
{
    public struct FatInstruction
    {
        public OpCode OpCode;
        public int Arg;
        public object? Operand;

        public readonly string String => (string)Operand!;
        public readonly PyObject PyObject => (PyObject)Operand!;

        public FatInstruction(OpCode opCode, int arg = 0)
        {
            OpCode = opCode;
            Arg = arg;
        }
        public FatInstruction(OpCode opCode, Label label)
        {
            OpCode = opCode;
            Arg = -label.Id;
        }
        public FatInstruction(OpCode opCode, string operand)
        {
            OpCode = opCode;
            Operand = operand;
        }
        public FatInstruction(OpCode opCode, PyObject operand)
        {
            OpCode = opCode;
            Operand = operand;
        }
    }

    private readonly List<FatInstruction> _instructions = [];
    private readonly List<int> _labelOffsets = [];
    private readonly OrderedDictionary<int, CodeMetaInfo?> _infos = [];
    private readonly Stack<CodeMetaInfo?> _metaInfoStack = [];

    private FatInstruction _lastInstruction;

    internal override Bytecode ToBytecode()
    {
        var fatInstructions = CollectionsMarshal.AsSpan(_instructions);
        var instructions = new List<Instruction>(fatInstructions.Length);
        var consts = new OrderedDictionary<PyObject, int>(PyObjectConstEqualityComparer.Shared);
        var names = new OrderedDictionary<string, int>(StringComparer.Ordinal);

        foreach (FatInstruction instruction in fatInstructions)
        {
            if (instruction.Arg < 0)
            {
                instructions.Add(new Instruction(instruction.OpCode, _labelOffsets[-instruction.Arg - 1]));
            }
            else if (instruction.Operand is PyObject pyObject)
            {
                if (!consts.TryGetValue(pyObject, out var index))
                    consts[pyObject] = index = consts.Count;
                instructions.Add(new Instruction(instruction.OpCode, index));
            }
            else if (instruction.Operand is string name)
            {
                if (!names.TryGetValue(name, out var index))
                    names[name] = index = names.Count;
                instructions.Add(new Instruction(instruction.OpCode, index));
            }
            else
            {
                instructions.Add(new Instruction(instruction.OpCode, instruction.Arg));
            }
        }

        return new Bytecode([.. instructions], [.. _infos.Reverse()], [.. consts.Keys], [.. names.Keys]);
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


    private static bool IsStackTopBool(FatInstruction instruction)
    {
        return instruction.OpCode switch
        {
            OpCode.ToBool => true,
            OpCode.IsOp => true,

            _ => false
        };
    }

    private void InternalEmit(FatInstruction instruction)
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

        InternalEmit(new FatInstruction(opCode));
    }

    public override void Emit(OpCode opCode, int arg)
    {
        InternalEmit(new FatInstruction(opCode, arg));
    }

    public override void Emit(OpCode opCode, Label label)
    {
        if (opCode is OpCode.PopJumpIfFalse or OpCode.PopJumpIfTrue)
        {
            if (_lastInstruction.OpCode is OpCode.UnaryNot)
                opCode = opCode is OpCode.PopJumpIfFalse ? OpCode.PopJumpIfTrue : OpCode.PopJumpIfFalse;
        }

        InternalEmit(new FatInstruction(opCode, label));
    }

    public override void Emit(OpCode opCode, PyObject pyObject)
    {
        InternalEmit(new FatInstruction(opCode, pyObject));
    }

    public override void Emit(OpCode opCode, string name)
    {
        InternalEmit(new FatInstruction(opCode, name));
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