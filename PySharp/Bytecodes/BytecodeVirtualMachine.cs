using PySharp.AstNodes;
using PySharp.Compilation;
using PySharp.PyModules.Builtins;
using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;
using System.Collections;
using System.Diagnostics;

namespace PySharp.Bytecodes;

internal sealed class BytecodeVirtualMachine
{
    private OperandStack Stack { get; } = new();
    private PyCallContext Context { get; }
    private PyBytecodeCompilation Compilation { get; }

    internal BytecodeVirtualMachine(PyCallContext context, PyBytecodeCompilation compilation)
    {
        Context = context;
        Compilation = compilation;
    }

    internal void Eval()
    {
        var frame = Context.CurrentFrame;
        frame.SemanticModel = Compilation.Bytecode.Model;
        Eval(Context, frame, Compilation.Bytecode.Instructions);
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

    internal void Eval(PyCallContext context, PyFrame frame, List<Instruction> instructions)
    {
        int currentIndex = 0;
        
        Stack<ExceptionHandler> exceptionHandlers = [];

        // cache, clear before using
        PyObject value;
        bool boolValue;
        List<PyObject> args = [];
        PyResult result;

        while (currentIndex < instructions.Count)
        {
            var instruction = instructions[currentIndex];
            var nextIndex = currentIndex + 1;

            try
            {
                EvalOpCode(instruction.OpCode);
            }
            catch (PyRuntimeException e)
            {
                handle:
                if (!exceptionHandlers.TryPeek(out var currentHandler))
                {
                    Stack.Clear();
                    throw;
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

                    case OpCode.DeleteName:
                        frame.DeleteName(instruction.StringOperand).PyUnwrap(context);
                        break;

                    case OpCode.DeleteGlobal:
                        frame.DeleteGlobal(instruction.StringOperand).PyUnwrap(context);
                        break;

                    case OpCode.DeleteFast:
                        frame.DeleteFast(instruction.Arg).PyUnwrap(context);
                        break;

                    case OpCode.Call:
                        args.Clear();
                        for (int i = 0; i < instruction.Arg; i++)
                            args.Add(Stack.Pop());
                        args.Reverse();
                        var callable = Stack.Pop();
                        value = callable.Call(context, args).PyUnwrap(context);
                        Stack.Push(value);
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
                        value = Stack.Pop();
                        value = PySpecialMethods.Bool(context, value).PyUnwrap(context);
                        Stack.Push(value);
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
                        value = Stack.Pop();
                        value = PySpecialMethods.Iter(context, value).PyUnwrap(context);
                        Stack.Push(value);
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
                        var right = Stack.Pop();
                        var left = Stack.Pop();
                        value = BinOpNode.EvalOperator(context, (OperatorType)instruction.Arg, left, right).PyUnwrap(context);
                        Stack.Push(value);
                        break;

                    case OpCode._UnaryOp:
                        value = Stack.Pop();
                        value = UnaryOpNode.EvalOperator(context, (UnaryOpType)instruction.Arg, value).PyUnwrap(context);
                        Stack.Push(value);
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
                        value = Stack.Pop();
                        var condition = ExceptHandlerNode.MakeCondition(context, value);
                        Stack.Push(PyBoolObject.FromBoolean(condition(frame.CurrentException)));
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
    }
}
