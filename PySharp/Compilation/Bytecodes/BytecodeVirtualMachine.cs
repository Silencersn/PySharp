using PySharp.Compilation.CodeAnalysis;
using PySharp.Compilation.Primitives;
using PySharp.Modules.Builtins;
using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.Calls.Extensions;
using PySharp.Utility;
using System.Collections;
using System.Collections.Frozen;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Xml.Linq;

namespace PySharp.Compilation.Bytecodes;

internal static class BytecodeVirtualMachine
{
    internal sealed class ExceptionHandler
    {
        public const int State_Init = 0, State_Except = 1, State_Finally = 2, State_End = 3;

        public int ExceptOffset;
        public int FinallyOffset;
        public int State;
        public PyExceptionObject? PyException;
        public int StackDepth;
        public PyObject? ReturnValue;
        public bool HitExcept;

        public ExceptionHandler(int exceptOffset, int finallyOffset)
        {
            ExceptOffset = exceptOffset;
            FinallyOffset = finallyOffset;
            State = State_Init;
            PyException = null;
        }
    }

    internal static PyResult Eval(ref BytecodeVirtualMachineStates states)
    {
        var context = states.Context;
        ref var frame = ref context.CurrentInternalFrame;
        var callDepth = 0;
        PyResult evalResult = default;
        bool needCheckEvalResult = false;

        #region Eval Body

    eval_begin:
        Debug.Assert(frame.CodeObject is not null);
        ref int currentIndex = ref frame.InstructionIndex;
        var instructions = frame.CodeObject.Bytecode.Instructions.AsSpan();
        var consts = frame.CodeObject.Bytecode.Consts.AsSpan();
        var names = frame.CodeObject.Bytecode.Names.AsSpan();
        var length = instructions.Length;
        ValueOperandStack Stack;
        if (states.Stack is not null)
            Stack = states.Stack.AsValueOperandStack();
        else
            Stack = new ValueOperandStack(frame.Variables.OperandStackSpan);
        Stack.SetSize(states.OperandStackSize);

        // cache, clear before using
        PyObject value, left, right;
        bool boolValue;
        PyResult result;
        PyObject? returnValue = null, intermediateValue = null;

        PyObject? callable = null;
        IReadOnlyList<PyObject>? callArgs = null;
        IReadOnlyDictionary<string, PyObject>? callKwargs = null;

        int instructionArg = 0;

    eval_resume:
        try
        {
            if (needCheckEvalResult)
            {
                needCheckEvalResult = false;
                Stack.Push(evalResult.PyUnwrap(context));
            }

            while (currentIndex < length)
            {
                var instruction = instructions[currentIndex];
                var nextIndex = currentIndex + 1;

                #region Eval OpCode

                instructionArg |= instruction.Arg;

                switch (instruction.OpCode)
                {
                    case OpCode.NoOperation:
                        break;

                    case OpCode.ExtendedArg:
                        instructionArg <<= 8;
                        break;

                    case OpCode.LoadConst:
                        Stack.Push(consts[instructionArg]);
                        break;

                    case OpCode.LoadSpecial:
                        value = names[instructionArg] switch
                        {
                            PySpecialNames.Enter => new PyWrapperDescriptorObject(
                                Stack[-1].PyType.Slots.Enter ??
                                throw context.TypeError(PySR.Runtime_WithStmt_MissingEnter, Stack[-1].PyType.FullName)),
                            PySpecialNames.Exit => new PyWrapperDescriptorObject(
                                Stack[-1].PyType.Slots.Exit ??
                                throw context.TypeError(PySR.Runtime_WithStmt_MissingExit, Stack[-1].PyType.FullName)),

                            _ => throw new UnreachableException()
                        };
                        Stack.Push(value);
                        break;

                    case OpCode._LoadExcInfo:
                        {
                            var exc = states.CurrentException;
                            Stack.PushRange(exc.PyType, exc, PyTraceback.CaptureCurrentFrame(context));
                        }
                        break;

                    case OpCode._LoadHitExcept:
                        Stack.Push(PyBoolObject.FromBoolean(states.ExceptionHandlers.Peek().HitExcept));
                        break;

                    case OpCode.LoadName:
                        value = frame.Variables.LoadName(names[instructionArg]).PyUnwrap(context);
                        Stack.Push(value);
                        break;

                    case OpCode.LoadGlobal:
                        value = frame.Variables.LoadGlobal(names[instructionArg]).PyUnwrap(context);
                        Stack.Push(value);
                        break;

                    case OpCode.LoadFast:
                        value = frame.Variables.LoadFast(instructionArg).PyUnwrap(context);
                        Stack.Push(value);
                        break;

                    case OpCode._LoadDerefFast:
                        value = frame.Variables.LoadDerefFast(instructionArg).PyUnwrap(context);
                        Stack.Push(value);
                        break;

                    case OpCode.LoadDeref:
                        value = frame.Variables.LoadDeref(names[instructionArg]).PyUnwrap(context);
                        Stack.Push(value);
                        break;

                    case OpCode.StoreName:
                        value = Stack.Pop();
                        frame.Variables.StoreName(names[instructionArg], value);
                        break;

                    case OpCode.StoreGlobal:
                        value = Stack.Pop();
                        frame.Variables.StoreGlobal(names[instructionArg], value);
                        break;

                    case OpCode.StoreFast:
                        value = Stack.Pop();
                        frame.Variables.StoreFast(instructionArg, value);
                        break;

                    case OpCode._StoreDerefFast:
                        value = Stack.Pop();
                        _ = frame.Variables.StoreDerefFast(instructionArg, value).PyUnwrap(context);
                        break;

                    case OpCode.StoreDeref:
                        value = Stack.Pop();
                        frame.Variables.StoreDeref(names[instructionArg], value);
                        break;

                    case OpCode._StoreNameIncludedNonInlineFrame:
                        value = Stack.Pop();
                        frame.Variables.StoreName(names[instructionArg], value);
                        if (frame.FrameType is FrameType.Comprehension)
                            context.FrameState.FindOuterNonInlineFrame()
                                .Variables.StoreName(names[instructionArg], value);
                        break;

                    case OpCode._StoreDerefIncludedNonInlineFrame:
                        value = Stack.Pop();
                        _ = frame.Variables.StoreDeref(names[instructionArg], value).PyUnwrap(context);
                        if (frame.FrameType is FrameType.Comprehension)
                            context.FrameState.FindOuterNonInlineFrame()
                                .Variables.StoreDeref(names[instructionArg], value).PyUnwrap(context);
                        break;

                    case OpCode.DeleteName:
                        frame.Variables.DeleteName(names[instructionArg]).PyUnwrap(context);
                        break;

                    case OpCode.DeleteGlobal:
                        frame.Variables.DeleteGlobal(names[instructionArg]).PyUnwrap(context);
                        break;

                    case OpCode.DeleteFast:
                        frame.Variables.DeleteFast(instructionArg).PyUnwrap(context);
                        break;

                    case OpCode._DeleteDerefFast:
                        frame.Variables.DeleteDerefFast(instructionArg).PyUnwrap(context);
                        break;

                    case OpCode.DeleteDeref:
                        frame.Variables.DeleteDeref(names[instructionArg]);
                        break;

                    case OpCode.LoadAttr:
                        {
                            Stack[-1] = PyOperators.GetAttr(context, Stack[-1], names[instructionArg]).PyUnwrap(context);
                        }
                        break;

                    case OpCode.StoreAttr:
                        {
                            // Stack: [..., value, obj]

                            PyOperators.SetAttr(context, Stack.Pop(), names[instructionArg], Stack.Pop()).PyUnwrap(context);
                        }
                        break;

                    case OpCode.DeleteAttr:
                        {
                            value = Stack.Pop();
                            PyOperators.DelAttr(context, value, names[instructionArg]).PyUnwrap(context);
                        }
                        break;

                    case OpCode.BinarySubscr:
                        {
                            // Stack: [..., container, key]

                            value = Stack.Pop(); // key
                            value = PySpecialMethods.GetItem(context, Stack.Pop() /* container */, value).PyUnwrap(context);
                            Stack.Push(value);
                        }
                        break;

                    case OpCode.StoreSubscr:
                        {
                            // Stack: [..., value, container, key]

                            value = Stack.Pop(); // key
                            _ = PySpecialMethods.SetItem(context, Stack.Pop() /* container */, value, Stack.Pop() /* value */).PyUnwrap(context);
                        }
                        break;

                    case OpCode.DeleteSubscr:
                        {
                            // Stack: [..., container, key]

                            value = Stack.Pop(); // key
                            _ = PySpecialMethods.DelItem(context, Stack.Pop() /* container */, value).PyUnwrap(context);
                        }
                        break;

                    case OpCode.LoadMethod:
                        {
                            value = Stack[-1];
                            Stack[-1] = PyCore.GetAttrOrMethod(context, value, names[instructionArg], out var isMethod).PyUnwrap(context);
                            Stack.Push(isMethod ? value : null! /* this null will be handled by OpCode.Call or OpCode.CallKw */);
                        }
                        break;

                    case OpCode.Call:
                        {
                            boolValue = instructionArg > 0 && Stack[-instructionArg] is null;
                            if (boolValue)
                                instructionArg--;

                            LoadArgs(ref Stack, states.CacheArgs, instructionArg);
                            if (boolValue)
                                Stack.Pop();

                            callable = Stack.Pop();
                            callArgs = states.CacheArgs;
                            callKwargs = FrozenDictionary<string, PyObject>.Empty;
                            goto case OpCode.__CallImpl;
                        }

                    case OpCode.CallKw:
                        {
                            var tuple = (PyTupleObject)Stack.Pop();
                            states.CacheKwargs.Clear();

                            LoadArgs(ref Stack, states.CacheArgs, tuple.Count);

                            for (int i = 0; i < tuple.Count; i++)
                            {
                                var str = (PyStrObject)tuple[i];
                                states.CacheKwargs.Add(str.Value, states.CacheArgs[i]);
                            }

                            var argsCount = instructionArg - states.CacheKwargs.Count;
                            var isNull = argsCount > 0 && Stack[-argsCount] is null;
                            if (isNull)
                                argsCount--;

                            LoadArgs(ref Stack, states.CacheArgs, argsCount);
                            if (isNull)
                                Stack.Pop();

                            callable = Stack.Pop();
                            callArgs = states.CacheArgs;
                            callKwargs = states.CacheKwargs;
                            goto case OpCode.__CallImpl;
                        }

                    case OpCode.CallFunctionEx:
                        {
                            var dict = (PyDictObject)Stack.Pop();
                            var pyargs = (PyListObject)Stack.Pop();
                            states.CacheKwargs.Clear();

                            foreach (var pair in dict)
                            {
                                if (pair.Key is not PyStrObject str)
                                    throw context.TypeError(PySR.Runtime_Keyword_KeywordsMustBeStrings);
                                states.CacheKwargs.Add(str.Value, pair.Value);
                            }

                            callable = Stack.Pop();
                            callArgs = pyargs;
                            callKwargs = states.CacheKwargs;
                            goto case OpCode.__CallImpl;
                        }

                    case OpCode.__CallImpl:
                        {
                            Debug.Assert(instruction.OpCode is OpCode.Call or OpCode.CallKw or OpCode.CallFunctionEx);
                            Debug.Assert(callable is not null);
                            Debug.Assert(callArgs is not null);
                            Debug.Assert(callKwargs is not null);

                            if (callable is not PyFunctionObject func)
                            {
                                value = callable.Call(context, callArgs, callKwargs).PyUnwrap(context);
                                Stack.Push(value);
                                break;
                            }

                            if (func.Code.Flags is not CodeObjectFlags.Function)
                            {
                                value = func.InternalCall(context, callArgs, callKwargs).PyUnwrap(context);
                                Stack.Push(value);
                                break;
                            }
                            
                            InlinePyObjectArray buffer = default;
                            if (!func._def.TryParse(callArgs, callKwargs, buffer, out var arguments))
                                return PyResult.TypeError(null /* TODO */);

                            frame.InstructionIndex++;
                            var newFrame = PyInternalFrame.CreateFuncCallFrame(context, func, FrameType.Function, func._globals, func.Code);
                            newFrame.InitArgs(func._def, func.Code, arguments, func.Closure);
                            context.FrameState.EnterFrame(ref newFrame);
                            callDepth++;
                            frame = ref context.CurrentInternalFrame;
                            states.OperandStackSize = Stack.Count;
                            context.FrameState.PushStates(ref states);
                            states = new BytecodeVirtualMachineStates(context, usingLocalsPlusAsOperandStack: true);
                            goto eval_begin;
                        }

                    case OpCode.PopTop:
                        Stack.Pop();
                        break;

                    case OpCode.Copy:
                        value = Stack[-instructionArg];
                        Stack.Push(value);
                        break;

                    case OpCode.Swap:
                        (Stack[-1], Stack[-instructionArg]) = (Stack[-instructionArg], Stack[-1]);
                        break;

                    case OpCode.ToBool:
                        Stack[-1] = PySpecialMethods.Bool(context, Stack[-1]).PyUnwrap(context);
                        break;

                    case OpCode.Jump:
                        nextIndex = instructionArg;
                        break;

                    case OpCode.PopJumpIfFalse:
                        {
                            value = Stack.Pop();
                            boolValue = ((PyBoolObject)value).BoolValue;
                            if (!boolValue)
                                nextIndex = instructionArg;
                        }
                        break;

                    case OpCode.PopJumpIfTrue:
                        {
                            value = Stack.Pop();
                            boolValue = ((PyBoolObject)value).BoolValue;
                            if (boolValue)
                                nextIndex = instructionArg;
                        }
                        break;

                    case OpCode.PopJumpIfNone:
                        {
                            value = Stack.Pop();
                            if (value is PyNoneObject)
                                nextIndex = instructionArg;
                        }
                        break;

                    case OpCode.GetIter:
                        Stack[-1] = PySpecialMethods.Iter(context, Stack[-1]).PyUnwrap(context);
                        break;

                    case OpCode.ForIter:
                        result = PySpecialMethods.Next(context, Stack.Peek());
                        if (result.IsStopIteration)
                            nextIndex = instructionArg;
                        else
                            Stack.Push(result.PyUnwrap(context));
                        break;

                    case OpCode.PopIter:
                        Stack.Pop();
                        break;

                    case OpCode.BinaryOp:
                        right = Stack.Pop();
                        left = Stack.Pop();
                        value = PyCore.EvalOperator(context, (OperatorType)instructionArg, left, right).PyUnwrap(context);
                        Stack.Push(value);
                        break;

                    case OpCode._UnaryOp:
                        value = Stack.Pop();
                        value = PyCore.EvalOperator(context, (UnaryOpType)instructionArg, value).PyUnwrap(context);
                        Stack.Push(value);
                        break;

                    case OpCode.UnaryNot:
                        boolValue = ((PyBoolObject)Stack.Peek()).BoolValue;
                        Stack[-1] = PyBoolObject.FromBoolean(!boolValue);
                        break;

                    case OpCode.CompareOp:
                        right = Stack.Pop();
                        left = Stack.Pop();
                        value = PyCore.EvalOperator(context, (CmpopType)instructionArg, left, right).PyUnwrap(context);
                        Stack.Push(value);
                        break;

                    case OpCode.IsOp:
                        right = Stack.Pop();
                        left = Stack.Pop();
                        if (instructionArg is 0)
                            value = PyOperators.Is(left, right);
                        else
                            value = PyOperators.IsNot(left, right);
                        Stack.Push(value);
                        break;

                    case OpCode.ContainsOp:
                        right = Stack.Pop();
                        left = Stack.Pop();
                        if (instructionArg is 0)
                            value = PyOperators.In(context, left, right).PyUnwrap(context);
                        else
                            value = PyOperators.NotIn(context, left, right).PyUnwrap(context);
                        Stack.Push(value);
                        break;

                    case OpCode._AugAssignOp:
                        right = Stack.Pop();
                        left = Stack.Pop();
                        value = PyCore.EvalInplaceOperator(context, (OperatorType)instructionArg, left, right).PyUnwrap(context);
                        Stack.Push(value);
                        break;

                    case OpCode.PushNull:
                        // suppressed null warnings
                        // because null is rarely used and
                        // we assume that all nulls can be handled correctly.
                        Stack.Push(null!);
                        break;

                    case OpCode.BuildList:
                        LoadArgs(ref Stack, states.CacheArgs, instructionArg);
                        Stack.Push(PyListObject.CreateList(states.CacheArgs));
                        break;

                    case OpCode.BuildTuple:
                        LoadArgs(ref Stack, states.CacheArgs, instructionArg);
                        Stack.Push(PyTupleObject.CreateTuple(states.CacheArgs));
                        break;

                    case OpCode.BuildSet:
                        LoadArgs(ref Stack, states.CacheArgs, instructionArg);
                        Stack.Push(PySetObject.CreateSet(states.CacheArgs));
                        break;

                    case OpCode.BuildMap:
                        LoadArgs(ref Stack, states.CacheArgs, instructionArg * 2);
                        states.CachePairs.Clear();
                        for (int i = 0; i < instructionArg; i++)
                        {
                            states.CachePairs.Add(KeyValuePair.Create(states.CacheArgs[i * 2], states.CacheArgs[i * 2 + 1]));
                        }
                        Stack.Push(PyDictObject.CreateDict(states.CachePairs));
                        break;

                    case OpCode._EnterInlineFrame:
                        {
                            var inlineFrame = frame.CreateInlineFrame(FrameType.Comprehension);
                            context.FrameState.EnterFrame(ref inlineFrame);
                            frame = ref context.CurrentInternalFrame;
                            currentIndex = ref frame.InstructionIndex;
                        }
                        break;

                    case OpCode._ExitInlineFrame:
                        context.FrameState.ExitInternalFrame(context, dispose: true);
                        frame = ref context.CurrentInternalFrame;
                        currentIndex = ref frame.InstructionIndex;
                        break;

                    case OpCode.ListAppend:
                        {
                            value = Stack.Pop();
                            ((PyListObject)Stack[-instructionArg]).Add(value);
                        }
                        break;

                    case OpCode.ListExtend:
                        {
                            value = Stack.Pop();
                            _ = ((PyListObject)Stack[-instructionArg]).PyExtend(context, value).PyUnwrap(context);
                        }
                        break;

                    case OpCode._ListToTuple:
                        {
                            Stack[-1] = PyTupleObject.CreateTuple((PyListObject)Stack[-1]);
                        }
                        break;

                    case OpCode._ListToSet:
                        {
                            Stack[-1] = PySetObject.CreateSet((PyListObject)Stack[-1]);
                        }
                        break;

                    case OpCode.SetAdd:
                        {
                            value = Stack.Pop();
                            ((PySetObject)Stack[-instructionArg]).Add(value);
                        }
                        break;

                    case OpCode.MapAdd:
                        InternalMapAdd(context, ref Stack, instructionArg);
                        break;

                    case OpCode.DictUpdate:
                        InternalDictUpdate(context, ref Stack, instructionArg);
                        break;

                    case OpCode.DictMerge:
                        InternalDictMerge(context, ref Stack, instructionArg);
                        break;

                    case OpCode.UnpackSequence:
                        InternalUnpackSequence(context, ref Stack, instructionArg);
                        break;

                    case OpCode.UnpackEx:
                        InternalUnpackEx(context, ref Stack, instructionArg);
                        break;

                    case OpCode.ReturnValue:
                        returnValue = Stack.Pop();
                        break;

                    case OpCode.ReturnGenerator:
                        {
                            currentIndex++;
                            intermediateValue = new PyBytecodeGeneratorObject(frame.CallerName, frame, states);
                        }
                        break;

                    case OpCode.YieldValue:
                        {
                            intermediateValue = Stack.Pop();
                            currentIndex++;
                        }
                        break;

                    case OpCode.GetYieldFromIter:
                        if (!PyGeneratorObjectType.Shared.IsInstance(Stack[-1]))
                            goto case OpCode.GetIter;
                        break;

                    case OpCode.Send:
                        InternalSend(context, ref states, ref Stack, ref nextIndex, instructionArg);
                        break;

                    case OpCode._CheckExcToRaise:
                        if (states.ExceptionToRaise is not null)
                        {
                            var exc = Move(ref states.ExceptionToRaise);
                            throw new PyRuntimeException(exc);
                        }
                        break;

                    case OpCode.ConvertValue:
                        value = Stack.Pop();
                        if (instructionArg is 1)
                            value = PySpecialMethods.Str(context, value).PyUnwrap(context);
                        else if (instructionArg is 2)
                            value = PySpecialMethods.Repr(context, value).PyUnwrap(context);
                        else if (instructionArg is 3)
                            value = PyBuiltinFunctions.Ascii.Call(context, [value]).PyUnwrap(context);
                        else
                            throw new UnreachableException();
                        Stack.Push(value);
                        break;

                    case OpCode.FormatSimple:
                        Stack[-1] = PySpecialMethods.Format(context, Stack[-1], PyStrObject.Empty).PyUnwrap(context);
                        break;

                    case OpCode.FormatWithSpec:
                        value = Stack.Pop();
                        Stack[-1] = PySpecialMethods.Format(context, Stack[-1], value).PyUnwrap(context);
                        break;

                    case OpCode.BuildString:
                        if (instructionArg is 0)
                        {
                            Stack.Push(PyStrObject.Empty);
                        }
                        else if (instructionArg is 1)
                        {
                            Debug.Assert(Stack.Peek() is PyStrObject);
                        }
                        else
                        {
                            states.CacheBuilder.Clear();
                            LoadArgs(ref Stack, states.CacheArgs, instructionArg);
                            foreach (var arg in states.CacheArgs)
                            {
                                Debug.Assert(arg is PyStrObject);
                                states.CacheBuilder.Append(((PyStrObject)arg).Value);
                            }
                            Stack.Push(PyStrObject.FromString(states.CacheBuilder.ToString()));
                        }
                        break;

                    case OpCode.ImportName:
                        InternalImportName(context, ref Stack, names, instructionArg);
                        break;

                    case OpCode.ImportFrom:
                        value = PyOperators.GetAttr(context, Stack[-1], names[instructionArg]).PyUnwrap(context);
                        Stack.Push(value);
                        break;

                    case OpCode._ImportAllFrom:
                        PyCore.ImportAllFrom(context, ref frame, (PyModuleObject)Stack.Pop());
                        break;

                    case OpCode.BuildSlice:
                        InternalBuildSlice(ref Stack, instructionArg);
                        break;

                    case OpCode._MakeFunctionWithPyArgsDef:
                        InternalMakeFunctionWithPyArgsDef(ref frame, ref Stack);
                        break;

                    case OpCode._BuildClass:
                        InternalBuildClass(context, ref Stack, ref states, instructionArg);
                        break;

                    case OpCode._LoadClass:
                        Debug.Assert(frame.Caller is not null);
                        value = frame.Caller;
                        Stack.Push(value);
                        break;

                    case OpCode.MakeCell:
                        frame.Variables.StoreLocal(names[instructionArg], PyCellObject.CreateEmpty());
                        break;

                    case OpCode._MakeCellFast:
                        {
                            value = PyCellObject.CreateCell(frame.Variables.LocalsSpan[instructionArg]);
                            frame.Variables.StoreFast(instructionArg, value);
                        }
                        break;

                    case OpCode.RaiseVarArgs:
                        InternalRaiseVarArgs(context, ref Stack, ref states, instructionArg);
                        break;

                    case OpCode.CheckExcMatch:
                        var condition = PyCore.MakeExceptCondition(context, Stack[-1]);
                        Stack[-1] = PyBoolObject.FromBoolean(condition(states.CurrentException));
                        break;

                    case OpCode.CheckEgMatch:
                        InternalCheckEgMatch(context, ref Stack, ref states, instructionArg);
                        break;

                    case OpCode._CheckMatch:
                        if (Stack.Peek() is PyNoneObject)
                        {
                            Stack.Pop();
                            nextIndex = instructionArg;
                        }
                        break;

                    case OpCode._LoadExc:
                        Stack.Push(states.CurrentException);
                        break;

                    case OpCode._SetupFinally:
                        var handler = new ExceptionHandler(-1, instructionArg) { StackDepth = Stack.Count };
                        states.ExceptionHandlers.Push(handler);
                        break;

                    case OpCode._SetupExcept:
                        states.ExceptionHandlers.Peek().ExceptOffset = instructionArg;
                        break;

                    case OpCode._EnterFinally:
                        states.ExceptionHandlers.Peek().State = ExceptionHandler.State_Finally;
                        break;

                    case OpCode._ExitFinally:
                        var currentHandler = states.ExceptionHandlers.Peek();
                        currentHandler.State = ExceptionHandler.State_End;
                        if (currentHandler.PyException is not null)
                        {
                            var exc = currentHandler.PyException;
                            states.ExceptionHandlers.Pop();
                            throw new PyRuntimeException(exc);
                        }
                        states.ExceptionHandlers.Pop();
                        states.Exceptions.Clear();
                        if (currentHandler.ReturnValue is not null)
                            returnValue = Move(ref currentHandler.ReturnValue);
                        break;

                    case OpCode._PopException:
                        states.Exceptions.Pop();
                        states.ExceptionHandlers.Peek().PyException = null;
                        break;

                    case OpCode._PopExceptionIfTrue:
                        value = Stack.Peek();
                        boolValue = ((PyBoolObject)value).BoolValue;
                        if (boolValue)
                            goto case OpCode._PopException;
                        break;

                    case OpCode._PopExceptionAndJumpIfNull:
                        if (states.Exceptions.Peek() is null)
                        {
                            nextIndex = instructionArg;
                            goto case OpCode._PopException;
                        }
                        break;

                    case OpCode._CallPrintIfNotNone:
                        value = Stack.Pop();
                        if (value is not PyNoneObject)
                            _ = PyBuiltinFunctions.Print.Call(context, [value]).PyUnwrap(context);
                        break;

                    case OpCode.MatchSequence:
                        boolValue = PyCore.IsSequenceForMatch(Stack[-1]);
                        Stack.Push(PyBoolObject.FromBoolean(boolValue));
                        break;

                    case OpCode.MatchMapping:
                        boolValue = PyCore.IsMappingForMatch(Stack[-1]);
                        Stack.Push(PyBoolObject.FromBoolean(boolValue));
                        break;

                    case OpCode.GetLen:
                        value = PySpecialMethods.Len(context, Stack.Peek()).PyUnwrap(context);
                        Stack.Push(value);
                        break;

                    case OpCode.MatchKeys:
                        InternalMatchKeys(context, ref Stack);
                        break;

                    case OpCode.MatchClass:
                        InternalMatchClass(context, ref Stack, instructionArg);
                        break;

                    case OpCode.__BytecodeEnd:
                        nextIndex = instructions.Length;
                        break;

                    default:
                        throw new NotImplementedException($"OpCode {instruction.OpCode} is not implemented");
                }

                #endregion Eval OpCode

                if (intermediateValue is not null)
                {
                    evalResult = intermediateValue;
                    goto eval_end;
                }

                if (returnValue is not null)
                {
                find_next_finally:
                    if (!states.ExceptionHandlers.TryPeek(out var handler))
                    {
                        states.RunToEnd = true;
                        evalResult = returnValue;
                        goto eval_end;
                    }

                    if (handler.State is ExceptionHandler.State_Finally)
                    {
                        states.ExceptionHandlers.Pop();
                        goto find_next_finally;
                    }

                    handler.ReturnValue = returnValue;
                    returnValue = null;
                    nextIndex = handler.FinallyOffset;
                }

                if (instruction.OpCode is not OpCode.ExtendedArg)
                    instructionArg = 0;

                currentIndex = nextIndex;
            }
        }
        catch (PyRuntimeException e)
        {
            int nextIndex;
        handle:
            if (!states.ExceptionHandlers.TryPeek(out var currentHandler))
                {
                    Stack.Clear();
                    states.RunToEnd = true;
                    evalResult = PyResult.FromException(e.PyException);
                    goto eval_end;
                }

                if (currentHandler.State is ExceptionHandler.State_Except)
                {
                    // raise exception during except body

                    currentHandler.PyException = e.PyException;
                    nextIndex = currentHandler.FinallyOffset;
                }
                else if (currentHandler.State is ExceptionHandler.State_Finally)
                {
                    // raise exception during finally body

                    states.Exceptions.Clear();
                    states.ExceptionHandlers.Pop();

                    goto handle;
                }
                else
                {
                    Debug.Assert(currentHandler.State is ExceptionHandler.State_Init);
                    currentHandler.PyException = e.PyException;

                    currentHandler.HitExcept = true;
                    if (currentHandler.ExceptOffset is not -1)
                    {
                        currentHandler.State = ExceptionHandler.State_Except;
                        nextIndex = currentHandler.ExceptOffset;
                    }
                    else
                    {
                        nextIndex = currentHandler.FinallyOffset;
                    }
                }

                // TODO: rollback until what?
                while (currentHandler.StackDepth < Stack.Count)
                    Stack.Pop();

                e.PyException.WithTraceback(context, overwriteExisting: false);

                states.Exceptions.Push(e.PyException);

                instructionArg = 0;
                currentIndex = nextIndex;
                goto eval_resume;
        }

        states.RunToEnd = true;
        evalResult = PyNoneObject.None;

        #endregion Eval Body

    eval_end:
        Debug.Assert(!states.RunToEnd || Stack.Count is 0);
        if (states.RunToEnd)
        {
            frame.Dispose(states.Context);
            states.Stack?.Dispose();
        }
        else
        {
            Debug.Assert(states.Stack is not null);
            states.Stack.Count = Stack.Count;
            states.OperandStackSize = Stack.Count;
        }

        if (callDepth > 0)
        {
            callDepth--;
            needCheckEvalResult = true;
            context.FrameState.ExitInternalFrame(context, dispose: true);
            frame = ref context.CurrentInternalFrame;
            states = context.FrameState.PopStates();
            goto eval_begin;
        }

        return evalResult;
    }


