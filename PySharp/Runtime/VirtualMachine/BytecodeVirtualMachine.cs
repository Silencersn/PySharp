using PySharp.Compilation.Bytecodes;
using PySharp.Compilation.Primitives;
using PySharp.Modules.Builtins;
using PySharp.Modules.String.TemplateLib;
using PySharp.Modules.Typing;
using PySharp.Runtime.Calls;
using PySharp.Utility;
using System.Collections.Frozen;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace PySharp.Runtime.VirtualMachine;

internal static partial class BytecodeVirtualMachine
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

    internal static PyResult Eval(PyCallContext context, ref BytecodeVirtualMachineStates states)
    {
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
        {
            Stack = states.Stack.AsValueOperandStack();
        }
        else
        {
            // Inline frames (Comprehension) share the enclosing non-inline frame's
            // operand stack, because the list/iterator lives on the outer frame's
            // stack space and CreateInline() does not allocate stack space.
            ref var stackFrame = ref frame.FrameType is FrameType.Comprehension
                ? ref context.FrameState.FindOuterNonInlineFrame()
                : ref frame;
            Stack = new ValueOperandStack(stackFrame.Variables.OperandStackSpan);
        }
        Stack.SetSize(states.OperandStackSize);
        Span<PyObject?> locals = [];
        if (frame.Variables.HasLocals)
            locals = frame.Variables.LocalsSpan;

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
                        value = (LoadSpecialMethods)instructionArg switch
                        {
                            LoadSpecialMethods.Enter => new PyWrapperDescriptorObject(
                                Stack[-1].PyType.Slots.Enter ??
                                throw context.TypeError(PySR.Runtime_WithStmt_MissingEnter, Stack[-1].PyType.FullName)),
                            LoadSpecialMethods.Exit => new PyWrapperDescriptorObject(
                                Stack[-1].PyType.Slots.Exit ??
                                throw context.TypeError(PySR.Runtime_WithStmt_MissingExit, Stack[-1].PyType.FullName)),
                            LoadSpecialMethods.AEnter => new PyWrapperDescriptorObject(
                                Stack[-1].PyType.Slots.AEnter ??
                                throw context.TypeError(PySR.Runtime_AsyncWith_MissingAEnter, Stack[-1].PyType.FullName)),
                            LoadSpecialMethods.AExit => new PyWrapperDescriptorObject(
                                Stack[-1].PyType.Slots.AExit ??
                                throw context.TypeError(PySR.Runtime_AsyncWith_MissingAExit, Stack[-1].PyType.FullName)),

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
                        value = locals[instructionArg]
                            ?? throw context.UnboundLocalError(PySR.Runtime_Variable_UnboundLocalError, $"[{instructionArg /* TODO: name */}]");
                        Stack.Push(value);
                        break;

                    case OpCode._LoadDerefFast:
                        value = locals[instructionArg]
                            ?? throw context.UnboundLocalError(PySR.Runtime_Variable_UnboundLocalError, $"[{instructionArg /* TODO: name */}]");
                        value = ((PyCellObject)value).Value
                            ?? throw context.NameError(PySR.Runtime_Variable_UnboundLocalOrFreeError, $"[{instructionArg /* TODO: name */}]");
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
                        locals[instructionArg] = Stack.Pop();
                        break;

                    case OpCode._StoreDerefFast:
                        value = locals[instructionArg]
                            ?? throw context.UnboundLocalError(PySR.Runtime_Variable_UnboundLocalError, $"[{instructionArg /* TODO: name */}]");
                        ((PyCellObject)value).Value = Stack.Pop();
                        break;

                    case OpCode.StoreDeref:
                        value = Stack.Pop();
                        frame.Variables.StoreDeref(names[instructionArg], value);
                        break;

                    case OpCode._StoreNameIncludedNonInlineFrame:
                        value = Stack.Pop();
                        frame.Variables.StoreName(names[instructionArg], value);
                        if (frame.FrameType is FrameType.Comprehension)
                        {
                            context.FrameState.FindOuterNonInlineFrame()
                                .Variables.StoreName(names[instructionArg], value);
                        }
                        break;

                    case OpCode._StoreDerefIncludedNonInlineFrame:
                        value = Stack.Pop();
                        _ = frame.Variables.StoreDeref(names[instructionArg], value).PyUnwrap(context);
                        if (frame.FrameType is FrameType.Comprehension)
                        {
                            context.FrameState.FindOuterNonInlineFrame()
                                .Variables.StoreDeref(names[instructionArg], value).PyUnwrap(context);
                        }
                        break;

                    case OpCode.DeleteName:
                        frame.Variables.DeleteName(names[instructionArg]).PyUnwrap(context);
                        break;

                    case OpCode.DeleteGlobal:
                        frame.Variables.DeleteGlobal(names[instructionArg]).PyUnwrap(context);
                        break;

                    case OpCode.DeleteFast:
                        if (locals[instructionArg] is null)
                            throw context.UnboundLocalError(PySR.Runtime_Variable_UnboundLocalError, $"[{instructionArg /* TODO: name */}]");
                        locals[instructionArg] = null;
                        break;

                    case OpCode._DeleteDerefFast:
                        value = locals[instructionArg]
                            ?? throw context.UnboundLocalError(PySR.Runtime_Variable_UnboundLocalError, $"[{instructionArg /* TODO: name */}]");
                        ((PyCellObject)value).Value = null;
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
                                throw context.TypeError(null /* TODO */);

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
                            states.CachePairs.Add(KeyValuePair.Create(states.CacheArgs[i * 2], states.CacheArgs[i * 2 + 1]));
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
                            Stack[-1] = PyTupleObject.CreateTuple((IEnumerable<PyObject>)(PyListObject)Stack[-1]);
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
                            Debug.Assert(frame.CodeObject is not null);
                            currentIndex++;
                            var flags = frame.CodeObject.Flags;
                            PyTypeObject genType = flags.HasFlag(CodeObjectFlags.AsyncGenerator) ? PyAsyncGeneratorObjectType.Shared
                                : flags.HasFlag(CodeObjectFlags.Coroutine) ? PyCoroutineObjectType.Shared
                                : PyGeneratorObjectType.Shared;
                            intermediateValue = new PyBytecodeGeneratorObject(genType, frame.CallerName, frame, states);
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

                    case OpCode.GetAwaitable:
                        if (PyCoroutineObjectType.Shared.IsInstance(Stack[-1]))
                            break;

                        Stack[-1] = PySpecialMethods.Await(context, Stack[-1]).PyUnwrap(context);
                        break;

                    case OpCode.GetAIter:
                        {
                            var aiter = PySpecialMethods.AIter(context, Stack[-1]).PyUnwrap(context);
                            // CPython 3.14 validates that the result of __aiter__() has __anext__
                            if (aiter.PyType.Slots.ANext is null)
                                throw context.TypeError(PySR.Runtime_AsyncFor_AIterReturnsNoANext, aiter.PyType.FullName);
                            Stack[-1] = aiter;
                        }
                        break;

                    case OpCode.GetANext:
                        {
                            // CPython GET_ANEXT: get __anext__ via slot, call it, wrap in awaitable
                            var aiter = Stack[-1];
                            var slot = aiter.PyType.Slots.ANext ?? throw context.TypeError(PySR.Runtime_AsyncFor_MissingANext, aiter.PyType.FullName);
                            var nextIter = slot(context, aiter).PyUnwrap(context);
                            if (!PyCoroutineObjectType.Shared.IsInstance(nextIter))
                                nextIter = PySpecialMethods.Await(context, nextIter).PyUnwrap(context);
                            Stack.Push(nextIter);
                        }
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

                    case OpCode.BuildInterpolation:
                        {
                            var formatSpec = ((instructionArg & 1) is not 0) ? (PyStrObject)Stack.Pop() : PyStrObject.Empty;
                            var expression = (PyStrObject)Stack.Pop();
                            value = Stack.Pop();

                            var conversion = (instructionArg >> 2) switch
                            {
                                0 => null,
                                1 => PyStrObject.FromString("s"),
                                2 => PyStrObject.FromString("r"),
                                3 => PyStrObject.FromString("a"),
                                _ => throw new UnreachableException()
                            };

                            Stack.Push(new PyInterpolationObject(value, expression, conversion, formatSpec));
                        }
                        break;

                    case OpCode.BuildTemplate:
                        {
                            var interpolations = (PyTupleObject)Stack.Pop();
                            var strings = (PyTupleObject)Stack[-1];
                            Stack[-1] = new PyTemplateObject(strings, interpolations);
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

                    case OpCode.SetupAnnotations:
                        InternalSetupAnnotations(ref frame);
                        break;

                    case OpCode._BuildClass:
                        InternalBuildClass(context, ref Stack, ref states, instructionArg);
                        break;

                    case OpCode.MakeCell:
                        frame.Variables.StoreLocal(names[instructionArg], PyCellObject.CreateEmpty());
                        break;

                    case OpCode._MakeCellFast:
                        {
                            value = PyCellObject.CreateCell(frame.Variables.LocalsSpan[instructionArg]);
                            locals[instructionArg] = value;
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
                        if (currentHandler.ReturnValue is not null)
                        {
                            returnValue = Move(ref currentHandler.ReturnValue);

                            // When returning through a finally block, the stack
                            // may still hold items that were on the stack at
                            // _SetupFinally (e.g. a for-loop iterator). Pop them.
                            // targetDepth = StackDepth - 1 because ReturnValue
                            // already popped the return value off the stack.
                            var targetDepth = currentHandler.StackDepth - 1;
                            if (targetDepth < 0)
                                targetDepth = 0;
                            var popCount = Stack.Count - targetDepth;
                            if (popCount > 0)
                                Stack.PopN(popCount);
                        }
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

                    case OpCode._MakeTypeVar:
                        {
                            var typeVarName = (PyStrObject)Stack.Pop();
                            Stack.Push(new PyTypeVarObject(typeVarName.Value));
                        }
                        break;

                    case OpCode._MakeTypeAlias:
                        {
                            var func = (PyFunctionObject)Stack[-1];
                            value = new PyTypeAliasTypeObject(names[instructionArg], func);
                            Stack[-1] = value;
                        }
                        break;

                    case OpCode._SetFunctionTypeParams:
                        {
                            var typeParams = Stack.Pop();
                            var func = Stack[-1];
                            Debug.Assert(func is PyFunctionObject);
                            func.PyAttributes[PySpecialNames.TypeParams] = typeParams;
                        }
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

                    // If we're returning from an except handler (State_Except),
                    // the exception was already handled. Clear it so _ExitFinally
                    // doesn't re-raise it.
                    if (handler.State is ExceptionHandler.State_Except)
                        handler.PyException = null;

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
            var popCount = Stack.Count - currentHandler.StackDepth;
            if (popCount > 0)
                Stack.PopN(popCount);

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
            frame.Dispose(context);
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

    [return: NotNullIfNotNull(nameof(value))]
    private static T? Move<T>(ref T? value)
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
