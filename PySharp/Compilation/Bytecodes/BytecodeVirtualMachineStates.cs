using PySharp.Modules.Builtins;
using PySharp.Runtime.Calls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace PySharp.Compilation.Bytecodes;

internal struct BytecodeVirtualMachineStates
{
    internal PyExceptionObject? ExceptionToRaise;
    internal bool RunToEnd;
    internal Stack<BytecodeVirtualMachine.ExceptionHandler> ExceptionHandlers => field ??= [];
    internal readonly OperandStack? Stack;
    internal PyCallContext Context { get; }
    internal Bytecode Bytecode { get; }
    internal Stack<PyExceptionObject> Exceptions => field ??= [];
    internal PyExceptionObject CurrentException => Exceptions.Peek();

    internal List<PyObject> CacheArgs => field ??= [];
    internal OrderedDictionary<string, PyObject> CacheKwargs => field ??= [];
    internal List<KeyValuePair<PyObject, PyObject>> CachePairs => field ??= [];
    internal StringBuilder CacheBuilder => field ??= new();

    internal BytecodeVirtualMachineStates(PyCallContext context, Bytecode bytecode, bool usingLocalsPlusAsOperandStack = false)
    {
        Context = context;
        Bytecode = bytecode;
        Stack = usingLocalsPlusAsOperandStack ? null : new(bytecode.StackSize);
    }

    internal readonly void SetYieldReceivedValue(PyObject value)
    {
        Debug.Assert(Stack is not null);
        Stack.Push(value);
    }
}