    private static void InternalUnpackEx(PyCallContext context, ref ValueOperandStack stack, int instructionArg)
    {
        var postCount = instructionArg & ushort.MaxValue;
        var preCount = (instructionArg >> 16) & ushort.MaxValue;
        var list = PyUtils.IterableToList(context, stack.Pop()).PyUnwrap(context);
        var span = CollectionsMarshal.AsSpan(list.InternalList);
        if (span.Length < preCount + postCount)
            throw context.ValueError(PySR.Runtime_Assignment_NotEnoughToUnpackStarred, preCount + postCount, span.Length);
        stack.PushReversedRange(span[^postCount..]);
        stack.Push(PyListObject.CreateList(span[preCount..^postCount]));
        stack.PushReversedRange(span[..preCount]);
    }

    private static void InternalMatchClass(PyCallContext context, ref ValueOperandStack stack, int instructionArg)
    {
        var keys = (PyTupleObject)stack.Pop();
        var value = stack.Pop();
        if (value is not PyTypeObject cls)
            throw context.TypeError(PySR.Runtime_MatchStmt_CallNonClass);
        var subject = stack.Pop();

        if (!cls.IsInstance(subject))
        {
            stack.Push(PyNoneObject.None);
            return;
        }

        var values = new PyObject[instructionArg + keys.Count];

        if (IsSpecialType(cls))
        {
            if (instructionArg > 1)
                throw context.TypeError(PySR.Runtime_MatchStmt_MatchArgsLengthNotEnough, cls.FullName, 1, instructionArg);
            else if (instructionArg is 1)
                values[0] = subject;
        }
        else if (instructionArg > 0)
        {
            var matchArgs = PyOperators.GetAttr(context, cls, PySpecialNames.MatchArgs).PyUnwrap(context);

            if (matchArgs is not PyTupleObject tuple)
                throw context.TypeError(PySR.Runtime_MatchStmt_MatchArgsIsNonTuple, cls.FullName, matchArgs.PyType.FullName);
            if (instructionArg > tuple.Count)
                throw context.TypeError(PySR.Runtime_MatchStmt_MatchArgsLengthNotEnough, cls.FullName, tuple.Count, instructionArg);

            for (int i = 0; i < instructionArg; i++)
            {
                if (tuple[i] is not PyStrObject attrName)
                    throw context.TypeError(PySR.Runtime_MatchStmt_MatchArgsEltMustBeString, tuple[i].PyType.FullName);

                var attr = PyOperators.GetAttr(context, subject, attrName);
                if (attr.IsAttributeError)
                {
                    stack.Push(PyNoneObject.None);
                    return;
                }

                values[i] = attr.PyUnwrap(context);
            }
        }

        for (int i = 0; i < keys.Count; i++)
        {
            var attrName = keys[i];
            Debug.Assert(attrName is PyStrObject);

            var attr = PyOperators.GetAttr(context, subject, attrName);
            if (attr.IsAttributeError)
            {
                stack.Push(PyNoneObject.None);
                return;
            }

            values[instructionArg + i] = attr.PyUnwrap(context);
        }

        stack.Push(PyTupleObject.CreateProxy(values));

        static bool IsSpecialType(PyTypeObject type)
        {
            // TODO: not implemented: bytearray bytes frozenset
            return type is
                PyBoolObjectType or
                PyDictObjectType or
                PyFloatObjectType or
                PyIntObjectType or
                PyListObjectType or
                PySetObjectType or
                PyStrObjectType or
                PyTupleObjectType;
        }
    }

