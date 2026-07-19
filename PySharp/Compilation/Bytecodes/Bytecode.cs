using PySharp.Modules.Builtins;
using PySharp.Utility;
using System.Collections.Immutable;
using System.Diagnostics;

namespace PySharp.Compilation.Bytecodes;

public sealed class Bytecode
{
    private bool _trimmed;
    private ImmutableArray<Instruction> _instructions;
    private readonly LineTable _lineTable;
    private readonly ImmutableArray<PyObject> _consts;
    private readonly ImmutableArray<string> _names;
    private readonly int _stackSize;

    internal Bytecode(ImmutableArray<Instruction> instructions, LineTable lineTable, ImmutableArray<PyObject> consts, ImmutableArray<string> names)
    {
        _instructions = instructions;
        _lineTable = lineTable;
        _consts = consts;
        _names = names;
        _stackSize = StackSizeHelper.Calculate(instructions);
    }

    internal ImmutableArray<Instruction> Instructions => _instructions;
    internal LineTable LineTable => _lineTable;

    public ImmutableArray<PyObject> Consts => _consts;
    public ImmutableArray<string> Names => _names;
    public int StackSize => _stackSize;

    public void TrimExcess(bool recursive = false)
    {
        if (recursive)
        {
            foreach (var obj in _consts)
            {
                if (obj is not PyCodeObject { Bytecode: var bytecode })
                    continue;

                bytecode.TrimExcess(recursive: true);
            }
        }

        if (_trimmed)
            return;

        var instructionsCount = 0;
        for (int i = _instructions.Length - 1; i >= 0; i--)
        {
            // if _instructions contains OpCode.__BytecodeEnd,
            // it must end with nops

            var opCode = _instructions[i].OpCode;

            if (opCode is OpCode.NoOperation)
                continue;

            // OpCode.__BytecodeEnd should be trimmed
            instructionsCount = opCode is OpCode.__BytecodeEnd ? i : i + 1;
            break;
        }

        var instructionsThreshold = _instructions.Length * 0.9;
        if (instructionsCount < instructionsThreshold)
            _instructions = _instructions[..instructionsCount];

        _lineTable.TrimExcess();
        _trimmed = true;
    }

    private struct StackSizeHelper
    {
        public static int Calculate(ImmutableArray<Instruction> instructions)
        {
            return new StackSizeHelper().InternalCalculate(instructions);
        }

        private int _maxStackSize;
        private int _currentStackSize;
        private int _arg;

        private int InternalCalculate(ImmutableArray<Instruction> instructions)
        {
            if (instructions.IsEmpty)
                return 0;

            using var rentedArray = PoolHelper.Rent<int>(instructions.Length);
            var stackSizes = rentedArray.Span;
            stackSizes.Fill(-1);
            stackSizes[0] = 0;

            Queue<int> targets = [];
            targets.Enqueue(0);

            while (targets.Count > 0)
            {
                var target = targets.Dequeue();
                _currentStackSize = stackSizes[target];

                for (int i = target; i < instructions.Length; i++)
                {
                    var instruction = instructions[i];

                    var arg = instruction.Arg | _arg;

                    IncrementStackSizeByInstruction(instruction);

                    if (IsJump(instruction.OpCode))
                    {
                        int nextTarget = arg;

                        if (nextTarget < instructions.Length && stackSizes[nextTarget] is -1)
                        {
                            stackSizes[nextTarget] = _currentStackSize;
                            targets.Enqueue(nextTarget);
                        }
                    }

                    if (i == stackSizes.Length - 1 || stackSizes[i + 1] is not -1 ||
                        instruction.OpCode is OpCode.__BytecodeEnd or OpCode.ReturnValue or OpCode.Jump or OpCode.RaiseVarArgs)
                        break;
                }
            }

            return _maxStackSize;
        }
        private static bool IsJump(OpCode op)
        {
            return op is OpCode.Jump or OpCode.PopJumpIfFalse or OpCode.PopJumpIfTrue or OpCode.PopJumpIfNone
                or OpCode.ForIter or OpCode.Send or OpCode._CheckMatch or OpCode._PopExceptionAndJumpIfNull
                or OpCode._SetupFinally or OpCode._SetupExcept;
        }

        private void IncrementStackSize(int delta)
        {
            _currentStackSize += delta;
            if (_currentStackSize > _maxStackSize)
                _maxStackSize = _currentStackSize;
            if (_currentStackSize < 0)
                _currentStackSize = 0;
        }

