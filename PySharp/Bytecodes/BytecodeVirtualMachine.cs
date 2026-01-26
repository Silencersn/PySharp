using PySharp.AstNodes;
using PySharp.Compilation;
using PySharp.PyModules.Builtins;
using PySharp.PyRuntime.Calls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace PySharp.Bytecodes;

internal sealed class BytecodeVirtualMachine
{
    private Stack<PyObject> Stack { get; } = [];
    private List<Instruction> Instructions => Compilation.Instructions;
    private PyCallContext Context { get; }
    private PyBytecodeCompilation Compilation { get; }

    internal BytecodeVirtualMachine(PyCallContext context, PyBytecodeCompilation compilation)
    {
        Context = context;
        Compilation = compilation;
    }

    internal void Eval()
    {
        var context = Context;
        var frame = Context.CurrentFrame;
        frame.SemanticModel = Compilation.Model;
        int currentIndex = 0;

        PyObject value;
        List<PyObject> args = [];

        while (currentIndex < Instructions.Count)
        {
            var instruction = Instructions[currentIndex];
            var nextIndex = currentIndex + 1;

            switch (instruction.OpCode)
            {
                case OpCode.NoOperation:
                    break;

                case OpCode.LoadConst:
                    Stack.Push(instruction.GetOperand<PyObject>());
                    break;

                case OpCode.LoadName:
                    value = frame.LoadName(instruction.GetOperand<string>()).PyUnwrap(context);
                    Stack.Push(value);
                    break;

                case OpCode.LoadGlobal:
                    value = frame.LoadGlobal(instruction.GetOperand<string>()).PyUnwrap(context);
                    Stack.Push(value);
                    break;

                case OpCode.LoadFast:
                    value = frame.LoadFast(instruction.Arg).PyUnwrap(context);
                    Stack.Push(value);
                    break;

                case OpCode.StoreName:
                    value = Stack.Pop();
                    frame.StoreName(instruction.GetOperand<string>(), value);
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

                default:
                    break;
            }

            currentIndex = nextIndex;
        }
    }
}
