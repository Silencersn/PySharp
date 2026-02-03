using PySharp.AstNodes;
using PySharp.Compilation;
using PySharp.PyModules.Builtins;
using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;
using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Text;

namespace PySharp.Bytecodes;

internal sealed class BytecodeVirtualMachine
{
    // States
    internal int InstructionIndex;
    internal PyExceptionObject? ExceptionToRaise;
    internal bool RunToEnd { get; private set; }
    private Stack<ExceptionHandler> ExceptionHandlers => field ??= [];
    private OperandStack Stack { get; } = new();

    private PyCallContext Context { get; }
    private Bytecode Bytecode { get; }


    internal BytecodeVirtualMachine(PyCallContext context, Bytecode bytecode)
    {
        Context = context;
        Bytecode = bytecode;
    }

    internal PyResult Eval()
    {
        var frame = Context.CurrentFrame;
        return Eval(Context, frame);
    }

    private class ExceptionHandler
    {
        public const int State_Init = 0, State_Except = 1, State_Finally = 2, State_End = 3;

        public Label? ExceptLabel;
        public Label FinallyLabel;
        public int State;
        public PyExceptionObject? PyException;
        public int StackDepth;
        public PyObject? ReturnValue;

        public ExceptionHandler(Label? exceptionHandlerLabel, Label finallyLabel)
        {
            ExceptLabel = exceptionHandlerLabel;
            FinallyLabel = finallyLabel;
            State = State_Init;
            PyException = null;
        }
    }

    private sealed class OperandStack
    {
        private readonly List<PyObject> _stack = [];

        public int Count => _stack.Count;

        internal List<PyObject> InternalList => _stack;

        public PyObject this[int index]
        {
            get => _stack[_stack.Count + index];
            set => _stack[_stack.Count + index] = value;
        }

        public void Push(PyObject value)
        {
            _stack.Add(value);
        }
        public PyObject Peek()
        {
            return _stack[^1];
        }
        public PyObject Pop()
        {
            var result = Peek();
            _stack.RemoveAt(_stack.Count - 1);
            return result;
        }
        public void Clear()
        {
            _stack.Clear();
        }
    }

    internal void SetYieldReceivedValue(PyObject value)
    {
        Stack.Push(value);
    }

