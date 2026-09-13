using PySharp.Modules.Builtins;
using PySharp.Runtime.Calls;
using System.Diagnostics;
using System.Text;

namespace PySharp.Runtime.VirtualMachine;

internal struct BytecodeVirtualMachineStates
{
    internal PyExceptionObject? ExceptionToRaise;
    internal bool RunToEnd;
    internal int OperandStackSize;
    // HandledException value observed when this frame started executing;
    // restored on frame exit so a callee's handler state never leaks onto
    // the caller's chain (CPython: frame exc_state pop on exit).
    internal PyExceptionObject? SavedHandledException;
    internal Stack<BytecodeVirtualMachine.ExceptionHandler> ExceptionHandlers => field ??= [];
    internal readonly OperandStack? Stack;
    internal Stack<PyExceptionObject> Exceptions => field ??= [];
    internal PyExceptionObject CurrentException => Exceptions.Peek();

    internal List<PyObject> CacheArgs => field ??= [];
    internal OrderedDictionary<string, PyObject> CacheKwargs => field ??= [];
    internal List<KeyValuePair<PyObject, PyObject>> CachePairs => field ??= [];
    internal StringBuilder CacheBuilder => field ??= new();

    internal BytecodeVirtualMachineStates(PyCallContext context, bool usingLocalsPlusAsOperandStack = false)
    {
        Debug.Assert(context.CurrentInternalFrame.CodeObject is not null);

        Stack = usingLocalsPlusAsOperandStack ? null : new(context.CurrentInternalFrame.CodeObject.Bytecode.StackSize);
    }

    internal void SetYieldReceivedValue(PyObject value)
    {
        Debug.Assert(Stack is not null);
        Stack.Push(value);
        OperandStackSize = Stack.Count;
    }
}