    private static void InternalMatchKeys(PyCallContext context, ref ValueOperandStack stack)
    {
        var keys = (PyTupleObject)stack.Peek();
        var subject = stack[-2];
        var array = new PyObject[keys.Count];
        var matched = true;
        for (int i = 0; matched && i < array.Length; i++)
        {
            var key = keys[i];
            var result = PySpecialMethods.GetItem(context, subject, key);
            if (result.IsError && PyKeyErrorObjectType.Shared.IsInstance(result.Exception))
            {
                matched = false;
                break;
            }
            array[i] = result.PyUnwrap(context);
        }
        stack.Push(matched ? PyTupleObject.CreateProxy(array) : PyNoneObject.None);
    }

    private static void InternalSend(PyCallContext context, ref BytecodeVirtualMachineStates states,
        ref ValueOperandStack stack, ref int nextIndex, int instructionArg)
    {
        PyObject iter, value;
        PyResult result;

        if (states.ExceptionToRaise is not null)
        {
            // throw or close

            iter = stack[-1];

            if (PyGeneratorExitObjectType.Shared.IsInstance(states.ExceptionToRaise))
            {
                // close sub generator
                var close = PyOperators.GetAttr(context, iter, "close");
                if (!close.IsAttributeError)
                    _ = close.PyUnwrap(context).Call(context).PyUnwrap(context);

                // close self
                if (states.ExceptionToRaise is not null)
                {
                    var exc = Move(ref states.ExceptionToRaise);
                    throw new PyRuntimeException(exc);
                }
            }
            else
            {
                var throwMethod = PyOperators.GetAttr(context, iter, "throw");
                if (!throwMethod.IsAttributeError)
                {
                    var exc = Move(ref states.ExceptionToRaise);
                    value = throwMethod.PyUnwrap(context).Call(context, [exc]).PyUnwrap(context);
                    stack.Push(value);
                }
                else
                {
                    // throw at self
                    if (states.ExceptionToRaise is not null)
                    {
                        var exc = Move(ref states.ExceptionToRaise);
                        throw new PyRuntimeException(exc);
                    }
                }
                return;
            }
        }

        iter = stack[-2];
        value = stack[-1];
        if (value is PyNoneObject)
            result = PySpecialMethods.Next(context, iter);
        else
            result = iter.CallMethod(context, "send", [value]);

        if (result.IsStopIteration)
        {
            // replace sent value with received value by 'yield from'
            stack[-1] = result.Exception.Args.FirstOrDefault(PyNoneObject.None);
            nextIndex = instructionArg;
        }
        else
        {
            stack[-1] = result.PyUnwrap(context);
        }
    }

