using PySharp.Compilation.AstNodes;
using PySharp.Compilation.CodeAnalysis;
using PySharp.Modules.Builtins;
using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.Calls.Extensions;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;

namespace PySharp.Compilation.Bytecodes;

internal sealed class BytecodeVirtualMachine : ICodeMetaInfoProvider
{
    // States
    internal int InstructionIndex;
    internal PyExceptionObject? ExceptionToRaise;
    internal bool RunToEnd { get; private set; }
    private Stack<ExceptionHandler> ExceptionHandlers => field ??= [];
    private OperandStack Stack { get; } = new();

    private PyCallContext Context { get; }
    private Bytecode Bytecode { get; }

    CodeMetaInfo? ICodeMetaInfoProvider.MetaInfo
    {
        get
        {
            var infos = Bytecode.MetaInfos;
            var index = InstructionIndex;

            // TODO: do not O(n)
            foreach (var pair in infos)
            {
                if (pair.Key <= index)
                    return pair.Value;
            }

            return null;
        }
    }

    internal BytecodeVirtualMachine(PyCallContext context, Bytecode bytecode)
    {
        Context = context;
        Bytecode = bytecode;
    }

    internal PyResult Eval()
    {
        var frame = Context.CurrentFrame;
        using var withMetaInfo = new MetaInfoProviderSetter(frame, this);
        return Eval(Context, frame);
    }

    private class ExceptionHandler
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

    internal void SetYieldReceivedValue(PyObject value)
    {
        Stack.Push(value);
    }

