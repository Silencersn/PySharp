using PySharp.AstNodes;
using PySharp.Compilation;
using PySharp.PyModules.Builtins;
using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace PySharp.Bytecodes;

internal sealed class BytecodeVirtualMachine
{
    private Stack<PyObject> Stack { get; } = [];
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

    internal void Eval(PyCallContext context, PyFrame frame, List<Instruction> instructions)
    {
        int currentIndex = 0;

        // cache, clear before using
        PyObject value;
        bool boolValue;
        List<PyObject> args = [];

        while (currentIndex < instructions.Count)
        {
            var instruction = instructions[currentIndex];
            var nextIndex = currentIndex + 1;

            switch (instruction.OpCode)
            {
                case OpCode.NoOperation:
                    break;

                case OpCode.LoadConst:
                    Stack.Push(instruction.GetOperand<PyObject>());
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
                    frame.StoreGlobal(instruction.GetOperand<string>(), value);
                    break;

                case OpCode.StoreFast:
                    value = Stack.Pop();
                    frame.StoreFast(instruction.Arg, value);
                    break;

                case OpCode.DeleteName:
                    frame.DeleteName(instruction.GetOperand<string>()).PyUnwrap(context);
                    break;

                case OpCode.DeleteGlobal:
                    frame.DeleteGlobal(instruction.GetOperand<string>()).PyUnwrap(context);
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
                    Stack.Push(Stack.Peek());
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


                default:
                    break;
            }

            currentIndex = nextIndex;
        }
    }
}