    private static void InternalMapAdd(PyCallContext context, ref ValueOperandStack stack, int instructionArg)
    {
        var value = stack.Pop();
        var key = stack.Pop();
        var dict = (PyDictObject)stack[-instructionArg];
        dict[key] = value;
    }

    private static void InternalDictUpdate(PyCallContext context, ref ValueOperandStack stack, int instructionArg)
    {
        var map = stack.Pop();
        var dict = (PyDictObject)stack[-instructionArg];
        _ = dict.PyUpdate(context, map).PyUnwrap(context);
    }

    private static void InternalDictMerge(PyCallContext context, ref ValueOperandStack stack, int instructionArg)
    {
        var map = stack.Pop();
        var dictToMerge = PyUtils.ToDict(context, map).PyUnwrap(context);
        var dict = (PyDictObject)stack[-instructionArg];
        foreach (var pair in dictToMerge)
        {
            if (!dict.TryAdd(pair.Key, pair.Value))
                throw context.TypeError(PySR.Runtime_Arguments_MultipleKeywords, pair.Key);
        }
    }

    private static void InternalRaiseVarArgs(PyCallContext context, ref ValueOperandStack stack, ref BytecodeVirtualMachineStates states, int instructionArg)
    {
        if (instructionArg is 0)
        {
            PyCore.Raise(context, ref states, excObj: null, causeObj: null);
        }
        else if (instructionArg is 1)
        {
            var excObj = stack.Pop();
            PyCore.Raise(context, ref states, excObj, causeObj: null);
        }
        else if (instructionArg is 2)
        {
            var causeObj = stack.Pop();
            var excObj = stack.Pop();
            PyCore.Raise(context, ref states, excObj, causeObj);
        }
        else
        {
            throw new UnreachableException();
        }
    }