        private void IncrementStackSizeByInstruction(Instruction instruction)
        {
            _arg |= instruction.Arg;

            switch (instruction.OpCode)
            {
                case OpCode.ExtendedArg:
                case OpCode.NoOperation:
                case OpCode.DeleteName:
                case OpCode.DeleteGlobal:
                case OpCode.DeleteFast:
                case OpCode.DeleteDeref:
                case OpCode._DeleteDerefFast:
                case OpCode.LoadAttr:
                case OpCode.GetIter:
                case OpCode.Swap:
                case OpCode.ToBool:
                case OpCode.Jump:
                case OpCode.CheckExcMatch:
                case OpCode.CheckEgMatch:
                case OpCode._CheckMatch:
                case OpCode.YieldValue:
                case OpCode.GetYieldFromIter:
                case OpCode.GetAwaitable:
                case OpCode.GetAIter:
                case OpCode._CheckExcToRaise:
                case OpCode.MakeCell:
                case OpCode._MakeCellFast:
                case OpCode._ListToTuple:
                case OpCode._ListToSet:
                case OpCode._EnterInlineFrame:
                case OpCode._ExitInlineFrame:
                case OpCode.ConvertValue:
                case OpCode.FormatSimple:
                case OpCode._SetupFinally:
                case OpCode._SetupExcept:
                case OpCode._EnterFinally:
                case OpCode._ExitFinally:
                case OpCode._PopException:
                case OpCode._PopExceptionIfTrue:
                case OpCode._PopExceptionAndJumpIfNull:
                case OpCode._UnaryOp:
                case OpCode.UnaryNot:
                case OpCode.SetupAnnotations:
                case OpCode._MakeTypeVar:
                case OpCode._MakeTypeAlias:
                case OpCode.__BytecodeEnd:
                    break;

                case OpCode.LoadConst:
                case OpCode.LoadSpecial:
                case OpCode._LoadHitExcept:
                case OpCode.LoadName:
                case OpCode.LoadGlobal:
                case OpCode.LoadFast:
                case OpCode.LoadDeref:
                case OpCode._LoadDerefFast:
                case OpCode.ForIter:
                case OpCode.Copy:
                case OpCode._LoadExc:
                case OpCode.Send:
                case OpCode.GetANext:
                case OpCode.PushNull:
                case OpCode.MatchSequence:
                case OpCode.MatchMapping:
                case OpCode.GetLen:
                case OpCode.MatchKeys:
                case OpCode.ImportFrom:
                case OpCode.ReturnGenerator:
                    IncrementStackSize(1);
                    break;

                case OpCode.LoadMethod:
                    IncrementStackSize(2);
                    break;

                case OpCode._LoadExcInfo:
                    IncrementStackSize(3);
                    break;

                case OpCode.StoreName:
                case OpCode.StoreGlobal:
                case OpCode.StoreFast:
                case OpCode.StoreDeref:
                case OpCode._StoreDerefFast:
                case OpCode._StoreNameIncludedNonInlineFrame:
                case OpCode._StoreDerefIncludedNonInlineFrame:
                case OpCode.DeleteAttr:
                case OpCode.PopIter:
                case OpCode.BinaryOp:
                case OpCode.CompareOp:
                case OpCode.ContainsOp:
                case OpCode.IsOp:
                case OpCode._AugAssignOp:
                case OpCode.PopTop:
                case OpCode.ReturnValue:
                case OpCode.ListAppend:
                case OpCode.ListExtend:
                case OpCode.SetAdd:
                case OpCode.DictUpdate:
                case OpCode.DictMerge:
                case OpCode.BinarySubscr:
                case OpCode.FormatWithSpec:
                case OpCode.ImportName:
                case OpCode._ImportAllFrom:
                case OpCode._CallPrintIfNotNone:
                case OpCode._SetFunctionTypeParams:
                case OpCode.PopJumpIfFalse:
                case OpCode.PopJumpIfTrue:
                case OpCode.PopJumpIfNone:
                case OpCode.BuildTemplate:
                    IncrementStackSize(-1);
                    break;

                case OpCode.StoreAttr:
                case OpCode.CallFunctionEx:
                case OpCode.MapAdd:
                case OpCode.DeleteSubscr:
                case OpCode.MatchClass:
                    IncrementStackSize(-2);
                    break;

                case OpCode.Call:
                case OpCode.RaiseVarArgs:
                    IncrementStackSize(-_arg);
                    break;

                case OpCode._MakeFunctionWithPyArgsDef:
                    // Pops 2 tuples + codeObj, pushes 1 function. Net = -2.
                    IncrementStackSize(-2);
                    break;

                case OpCode._BuildClass:
                case OpCode.CallKw:
                    IncrementStackSize(-_arg - 1);
                    break;

                case OpCode.BuildList:
                case OpCode.BuildTuple:
                case OpCode.BuildSet:
                case OpCode.BuildSlice:
                case OpCode.BuildString:
                    IncrementStackSize(-_arg + 1);
                    break;

                case OpCode.BuildMap:
                    IncrementStackSize(-_arg * 2 + 1);
                    break;

                case OpCode.UnpackSequence:
                    IncrementStackSize(_arg - 1);
                    break;

                case OpCode.UnpackEx:
                    IncrementStackSize((_arg & ushort.MaxValue) + ((_arg >> 16) & ushort.MaxValue));
                    break;

                case OpCode.StoreSubscr:
                    IncrementStackSize(-3);
                    break;

                case OpCode.BuildInterpolation:
                    IncrementStackSize(-(_arg & 0b11) + 1);
                    break;

                default:
                    throw new UnreachableException();
            }

            if (instruction.OpCode is OpCode.ExtendedArg)
                _arg <<= 8;
            else
                _arg = 0;
        }
    }
}