    internal PyResult Eval(PyCallContext context, PyFrame frame)
    {
        int currentIndex = InstructionIndex;
        var instructions = Bytecode.Instructions;

        // cache, clear before using
        PyObject value, left, right;
        bool boolValue;
        List<PyObject> args = [];
        OrderedDictionary<string, PyObject> kwargs = [];
        PyResult result;
        PyObject? returnValue = null, intermediateValue = null;
        List<KeyValuePair<PyObject, PyObject>> pairs = [];
        StringBuilder builder = new();

        while (currentIndex < instructions.Count)
        {
            var instruction = instructions[currentIndex];
            var nextIndex = currentIndex + 1;

            try
            {
                EvalOpCode(instruction.OpCode);
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
                    nextIndex = handler.FinallyLabel.Offset;
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
                    nextIndex = currentHandler.FinallyLabel.Offset;
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

                    if (currentHandler.ExceptLabel is not null)
                    {
                        currentHandler.State = ExceptionHandler.State_Except;
                        nextIndex = currentHandler.ExceptLabel.Offset;
                    }
                    else
                    {
                        nextIndex = currentHandler.FinallyLabel.Offset;
                    }
                }

                Debug.Assert(currentHandler.StackDepth <= Stack.Count);
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
                var stackSpan = CollectionsMarshal.AsSpan(Stack.InternalList);
                stackSpan[^count..].CopyTo(argsSpan);
                CollectionsMarshal.SetCount(Stack.InternalList, Stack.Count - count);
            }

            void EvalOpCode(OpCode opCode)
            {
                switch (opCode)
                {
                    case OpCode.NoOperation:
                        break;

                    case OpCode.LoadConst:
                        Stack.Push(instruction.PyObjectOperand);
                        break;

                    case OpCode.LoadName:
                        value = frame.LoadName(instruction.StringOperand).PyUnwrap(context);
                        Stack.Push(value);
                        break;

                    case OpCode.LoadGlobal:
                        value = frame.LoadGlobal(instruction.StringOperand).PyUnwrap(context);
                        Stack.Push(value);
                        break;

                    case OpCode.LoadFast:
                        value = frame.LoadFast(instruction.Arg).PyUnwrap(context);
                        Stack.Push(value);
                        break;

                    case OpCode.LoadDeref:
                        value = frame.LoadClosure(instruction.StringOperand, isLocal: default /* TODO: allow unknown isLocal */).PyUnwrap(context);
                        Stack.Push(value);
                        break;

                    case OpCode.StoreName:
                        value = Stack.Pop();
                        frame.StoreName(instruction.StringOperand, value);
                        break;

                    case OpCode.StoreGlobal:
                        value = Stack.Pop();
                        frame.StoreGlobal(instruction.StringOperand, value);
                        break;

                    case OpCode.StoreFast:
                        value = Stack.Pop();
                        frame.StoreFast(instruction.Arg, value);
                        break;

                    case OpCode.StoreDeref:
                        value = Stack.Pop();
                        frame.StoreClosure(instruction.StringOperand, value);
                        break;

                    case OpCode._StoreNameIncludedNonInlineFrame:
                        value = Stack.Pop();
                        frame.StoreName(instruction.StringOperand, value);
                        frame._outerNonInlineFrame?.StoreName(instruction.StringOperand, value);
                        break;

                    case OpCode._StoreDerefIncludedNonInlineFrame:
                        value = Stack.Pop();
                        frame.StoreClosure(instruction.StringOperand, value);
                        frame._outerNonInlineFrame?.StoreClosure(instruction.StringOperand, value);
                        break;

                    case OpCode.DeleteName:
                        frame.DeleteName(instruction.StringOperand).PyUnwrap(context);
                        break;

                    case OpCode.DeleteGlobal:
                        frame.DeleteGlobal(instruction.StringOperand).PyUnwrap(context);
                        break;

                    case OpCode.DeleteFast:
                        frame.DeleteFast(instruction.Arg).PyUnwrap(context);
                        break;

                    case OpCode.DeleteDeref:
                        frame.DeleteClosure(instruction.StringOperand, isLocal: default /* TODO: allow unknown isLocal */);
                        break;

                    case OpCode.LoadAttr:
                        {
                            Stack[-1] = PyOperators.GetAttr(context, Stack[-1], instruction.StringOperand).PyUnwrap(context);
                        }
                        break;

                    case OpCode.StoreAttr:
                        {
                            var obj = Stack.Pop();
                            value = Stack.Pop();
                            PyOperators.SetAttr(context, obj, instruction.StringOperand, value).PyUnwrap(context);
                        }
                        break;

                    case OpCode.DeleteAttr:
                        {
                            var obj = Stack.Pop();
                            PyOperators.DelAttr(context, obj, instruction.StringOperand).PyUnwrap(context);
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

                            for(int i = 0; i < tuple._array.Length; i++)
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
                        nextIndex = instruction.LabelOperand.Offset;
                        break;

                    case OpCode.PopJumpIfFalse:
                        {
                            value = Stack.Pop();
                            boolValue = ((PyBoolObject)value).BoolValue;
                            if (!boolValue)
                                nextIndex = instruction.LabelOperand.Offset;
                        }
                        break;

                    case OpCode.PopJumpIfTrue:
                        {
                            value = Stack.Pop();
                            boolValue = ((PyBoolObject)value).BoolValue;
                            if (boolValue)
                                nextIndex = instruction.LabelOperand.Offset;
                        }
                        break;

                    case OpCode.GetIter:
                        Stack[-1] = PySpecialMethods.Iter(context, Stack[-1]).PyUnwrap(context);
                        break;

                    case OpCode.ForIter:
                        result = PySpecialMethods.Next(context, Stack.Peek());
                        if (result.IsStopIteration)
                            nextIndex = instruction.LabelOperand.Offset;
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
                                nextIndex = instruction.LabelOperand.Offset;
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
                            foreach (var value in args)
                            {
                                Debug.Assert(value is PyStrObject);
                                builder.Append(((PyStrObject)value).Value);
                            }
                            Stack.Push(PyStrObject.FromString(builder.ToString()));
                        }
                        break;

                    case OpCode._MakeFunctionWithPyArgsDef:
                        {
                            var codeObj = (PyCodeObject)Stack.Pop();

                            var argsNode = instruction.GetOperand<AstArgumentsNode>();
                            PyObject?[] kwDefaults = new PyObject?[argsNode.KwDefaults.Length];
                            for (int i = kwDefaults.Length - 1; i >= 0; i--)
                                kwDefaults[i] = Stack.Pop();
                            PyObject[] defaults = new PyObject[argsNode.Defaults.Length];
                            for (int i = defaults.Length - 1; i >= 0; i--)
                                defaults[i] = Stack.Pop();
                            var def = PyArgsDef.FromAstAndObjs(argsNode, kwDefaults, defaults);

                            var caller = GetCaller(codeObj);
                            var func = new PyFunctionObject(
                                codeObj.Name,
                                caller.Call,
                                Caller.GetFreeVars(frame, codeObj),
                                frame._globals,
                                codeObj,
                                def);
                            caller.Func = func;

                            // TODO: __doc__

                            Stack.Push(func);
                        }
                        break;

                    case OpCode._MakeGeneratorExp:
                        {
                            var inlineFrame = frame.CreateInlineFrame(FrameType.Comprehension);
                            var vm = new BytecodeVirtualMachine(context, instruction.GetOperand<Bytecode>());
                            var generator = new PyBytecodeGeneratorObject("<genexpr>", inlineFrame, vm);

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

                            var type = ClassBuilder.Build(context, codeObj, bases, (context, _) =>
                            {
                                Debug.Assert(codeObj.Bytecode is not null);
                                var vm = new BytecodeVirtualMachine(context, codeObj.Bytecode);
                                vm.Eval().PyUnwrap(context);
                            });

                            Stack.Push(type);
                        }
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

                    case OpCode._SetupExceptionHandler:
                        Debug.Assert(instruction.Operand is not null);
                        var labelPair = ((Label?, Label))instruction.Operand;
                        var handler = new ExceptionHandler(labelPair.Item1, labelPair.Item2) { StackDepth = Stack.Count };
                        ExceptionHandlers.Push(handler);
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

                    default:
                        throw new NotImplementedException($"OpCode {opCode} is not implemented");
                }
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