    private static void InternalCheckEgMatch(PyCallContext context, ref ValueOperandStack stack, ref BytecodeVirtualMachineStates states, int instructionArg)
    {
        var exc = states.CurrentException;
        if (!exc.IsGroup)
            exc = PyBaseExceptionGroupObjectType.CreateExceptionGroup(string.Empty, [exc]);

        var type = stack.Pop();
        var (rest, match) = PyCore.SplitExceptionGroup(context, exc, type);
        states.Exceptions.Pop();
        states.ExceptionHandlers.Peek().PyException = rest;
        states.Exceptions.Push(rest! /* null if rest is None, OpCode._PopExceptionAndJumpIfNull should handle that */);
        stack.Push(match);
    }

    private static void InternalBuildSlice(ref ValueOperandStack stack, int instructionArg)
    {
        if (instructionArg is 2)
        {
            var end = stack.Pop();
            var start = stack.Pop();
            var slice = new PySliceObject(start, end, PyNoneObject.None);
            stack.Push(slice);
        }
        else
        {
            Debug.Assert(instructionArg is 3);
            var step = stack.Pop();
            var end = stack.Pop();
            var start = stack.Pop();
            var slice = new PySliceObject(start, end, step);
            stack.Push(slice);
        }
    }

    private static void InternalMakeFunctionWithPyArgsDef(ref PyInternalFrame frame, ref ValueOperandStack stack)
    {
        var codeObj = (PyCodeObject)stack.Pop();

        PyObject?[] kwDefaults = new PyObject?[codeObj.KwDefaultsCount];
        stack.PopReversedRange(kwDefaults!);
        PyObject[] defaults = new PyObject[codeObj.DefaultsCount];
        stack.PopReversedRange(defaults);
        var def = PyArgsDef.FromCodeObjectAndDefaults(codeObj, kwDefaults, defaults);

        var func = PyCore.MakeFunction(ref frame, codeObj, def);

        stack.Push(func);
    }