    internal PyResult Eval(PyCallContext context, PyFrame frame)
    {
        ref int currentIndex = ref InstructionIndex;
        var instructions = Bytecode.Instructions.AsSpan();
        var consts = Bytecode.Consts.AsSpan();
        var names = Bytecode.Names.AsSpan();
        var length = instructions.Length;

        // cache, clear before using
        PyObject value, left, right;
        bool boolValue;
        List<PyObject> args = [];
        OrderedDictionary<string, PyObject> kwargs = [];
        PyResult result;
        PyObject? returnValue = null, intermediateValue = null;
        List<KeyValuePair<PyObject, PyObject>> pairs = [];
        StringBuilder builder = new();

        while (currentIndex < length)
        {
            var instruction = instructions[currentIndex];
            var nextIndex = currentIndex + 1;

            try
            {
                #region Eval OpCode

                switch (instruction.OpCode)
                {
                    case OpCode.NoOperation:
                        break;

                    case OpCode.LoadConst:
                        Stack.Push(consts[instruction.Arg]);
                        break;

                    case OpCode.LoadSpecial:
                        value = names[instruction.Arg] switch
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
                            var exc = frame.CurrentException;
                            Stack.PushRange(exc.PyType, exc, PyTraceback.CaptureCurrentFrame(context));
                        }
                        break;

                    case OpCode._LoadHitExcept:
                        Stack.Push(PyBoolObject.FromBoolean(ExceptionHandlers.Peek().HitExcept));
                        break;

                    case OpCode.LoadName:
                        value = frame.Variables.LoadName(names[instruction.Arg]).PyUnwrap(context);
                        Stack.Push(value);
                        break;

                    case OpCode.LoadGlobal:
                        value = frame.Variables.LoadGlobal(names[instruction.Arg]).PyUnwrap(context);
                        Stack.Push(value);
                        break;

                    case OpCode.LoadFast:
                        value = frame.Variables.LoadFast(instruction.Arg).PyUnwrap(context);
                        Stack.Push(value);
                        break;

                    case OpCode._LoadDerefFast:
                        value = frame.Variables.LoadDerefFast(instruction.Arg).PyUnwrap(context);
                        Stack.Push(value);
                        break;

                    case OpCode.LoadDeref:
                        value = frame.Variables.LoadDeref(names[instruction.Arg]).PyUnwrap(context);
                        Stack.Push(value);
                        break;

                    case OpCode.StoreName:
                        value = Stack.Pop();
                        frame.Variables.StoreName(names[instruction.Arg], value);
                        break;

                    case OpCode.StoreGlobal:
                        value = Stack.Pop();
                        frame.Variables.StoreGlobal(names[instruction.Arg], value);
                        break;

                    case OpCode.StoreFast:
                        value = Stack.Pop();
                        frame.Variables.StoreFast(instruction.Arg, value);
                        break;

                    case OpCode._StoreDerefFast:
                        value = Stack.Pop();
                        _ = frame.Variables.StoreDerefFast(instruction.Arg, value).PyUnwrap(context);
                        break;

                    case OpCode.StoreDeref:
                        value = Stack.Pop();
                        frame.Variables.StoreDeref(names[instruction.Arg], value);
                        break;

                    case OpCode._StoreNameIncludedNonInlineFrame:
                        value = Stack.Pop();
                        frame.Variables.StoreName(names[instruction.Arg], value);
                        frame._outerNonInlineFrame?.Variables.StoreName(names[instruction.Arg], value);
                        break;

                    case OpCode._StoreDerefIncludedNonInlineFrame:
                        value = Stack.Pop();
                        _ = frame.Variables.StoreDeref(names[instruction.Arg], value).PyUnwrap(context);
                        _ = frame._outerNonInlineFrame?.Variables.StoreDeref(names[instruction.Arg], value).PyUnwrap(context);
                        break;

                    case OpCode.DeleteName:
                        frame.Variables.DeleteName(names[instruction.Arg]).PyUnwrap(context);
                        break;

                    case OpCode.DeleteGlobal:
                        frame.Variables.DeleteGlobal(names[instruction.Arg]).PyUnwrap(context);
                        break;

                    case OpCode.DeleteFast:
                        frame.Variables.DeleteFast(instruction.Arg).PyUnwrap(context);
                        break;

                    case OpCode._DeleteDerefFast:
                        frame.Variables.DeleteDerefFast(instruction.Arg).PyUnwrap(context);
                        break;

                    case OpCode.DeleteDeref:
                        frame.Variables.DeleteDeref(names[instruction.Arg]);
                        break;

                    case OpCode.LoadAttr:
                        {
                            Stack[-1] = PyOperators.GetAttr(context, Stack[-1], names[instruction.Arg]).PyUnwrap(context);
                        }
                        break;

                    case OpCode.StoreAttr:
                        {
                            var obj = Stack.Pop();
                            value = Stack.Pop();
                            PyOperators.SetAttr(context, obj, names[instruction.Arg], value).PyUnwrap(context);
                        }
                        break;

                    case OpCode.DeleteAttr:
                        {
                            var obj = Stack.Pop();
                            PyOperators.DelAttr(context, obj, names[instruction.Arg]).PyUnwrap(context);
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

                    case OpCode.Call:
                        {
                            LoadArgs(args, instruction.Arg);
                            var callable = Stack.Pop();
                            value = callable.Call(context, args).PyUnwrap(context);
                            Stack.Push(value);
                        }
                        break;

                    case OpCode.CallKw:
                        {
                            var tuple = (PyTupleObject)Stack.Pop();
                            kwargs.Clear();

                            LoadArgs(args, tuple._array.Length);

                            for (int i = 0; i < tuple._array.Length; i++)
                            {
                                var str = (PyStrObject)tuple._array[i];
                                kwargs.Add(str.Value, args[i]);
                            }

                            LoadArgs(args, instruction.Arg - kwargs.Count);

                            var callable = Stack.Pop();
                            value = callable.Call(context, args, kwargs).PyUnwrap(context);
                            Stack.Push(value);
                        }
                        break;

                    case OpCode.CallFunctionEx:
                        {
                            var dict = (PyDictObject)Stack.Pop();
                            var pyargs = (PyListObject)Stack.Pop();
                            kwargs.Clear();

                            foreach (var pair in dict._dict)
                            {
                                if (pair.Key is not PyStrObject str)
                                    throw context.TypeError(PySR.Runtime_Keyword_KeywordsMustBeStrings);
                                kwargs.Add(str.Value, pair.Value);
                            }

                            Stack[-1] = Stack[-1].Call(context, pyargs._list, kwargs).PyUnwrap(context);
                        }
                        break;

                    case OpCode.PopTop:
                        Stack.Pop();
                        break;

                    case OpCode.Copy:
                        value = Stack[-instruction.Arg];
                        Stack.Push(value);
                        break;

                    case OpCode.Swap:
                        (Stack[-1], Stack[-instruction.Arg]) = (Stack[-instruction.Arg], Stack[-1]);
                        break;

                    case OpCode.ToBool:
                        Stack[-1] = PySpecialMethods.Bool(context, Stack[-1]).PyUnwrap(context);
                        break;

                    case OpCode.Jump:
                        nextIndex = instruction.Arg;
                        break;

                    case OpCode.PopJumpIfFalse:
                        {
                            value = Stack.Pop();
                            boolValue = ((PyBoolObject)value).BoolValue;
                            if (!boolValue)
                                nextIndex = instruction.Arg;
                        }
                        break;

                    case OpCode.PopJumpIfTrue:
                        {
                            value = Stack.Pop();
                            boolValue = ((PyBoolObject)value).BoolValue;
                            if (boolValue)
                                nextIndex = instruction.Arg;
                        }
                        break;

                    case OpCode.PopJumpIfNone:
                        {
                            value = Stack.Pop();
                            if (value is PyNoneObject)
                                nextIndex = instruction.Arg;
                        }
                        break;

                    case OpCode.GetIter:
                        Stack[-1] = PySpecialMethods.Iter(context, Stack[-1]).PyUnwrap(context);
                        break;

                    case OpCode.ForIter:
                        result = PySpecialMethods.Next(context, Stack.Peek());
                        if (result.IsStopIteration)
                            nextIndex = instruction.Arg;
                        else
                            Stack.Push(result.PyUnwrap(context));
                        break;

                    case OpCode.PopIter:
                        Stack.Pop();
                        break;

                    case OpCode.BinaryOp:
                        right = Stack.Pop();
                        left = Stack.Pop();
                        value = BinOpNode.EvalOperator(context, (OperatorType)instruction.Arg, left, right).PyUnwrap(context);
                        Stack.Push(value);
                        break;

                    case OpCode._UnaryOp:
                        value = Stack.Pop();
                        value = UnaryOpNode.EvalOperator(context, (UnaryOpType)instruction.Arg, value).PyUnwrap(context);
                        Stack.Push(value);
                        break;

                    case OpCode.UnaryNot:
                        boolValue = ((PyBoolObject)Stack.Peek()).BoolValue;
                        Stack[-1] = PyBoolObject.FromBoolean(!boolValue);
                        break;

                    case OpCode.CompareOp:
                        right = Stack.Pop();
                        left = Stack.Pop();
                        value = CompareNode.EvalOperator(context, (CmpopType)instruction.Arg, left, right).PyUnwrap(context);
                        Stack.Push(value);
                        break;

                    case OpCode.IsOp:
                        right = Stack.Pop();
                        left = Stack.Pop();
                        if (instruction.Arg is 0)
                            value = PyOperators.Is(left, right);
                        else
                            value = PyOperators.IsNot(left, right);
                        Stack.Push(value);
                        break;

                    case OpCode.ContainsOp:
                        right = Stack.Pop();
                        left = Stack.Pop();
                        if (instruction.Arg is 0)
                            value = PyOperators.In(context, left, right).PyUnwrap(context);
                        else
                            value = PyOperators.NotIn(context, left, right).PyUnwrap(context);
                        Stack.Push(value);
                        break;

                    case OpCode._AugAssignOp:
                        right = Stack.Pop();
                        left = Stack.Pop();
                        value = AugAssignNode.EvalInplaceOperator(context, (OperatorType)instruction.Arg, left, right).PyUnwrap(context);
                        Stack.Push(value);
                        break;

                    case OpCode.PushNull:
                        // suppressed null warnings
                        // because null is rarely used and
                        // we assume that all nulls can be handled correctly.
                        Stack.Push(null!);
                        break;

                    case OpCode.BuildList:
                        LoadArgs(args, instruction.Arg);
                        Stack.Push(PyListObject.CreateList(args));
                        break;

                    case OpCode.BuildTuple:
                        LoadArgs(args, instruction.Arg);
                        Stack.Push(PyTupleObject.CreateTuple(args));
                        break;

                    case OpCode.BuildSet:
                        LoadArgs(args, instruction.Arg);
                        Stack.Push(PySetObject.CreateSet(args));
                        break;

                    case OpCode.BuildMap:
                        LoadArgs(args, instruction.Arg * 2);
                        pairs.Clear();
                        for (int i = 0; i < instruction.Arg; i++)
                        {
                            pairs.Add(KeyValuePair.Create(args[i * 2], args[i * 2 + 1]));
                        }
                        Stack.Push(PyDictObject.CreateDict(pairs));
                        break;

                    case OpCode._EnterInlineFrame:
                        frame = frame.CreateInlineFrame(FrameType.Comprehension);
                        context.FrameState.EnterFrame(frame);
                        break;

                    case OpCode._ExitInlineFrame:
                        context.FrameState.ExitFrame();
                        frame = context.CurrentFrame;
                        break;

                    case OpCode.ListAppend:
                        {
                            value = Stack.Pop();
                            var list = (PyListObject)Stack[-instruction.Arg];
                            list._list.Add(value);
                        }
                        break;

                    case OpCode.ListExtend:
                        {
                            value = Stack.Pop();
                            var list = (PyListObject)Stack[-instruction.Arg];
                            _ = list.PyExtend(context, value).PyUnwrap(context);
                        }
                        break;

                    case OpCode._ListToTuple:
                        {
                            var list = (PyListObject)Stack[-1];
                            Stack[-1] = PyTupleObject.CreateTuple(list._list);
                        }
                        break;

                    case OpCode._ListToSet:
                        {
                            var list = (PyListObject)Stack[-1];
                            Stack[-1] = PySetObject.CreateSet(list._list);
                        }
                        break;

                    case OpCode.SetAdd:
                        {
                            value = Stack.Pop();
                            var set = (PySetObject)Stack[-instruction.Arg];
                            set._set.Add(value);
                        }
                        break;

                    case OpCode.MapAdd:
                        {
                            value = Stack.Pop();
                            var key = Stack.Pop();
                            var dict = (PyDictObject)Stack[-instruction.Arg];
                            dict._dict[key] = value;
                        }
                        break;

                    case OpCode.DictUpdate:
                        {
                            var map = Stack.Pop();
                            var dict = (PyDictObject)Stack[-instruction.Arg];
                            _ = dict.PyUpdate(context, map).PyUnwrap(context);
                        }
                        break;

                    case OpCode.DictMerge:
                        {
                            var map = Stack.Pop();
                            var dictToMerge = PyUtils.ToDict(context, map).PyUnwrap(context);
                            var dict = (PyDictObject)Stack[-instruction.Arg];
                            foreach (var pair in dictToMerge._dict)
                            {
                                if (!dict._dict.TryAdd(pair.Key, pair.Value))
                                    throw context.TypeError(PySR.Runtime_Arguments_MultipleKeywords, pair.Key);
                            }
                        }
                        break;

                    case OpCode.UnpackSequence:
                        {
                            var list = PyUtils.IterableToList(context, Stack.Pop()).PyUnwrap(context);
                            var span = CollectionsMarshal.AsSpan(list._list);
                            if (span.Length > instruction.Arg)
                                throw context.ValueError(PySR.Runtime_Assignment_TooManyToUnpack, instruction.Arg, span.Length);
                            else if (span.Length < instruction.Arg)
                                throw context.ValueError(PySR.Runtime_Assignment_NotEnoughToUnpack, instruction.Arg, span.Length);
                            Stack.PushReversedRange(span);
                        }
                        break;

                    case OpCode.UnpackEx:
                        {
                            var postCount = instruction.Arg & ushort.MaxValue;
                            var preCount = (instruction.Arg >> 16) & ushort.MaxValue;
                            var list = PyUtils.IterableToList(context, Stack.Pop()).PyUnwrap(context);
                            var span = CollectionsMarshal.AsSpan(list._list);
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
                            var generator = new PyBytecodeGeneratorObject(frame.CallerName, frame, this);
                            intermediateValue = generator;
                            InstructionIndex = currentIndex + 1;
                        }
                        break;

                    case OpCode.YieldValue:
                        {
                            intermediateValue = Stack.Pop();
                            InstructionIndex = currentIndex + 1;
                        }
                        break;

                    case OpCode.GetYieldFromIter:
                        if (!PyGeneratorObjectType.Shared.IsInstance(Stack[-1]))
                            goto case OpCode.GetIter;
                        break;

                    case OpCode.Send:
                        {
                            PyObject iter;
                            if (ExceptionToRaise is not null)
                            {
                                // throw or close

                                iter = Stack[-1];

                                if (PyGeneratorExitObjectType.Shared.IsInstance(ExceptionToRaise))
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
                                        var exc = Move(ref ExceptionToRaise);
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
                                nextIndex = instruction.Arg;
                            }
                            else
                            {
                                Stack[-1] = result.PyUnwrap(context);
                            }
                        }
                        break;

                    case OpCode._CheckExcToRaise:
                        if (ExceptionToRaise is not null)
                        {
                            var exc = Move(ref ExceptionToRaise);
                            throw new PyRuntimeException(exc);
                        }
                        break;

                    case OpCode.ConvertValue:
                        value = Stack.Pop();
                        if (instruction.Arg is 1)
                            value = PySpecialMethods.Str(context, value).PyUnwrap(context);
                        else if (instruction.Arg is 2)
                            value = PySpecialMethods.Repr(context, value).PyUnwrap(context);
                        else if (instruction.Arg is 3)
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
                        if (instruction.Arg is 0)
                        {
                            Stack.Push(PyStrObject.Empty);
                        }
                        else if (instruction.Arg is 1)
                        {
                            Debug.Assert(Stack.Peek() is PyStrObject);
                        }
                        else
                        {
                            builder.Clear();
                            LoadArgs(args, instruction.Arg);
                            foreach (var arg in args)
                            {
                                Debug.Assert(arg is PyStrObject);
                                builder.Append(((PyStrObject)arg).Value);
                            }
                            Stack.Push(PyStrObject.FromString(builder.ToString()));
                        }
                        break;

                    case OpCode.ImportName:
                        {
                            var fromList = Stack.Pop();
                            var level = (PyIntObject)Stack.Pop();

                            if (level.Value > 0)
                                throw new NotSupportedException();

                            if (names[instruction.Arg].Contains('.'))
                                throw new NotSupportedException();

                            if (!context.PyEnvironment.TryLoadModule(context, names[instruction.Arg], out var module))
                                throw context.ModuleNotFoundError(PySR.Runtime_Import_ModuleNotFound, names[instruction.Arg]);

                            Stack.Push(module);
                        }
                        break;

                    case OpCode.ImportFrom:
                        value = PyOperators.GetAttr(context, Stack[-1], names[instruction.Arg]).PyUnwrap(context);
                        Stack.Push(value);
                        break;

                    case OpCode._ImportAllFrom:
                        ImportFromNode.ImportAllFrom(context, frame, (PyModuleObject)Stack.Pop());
                        break;

                    case OpCode.BuildSlice:
                        {
                            if (instruction.Arg is 2)
                            {
                                var end = Stack.Pop();
                                var start = Stack.Pop();
                                var slice = new PySliceObject(start, end, PyNoneObject.None);
                                Stack.Push(slice);
                            }
                            else
                            {
                                Debug.Assert(instruction.Arg is 3);
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

                            var caller = GetCaller(codeObj);
                            var func = new PyFunctionObject(
                                codeObj.Name,
                                caller.Call,
                                Caller.GetFreeVars(frame, codeObj),
                                frame.Variables._globals,
                                codeObj,
                                def);
                            caller.Func = func;

                            Stack.Push(func);
                        }
                        break;

                    case OpCode._MakeGeneratorExp:
                        {
                            var codeObj = (PyCodeObject)Stack.Pop();
                            Debug.Assert(codeObj.Bytecode is not null);

                            var inlineFrame = frame.CreateInlineFrame(FrameType.Comprehension);
                            var vm = new BytecodeVirtualMachine(context, codeObj.Bytecode);
                            var generator = new PyBytecodeGeneratorObject(codeObj.Name, inlineFrame, vm);

                            Stack.Push(generator);
                        }
                        break;

                    case OpCode._BuildClass:
                        {
                            var codeObj = (PyCodeObject)Stack.Pop();

                            List<PyTypeObject> bases = [];
                            LoadArgs(args, instruction.Arg);
                            foreach (var arg in args)
                            {
                                if (arg is not PyTypeObject baseType)
                                    throw new NotSupportedException();

                                bases.Add(baseType);
                            }

                            var type = ClassBuilder.Build(context, codeObj, bases, (context, _, _) =>
                            {
                                Debug.Assert(codeObj.Bytecode is not null);
                                var vm = new BytecodeVirtualMachine(context, codeObj.Bytecode);
                                vm.Eval().PyUnwrap(context);
                            });

                            Stack.Push(type);
                        }
                        break;

                    case OpCode._LoadClass:
                        Debug.Assert(frame.Caller is not null);
                        value = frame.Caller;
                        Stack.Push(value);
                        break;

                    case OpCode.MakeCell:
                        frame.Variables.StoreLocal(names[instruction.Arg], PyCellObject.CreateEmpty());
                        break;

                    case OpCode.RaiseVarArgs:
                        if (instruction.Arg is 0)
                        {
                            RaiseNode.Raise(context, frame, excObj: null, causeObj: null);
                        }
                        else if (instruction.Arg is 1)
                        {
                            var excObj = Stack.Pop();
                            RaiseNode.Raise(context, frame, excObj, causeObj: null);
                        }
                        else if (instruction.Arg is 2)
                        {
                            var causeObj = Stack.Pop();
                            var excObj = Stack.Pop();
                            RaiseNode.Raise(context, frame, excObj, causeObj);
                        }
                        else
                        {
                            throw new UnreachableException();
                        }
                        break;

                    case OpCode.CheckExcMatch:
                        var condition = ExceptHandlerNode.MakeCondition(context, Stack[-1]);
                        Stack[-1] = PyBoolObject.FromBoolean(condition(frame.CurrentException));
                        break;

                    case OpCode.CheckEgMatch:
                        {
                            var exc = frame.CurrentException;
                            if (!PyBaseExceptionGroupObjectType.Shared.IsInstance(exc))
                                exc = PyBaseExceptionGroupObjectType.CreateExceptionGroup(string.Empty, [exc]);

                            var type = Stack.Pop();
                            var (rest, match) = ExceptHandlerNode.Split(context, exc, type);
                            frame.Exceptions.Pop();
                            ExceptionHandlers.Peek().PyException = rest;
                            frame.Exceptions.Push(rest! /* null if rest is None, OpCode._PopExceptionAndJumpIfNull should handle that */);
                            Stack.Push(match);
                        }
                        break;

                    case OpCode._CheckMatch:
                        if (Stack.Peek() is PyNoneObject)
                        {
                            Stack.Pop();
                            nextIndex = instruction.Arg;
                        }
                        break;

                    case OpCode._LoadExc:
                        Stack.Push(frame.CurrentException);
                        break;

                    case OpCode._SetupFinally:
                        var handler = new ExceptionHandler(-1, instruction.Arg) { StackDepth = Stack.Count };
                        ExceptionHandlers.Push(handler);
                        break;

                    case OpCode._SetupExcept:
                        ExceptionHandlers.Peek().ExceptOffset = instruction.Arg;
                        break;

                    case OpCode._EnterFinally:
                        ExceptionHandlers.Peek().State = ExceptionHandler.State_Finally;
                        break;

                    case OpCode._ExitFinally:
                        var currentHandler = ExceptionHandlers.Peek();
                        currentHandler.State = ExceptionHandler.State_End;
                        if (currentHandler.PyException is not null)
                        {
                            var exc = currentHandler.PyException;
                            ExceptionHandlers.Pop();
                            throw new PyRuntimeException(exc);
                        }
                        ExceptionHandlers.Pop();
                        frame.Exceptions.Clear();
                        if (currentHandler.ReturnValue is not null)
                            returnValue = Move(ref currentHandler.ReturnValue);
                        break;

                    case OpCode._PopException:
                        frame.Exceptions.Pop();
                        ExceptionHandlers.Peek().PyException = null;
                        break;

                    case OpCode._PopExceptionIfTrue:
                        value = Stack.Peek();
                        boolValue = ((PyBoolObject)value).BoolValue;
                        if (boolValue)
                            goto case OpCode._PopException;
                        break;

                    case OpCode._PopExceptionAndJumpIfNull:
                        if (frame.Exceptions.Peek() is null)
                        {
                            nextIndex = instruction.Arg;
                            goto case OpCode._PopException;
                        }
                        break;

                    case OpCode._CallPrintIfNotNone:
                        value = Stack.Pop();
                        if (value is not PyNoneObject)
                            _ = PyBuiltinFunctions.Print.Call(context, [value]).PyUnwrap(context);
                        break;

                    case OpCode.MatchSequence:
                        boolValue = MatchSequenceNode.IsSequenceForMatch(Stack[-1], out _);
                        Stack.Push(PyBoolObject.FromBoolean(boolValue));
                        break;

                    case OpCode.MatchMapping:
                        boolValue = MatchMappingNode.IsMappingForMatch(Stack[-1], out _);
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
                            var array = new PyObject[keys._array.Length];
                            var matched = true;
                            for (int i = 0; matched && i < array.Length; i++)
                            {
                                var key = keys._array[i];
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

                            var values = new PyObject[instruction.Arg + keys._array.Length];

                            if (IsSpecialType(cls))
                            {
                                if (instruction.Arg > 1)
                                    throw context.TypeError(PySR.Runtime_MatchStmt_MatchArgsLengthNotEnough, cls.FullName, 1, instruction.Arg);
                                else if (instruction.Arg is 1)
                                    values[0] = subject;
                            }
                            else if (instruction.Arg > 0)
                            {
                                var matchArgs = PyOperators.GetAttr(context, cls, PySpecialNames.MatchArgs).PyUnwrap(context);

                                if (matchArgs is not PyTupleObject tuple)
                                    throw context.TypeError(PySR.Runtime_MatchStmt_MatchArgsIsNonTuple, cls.FullName, matchArgs.PyType.FullName);
                                if (instruction.Arg > tuple._array.Length)
                                    throw context.TypeError(PySR.Runtime_MatchStmt_MatchArgsLengthNotEnough, cls.FullName, tuple._array.Length, instruction.Arg);

                                for (int i = 0; i < instruction.Arg; i++)
                                {
                                    if (tuple._array[i] is not PyStrObject attrName)
                                        throw context.TypeError(PySR.Runtime_MatchStmt_MatchArgsEltMustBeString, tuple._array[i].PyType.FullName);

                                    var attr = PyOperators.GetAttr(context, subject, attrName);
                                    if (attr.IsAttributeError)
                                    {
                                        Stack.Push(PyNoneObject.None);
                                        goto match_class_break;
                                    }

                                    values[i] = attr.PyUnwrap(context);
                                }
                            }

                            for (int i = 0; i < keys._array.Length; i++)
                            {
                                var attrName = keys._array[i];
                                Debug.Assert(attrName is PyStrObject);

                                var attr = PyOperators.GetAttr(context, subject, attrName);
                                if (attr.IsAttributeError)
                                {
                                    Stack.Push(PyNoneObject.None);
                                    goto match_class_break;
                                }

                                values[instruction.Arg + i] = attr.PyUnwrap(context);
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

                    default:
                        throw new NotImplementedException($"OpCode {instruction.OpCode} is not implemented");
                }

                #endregion Eval OpCode

                if (intermediateValue is not null)
                    return intermediateValue;

                if (returnValue is not null)
                {
                find_next_finally:
                    if (!ExceptionHandlers.TryPeek(out var handler))
                    {
                        RunToEnd = true;
                        return returnValue;
                    }

                    if (handler.State is ExceptionHandler.State_Finally)
                    {
                        ExceptionHandlers.Pop();
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
                if (!ExceptionHandlers.TryPeek(out var currentHandler))
                {
                    Stack.Clear();
                    RunToEnd = true;
                    return PyResult.FromException(e.PyException);
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

                    frame.Exceptions.Clear();
                    ExceptionHandlers.Pop();

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
                context.EnsureFrameState(frame);

                frame.Exceptions.Push(e.PyException);
            }

            currentIndex = nextIndex;

            void LoadArgs(List<PyObject> args, int count)
            {
                // equals to:
                // args.Clear();
                // for (int i = 0; i < count; i++)
                //     args.Add(Stack.Pop());
                // args.Reverse();

                CollectionsMarshal.SetCount(args, count);
                var argsSpan = CollectionsMarshal.AsSpan(args);
                Stack.PopReversedRange(argsSpan);
            }
        }

        RunToEnd = true;
        return PyNoneObject.None;
    }
    private static T Move<T>([DisallowNull] ref T? value)
    {
        var result = value;
        value = default;
        return result;
    }

    private static FunctionCaller GetCaller(PyCodeObject codeObj)
    {
        Debug.Assert(codeObj.Bytecode is not null);
        return new FunctionCaller(FrameType.Function, (context, frame) =>
        {
            var vm = new BytecodeVirtualMachine(context, codeObj.Bytecode);
            return vm.Eval();
        });
    }
}
