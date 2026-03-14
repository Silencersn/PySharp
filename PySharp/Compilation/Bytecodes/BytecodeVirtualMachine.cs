using PySharp.Compilation.Primitives;
using PySharp.Modules.Builtins;
using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.Calls.Extensions;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

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
        ValueOperandStack Stack;
        PyResult evalResult;

        #region Eval Body

        ref int currentIndex = ref frame.InstructionIndex;
        var instructions = states.Bytecode.Instructions.AsSpan();
        var consts = states.Bytecode.Consts.AsSpan();
        var names = states.Bytecode.Names.AsSpan();
        var length = instructions.Length;
        if (states.Stack is not null)
            Stack = states.Stack.AsValueOperandStack();
        else
            Stack = new ValueOperandStack(frame.Variables.OperandStackSpan);

        // cache, clear before using
        PyObject value, left, right;
        bool boolValue;
        PyResult result;
        PyObject? returnValue = null, intermediateValue = null;

        int instructionArg = 0;

        while (currentIndex < length)
        {
            var instruction = instructions[currentIndex];
            var nextIndex = currentIndex + 1;

            try
            {
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
                            var obj = Stack.Pop();
                            value = Stack.Pop();
                            PyOperators.SetAttr(context, obj, names[instructionArg], value).PyUnwrap(context);
                        }
                        break;

                    case OpCode.DeleteAttr:
                        {
                            var obj = Stack.Pop();
                            PyOperators.DelAttr(context, obj, names[instructionArg]).PyUnwrap(context);
                        }
                        break;

                    case OpCode.BinarySubscr:
                        {
                            var key = Stack.Pop();
                            var container = Stack.Pop();
                            value = PySpecialMethods.GetItem(context, container, key).PyUnwrap(context);
                            Stack.Push(value);
                        }
                        break;

                    case OpCode.StoreSubscr:
                        {
                            var key = Stack.Pop();
                            var container = Stack.Pop();
                            value = Stack.Pop();
                            _ = PySpecialMethods.SetItem(context, container, key, value).PyUnwrap(context);
                        }
                        break;

                    case OpCode.DeleteSubscr:
                        {
                            var key = Stack.Pop();
                            var container = Stack.Pop();
                            _ = PySpecialMethods.DelItem(context, container, key).PyUnwrap(context);
                        }
                        break;

                    case OpCode.LoadMethod:
                        {
                            value = Stack[-1];
                            var method = PyCore.GetAttrOrMethod(context, value, names[instructionArg], out var isMethod).PyUnwrap(context);
                            Stack[-1] = method;
                            Stack.Push(isMethod ? value : null! /* this null will be handled by OpCode.Call or OpCode.CallKw */);
                        }
                        break;

                    case OpCode.Call:
                        {
                            var isNull = instructionArg > 0 && Stack[-instructionArg] is null;
                            if (isNull)
                                instructionArg--;

                            LoadArgs(ref Stack, states.CacheArgs, instructionArg);
                            if (isNull)
                                Stack.Pop();

                            var callable = Stack.Pop();
                            value = callable.Call(context, states.CacheArgs).PyUnwrap(context);
                            Stack.Push(value);
                        }
                        break;

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

                            var callable = Stack.Pop();
                            value = callable.Call(context, states.CacheArgs, states.CacheKwargs).PyUnwrap(context);
                            Stack.Push(value);
                        }
                        break;

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

                            Stack[-1] = Stack[-1].Call(context, pyargs, states.CacheKwargs).PyUnwrap(context);
                        }
                        break;

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
                        }
                        break;

                    case OpCode._ExitInlineFrame:
                        context.FrameState.ExitInternalFrame(context, dispose: true);
                        frame = ref context.CurrentInternalFrame;
                        break;

                    case OpCode.ListAppend:
                        {
                            value = Stack.Pop();
                            var list = (PyListObject)Stack[-instructionArg];
                            list.Add(value);
                        }
                        break;

                    case OpCode.ListExtend:
                        {
                            value = Stack.Pop();
                            var list = (PyListObject)Stack[-instructionArg];
                            _ = list.PyExtend(context, value).PyUnwrap(context);
                        }
                        break;

                    case OpCode._ListToTuple:
                        {
                            var list = (PyListObject)Stack[-1];
                            Stack[-1] = PyTupleObject.CreateTuple(list);
                        }
                        break;

                    case OpCode._ListToSet:
                        {
                            var list = (PyListObject)Stack[-1];
                            Stack[-1] = PySetObject.CreateSet(list);
                        }
                        break;

                    case OpCode.SetAdd:
                        {
                            value = Stack.Pop();
                            var set = (PySetObject)Stack[-instructionArg];
                            set.Add(value);
                        }
                        break;

                    case OpCode.MapAdd:
                        {
                            value = Stack.Pop();
                            var key = Stack.Pop();
                            var dict = (PyDictObject)Stack[-instructionArg];
                            dict[key] = value;
                        }
                        break;

                    case OpCode.DictUpdate:
                        {
                            var map = Stack.Pop();
                            var dict = (PyDictObject)Stack[-instructionArg];
                            _ = dict.PyUpdate(context, map).PyUnwrap(context);
                        }
                        break;

                    case OpCode.DictMerge:
                        {
                            var map = Stack.Pop();
                            var dictToMerge = PyUtils.ToDict(context, map).PyUnwrap(context);
                            var dict = (PyDictObject)Stack[-instructionArg];
                            foreach (var pair in dictToMerge)
                            {
                                if (!dict.TryAdd(pair.Key, pair.Value))
                                    throw context.TypeError(PySR.Runtime_Arguments_MultipleKeywords, pair.Key);
                            }
                        }
                        break;

                    case OpCode.UnpackSequence:
                        {
                            var list = PyUtils.IterableToList(context, Stack.Pop()).PyUnwrap(context);
                            var span = CollectionsMarshal.AsSpan(list.InternalList);
                            if (span.Length > instructionArg)
                                throw context.ValueError(PySR.Runtime_Assignment_TooManyToUnpack, instructionArg, span.Length);
                            else if (span.Length < instructionArg)
                                throw context.ValueError(PySR.Runtime_Assignment_NotEnoughToUnpack, instructionArg, span.Length);
                            Stack.PushReversedRange(span);
                        }
                        break;

                    case OpCode.UnpackEx:
                        {
                            var postCount = instructionArg & ushort.MaxValue;
                            var preCount = (instructionArg >> 16) & ushort.MaxValue;
                            var list = PyUtils.IterableToList(context, Stack.Pop()).PyUnwrap(context);
                            var span = CollectionsMarshal.AsSpan(list.InternalList);
                            if (span.Length < preCount + postCount)
                                throw context.ValueError(PySR.Runtime_Assignment_NotEnoughToUnpackStarred, preCount + postCount, span.Length);
                            Stack.PushReversedRange(span[^postCount..]);
                            Stack.Push(PyListObject.CreateList(span[preCount..^postCount]));
                            Stack.PushReversedRange(span[..preCount]);
                        }
                        break;

                    case OpCode.ReturnValue:
                        returnValue = Stack.Pop();
                        break;

                    case OpCode.ReturnGenerator:
                        {
                            currentIndex++;
                            var generator = new PyBytecodeGeneratorObject(frame.CallerName, frame, states);
                            intermediateValue = generator;
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
                        {
                            PyObject iter;
                            if (states.ExceptionToRaise is not null)
                            {
                                // throw or close

                                iter = Stack[-1];

                                if (PyGeneratorExitObjectType.Shared.IsInstance(states.ExceptionToRaise))
                                {
                                    // close sub generator
                                    var close = PyOperators.GetAttr(context, iter, "close");
                                    if (!close.IsAttributeError)
                                        _ = close.PyUnwrap(context).Call(context).PyUnwrap(context);

                                    // close self
                                    goto case OpCode._CheckExcToRaise;
                                }
                                else
                                {
                                    var throwMethod = PyOperators.GetAttr(context, iter, "throw");
                                    if (!throwMethod.IsAttributeError)
                                    {
                                        var exc = Move(ref states.ExceptionToRaise);
                                        value = throwMethod.PyUnwrap(context).Call(context, [exc]).PyUnwrap(context);
                                        Stack.Push(value);
                                    }
                                    else
                                    {
                                        // throw at self
                                        goto case OpCode._CheckExcToRaise;
                                    }
                                    break;
                                }
                            }

                            iter = Stack[-2];
                            value = Stack[-1];
                            if (value is PyNoneObject)
                                result = PySpecialMethods.Next(context, iter);
                            else
                                result = iter.CallMethod(context, "send", [value]);

                            if (result.IsStopIteration)
                            {
                                // replace sent value with received value by 'yield from'
                                Stack[-1] = result.Exception.Args.FirstOrDefault(PyNoneObject.None);
                                nextIndex = instructionArg;
                            }
                            else
                            {
                                Stack[-1] = result.PyUnwrap(context);
                            }
                        }
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
                        {
                            var spec = Stack.Pop();
                            Stack[-1] = PySpecialMethods.Format(context, Stack[-1], spec).PyUnwrap(context);
                        }
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
                        {
                            var fromList = Stack.Pop();
                            var level = (PyIntObject)Stack.Pop();

                            if (level.Value > 0)
                                throw new NotSupportedException();

                            if (names[instructionArg].Contains('.'))
                                throw new NotSupportedException();

                            if (!context.PyEnvironment.TryLoadModule(context, names[instructionArg], out var module))
                                throw context.ModuleNotFoundError(PySR.Runtime_Import_ModuleNotFound, names[instructionArg]);

                            Stack.Push(module);
                        }
                        break;

                    case OpCode.ImportFrom:
                        value = PyOperators.GetAttr(context, Stack[-1], names[instructionArg]).PyUnwrap(context);
                        Stack.Push(value);
                        break;

                    case OpCode._ImportAllFrom:
                        PyCore.ImportAllFrom(context, ref frame, (PyModuleObject)Stack.Pop());
                        break;

                    case OpCode.BuildSlice:
                        {
                            if (instructionArg is 2)
                            {
                                var end = Stack.Pop();
                                var start = Stack.Pop();
                                var slice = new PySliceObject(start, end, PyNoneObject.None);
                                Stack.Push(slice);
                            }
                            else
                            {
                                Debug.Assert(instructionArg is 3);
                                var step = Stack.Pop();
                                var end = Stack.Pop();
                                var start = Stack.Pop();
                                var slice = new PySliceObject(start, end, step);
                                Stack.Push(slice);
                            }
                        }
                        break;

                    case OpCode._MakeFunctionWithPyArgsDef:
                        {
                            var codeObj = (PyCodeObject)Stack.Pop();

                            PyObject?[] kwDefaults = new PyObject?[codeObj.KwDefaultsCount];
                            Stack.PopReversedRange(kwDefaults!);
                            PyObject[] defaults = new PyObject[codeObj.DefaultsCount];
                            Stack.PopReversedRange(defaults);
                            var def = PyArgsDef.FromCodeObjectAndDefaults(codeObj, kwDefaults, defaults);

                            var func = PyCore.MakeFunction(ref frame, codeObj, def);

                            Stack.Push(func);
                        }
                        break;

                    case OpCode._MakeGeneratorExp:
                        {
                            var codeObj = (PyCodeObject)Stack.Pop();
                            Debug.Assert(codeObj.Bytecode is not null);

                            var inlineFrame = frame.CreateInlineFrame(FrameType.Comprehension);
                            inlineFrame.CodeObject = codeObj;
                            var vmStates = new BytecodeVirtualMachineStates(context, codeObj.Bytecode);

                            var generator = new PyBytecodeGeneratorObject(codeObj.Name, inlineFrame, vmStates);

                            Stack.Push(generator);
                        }
                        break;

                    case OpCode._BuildClass:
                        {
                            var codeObj = (PyCodeObject)Stack.Pop();

                            List<PyTypeObject> bases = [];
                            LoadArgs(ref Stack, states.CacheArgs, instructionArg);
                            foreach (var arg in states.CacheArgs)
                            {
                                if (arg is not PyTypeObject baseType)
                                    throw new NotSupportedException();

                                bases.Add(baseType);
                            }

                            var type = PyCore.BuildClass(context, codeObj, bases);

                            Stack.Push(type);
                        }
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
                            var content = frame.Variables.LocalsSpan[instructionArg];
                            frame.Variables.StoreFast(instructionArg, PyCellObject.CreateCell(content));
                        }
                        break;

                    case OpCode.RaiseVarArgs:
                        if (instructionArg is 0)
                        {
                            PyCore.Raise(context, ref states, excObj: null, causeObj: null);
                        }
                        else if (instructionArg is 1)
                        {
                            var excObj = Stack.Pop();
                            PyCore.Raise(context, ref states, excObj, causeObj: null);
                        }
                        else if (instructionArg is 2)
                        {
                            var causeObj = Stack.Pop();
                            var excObj = Stack.Pop();
                            PyCore.Raise(context, ref states, excObj, causeObj);
                        }
                        else
                        {
                            throw new UnreachableException();
                        }
                        break;

                    case OpCode.CheckExcMatch:
                        var condition = PyCore.MakeExceptCondition(context, Stack[-1]);
                        Stack[-1] = PyBoolObject.FromBoolean(condition(states.CurrentException));
                        break;

                    case OpCode.CheckEgMatch:
                        {
                            var exc = states.CurrentException;
                            if (!exc.IsGroup)
                                exc = PyBaseExceptionGroupObjectType.CreateExceptionGroup(string.Empty, [exc]);

                            var type = Stack.Pop();
                            var (rest, match) = PyCore.SplitExceptionGroup(context, exc, type);
                            states.Exceptions.Pop();
                            states.ExceptionHandlers.Peek().PyException = rest;
                            states.Exceptions.Push(rest! /* null if rest is None, OpCode._PopExceptionAndJumpIfNull should handle that */);
                            Stack.Push(match);
                        }
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
                        {
                            var keys = (PyTupleObject)Stack.Peek();
                            var subject = Stack[-2];
                            var array = new PyObject[keys.Count];
                            var matched = true;
                            for (int i = 0; matched && i < array.Length; i++)
                            {
                                var key = keys[i];
                                result = PySpecialMethods.GetItem(context, subject, key);
                                if (result.IsError && PyKeyErrorObjectType.Shared.IsInstance(result.Exception))
                                {
                                    matched = false;
                                    break;
                                }
                                array[i] = result.PyUnwrap(context);
                            }
                            Stack.Push(matched ? PyTupleObject.CreateProxy(array) : PyNoneObject.None);
                        }
                        break;

                    case OpCode.MatchClass:
                        {
                            var keys = (PyTupleObject)Stack.Pop();
                            value = Stack.Pop();
                            if (value is not PyTypeObject cls)
                                throw context.TypeError(PySR.Runtime_MatchStmt_CallNonClass);
                            var subject = Stack.Pop();

                            if (!cls.IsInstance(subject))
                            {
                                Stack.Push(PyNoneObject.None);
                                break;
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
                                        Stack.Push(PyNoneObject.None);
                                        goto match_class_break;
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
                                    Stack.Push(PyNoneObject.None);
                                    goto match_class_break;
                                }

                                values[instructionArg + i] = attr.PyUnwrap(context);
                            }

                            Stack.Push(PyTupleObject.CreateProxy(values));

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
                    match_class_break:
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
            }
            catch (PyRuntimeException e)
            {
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

                // TODO:
                //context.EnsureFrameState(frame);

                states.Exceptions.Push(e.PyException);
            }

            if (instruction.OpCode is not OpCode.ExtendedArg)
                instructionArg = 0;

            currentIndex = nextIndex;

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
        }

        return evalResult;
    }


    private static T Move<T>([DisallowNull] ref T? value)
    {
        var result = value;
        value = default;
        return result;
    }
}