    private static void InternalBuildClass(PyCallContext context, ref ValueOperandStack stack, ref BytecodeVirtualMachineStates states, int instructionArg)
    {
        var codeObj = (PyCodeObject)stack.Pop();

        List<PyTypeObject> bases = [];
        LoadArgs(ref stack, states.CacheArgs, instructionArg);
        foreach (var arg in states.CacheArgs)
        {
            if (arg is not PyTypeObject baseType)
                throw new NotSupportedException();

            bases.Add(baseType);
        }

        var type = PyCore.BuildClass(context, codeObj, bases);

        stack.Push(type);
    }

    private static void InternalUnpackSequence(PyCallContext context, ref ValueOperandStack stack, int instructionArg)
    {
        var list = PyUtils.IterableToList(context, stack.Pop()).PyUnwrap(context);
        var span = CollectionsMarshal.AsSpan(list.InternalList);
        if (span.Length > instructionArg)
            throw context.ValueError(PySR.Runtime_Assignment_TooManyToUnpack, instructionArg, span.Length);
        else if (span.Length < instructionArg)
            throw context.ValueError(PySR.Runtime_Assignment_NotEnoughToUnpack, instructionArg, span.Length);
        stack.PushReversedRange(span);
    }

    private static void InternalImportName(PyCallContext context, ref ValueOperandStack stack, ReadOnlySpan<string> names, int instructionArg)
    {
        var fromList = stack.Pop();
        var level = (PyIntObject)stack.Pop();

        if (level.Value > 0)
            throw new NotSupportedException();

        if (names[instructionArg].Contains('.'))
            throw new NotSupportedException();

        if (!context.PyEnvironment.TryLoadModule(context, names[instructionArg], out var module))
            throw context.ModuleNotFoundError(PySR.Runtime_Import_ModuleNotFound, names[instructionArg]);

        stack.Push(module);
    }

    private static T Move<T>([DisallowNull] ref T? value)
    {
        var result = value;
        value = default;
        return result;
    }

    static void LoadArgs(ref ValueOperandStack stack, List<PyObject> args, int count)
    {
        // equals to:
        // args.Clear();
        // for (int i = 0; i < count; i++)
        //     args.Add(Stack.Pop());
        // args.Reverse();

        CollectionsMarshal.SetCount(args, count);
        var argsSpan = CollectionsMarshal.AsSpan(args);
        stack.PopReversedRange(argsSpan);
    }

}
