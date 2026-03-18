using PySharp.Modules.Builtins;
using PySharp.Runtime.Calls;
using System.Diagnostics;
using System.Text;

namespace PySharp.Compilation.Bytecodes;

internal struct BytecodeVirtualMachineStates
{
    internal PyExceptionObject? ExceptionToRaise;
    internal bool RunToEnd;
    internal int OperandStackSize;
    internal Stack<BytecodeVirtualMachine.ExceptionHandler> ExceptionHandlers => field ??= [];
    internal readonly OperandStack? Stack;
    internal PyCallContext Context { get; }
    internal Stack<PyExceptionObject> Exceptions => field ??= [];
    internal PyExceptionObject CurrentException => Exceptions.Peek();

    internal List<PyObject> CacheArgs => field ??= [];
    internal OrderedDictionary<string, PyObject> CacheKwargs => field ??= [];
    internal List<KeyValuePair<PyObject, PyObject>> CachePairs => field ??= [];
    internal StringBuilder CacheBuilder => field ??= new();

    internal BytecodeVirtualMachineStates(PyCallContext context, bool usingLocalsPlusAsOperandStack = false)
    {
        Debug.Assert(context.CurrentInternalFrame.CodeObject is not null);

        Context = context;
        Stack = usingLocalsPlusAsOperandStack ? null : new(context.CurrentInternalFrame.CodeObject.Bytecode.StackSize);
    }

    internal void SetYieldReceivedValue(PyObject value)
    {
        Debug.Assert(Stack is not null);
        Stack.Push(value);
        OperandStackSize = Stack.Count;
    }
}
