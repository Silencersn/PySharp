using PySharp.AstNodes;
using PySharp.Compilation;
using PySharp.PyModules.Builtins;
using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;
using System.Collections;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PySharp.Bytecodes;

internal sealed class BytecodeVirtualMachine
{
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

    internal PyResult Eval(PyCallContext context, PyFrame frame)
    {
        int currentIndex = 0;
        var instructions = Bytecode.Instructions;
        Stack<ExceptionHandler> exceptionHandlers = [];

        // cache, clear before using
        PyObject value, left, right;
        bool boolValue;
        List<PyObject> args = [];
        OrderedDictionary<string, PyObject> kwargs = [];
        PyResult result;
        PyObject? returnValue = null;

        while (currentIndex < instructions.Count)
        {
            var instruction = instructions[currentIndex];
            var nextIndex = currentIndex + 1;

            try
            {
                EvalOpCode(instruction.OpCode);
                if (returnValue is not null)
                    return returnValue;
            }
            catch (PyRuntimeException e)
            {
                handle:
                if (!exceptionHandlers.TryPeek(out var currentHandler))
                {
                    Stack.Clear();
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
                    exceptionHandlers.Pop();

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
                        value = Stack.Pop();
                        boolValue = ((PyBoolObject)value).BoolValue;
                        if (!boolValue)
                            nextIndex = instruction.LabelOperand.Offset;
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

                    case OpCode.ReturnValue:
                        returnValue = Stack.Pop();
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
                        exceptionHandlers.Push(handler);
                        break;

                    case OpCode._EnterFinally:
                        exceptionHandlers.Peek().State = ExceptionHandler.State_Finally;
                        break;

                    case OpCode._ExitFinally:
                        var currentHandler = exceptionHandlers.Peek();
                        currentHandler.State = ExceptionHandler.State_End;
                        if (currentHandler.PyException is not null)
                        {
                            var exc = currentHandler.PyException;
                            exceptionHandlers.Pop();
                            throw new PyRuntimeException(exc);
                        }
                        exceptionHandlers.Pop();
                        frame.Exceptions.Clear();
                        break;

                    case OpCode._PopException:
                        frame.Exceptions.Pop();
                        exceptionHandlers.Peek().PyException = null;
                        break;

                    default:
                        throw new NotImplementedException($"OpCode {opCode} is not implemented");
                }
            }
        }

        return PyNoneObject.None;
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
