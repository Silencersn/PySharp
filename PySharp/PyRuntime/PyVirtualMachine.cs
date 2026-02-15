using PySharp.AstNodes;
using PySharp.Bytecodes;
using PySharp.PyModules.Builtins;
using PySharp.PyRuntime.Calls;
using System.Diagnostics;

namespace PySharp.PyRuntime;

public sealed class PyExitEventArgs : EventArgs
{
    public int ExitCode { get; }
    public PyExceptionObject? Exception { get; }

    internal PyExitEventArgs(int exitCode, PyExceptionObject? exception = null)
    {
        ExitCode = exitCode;
        Exception = exception;
    }
}

public delegate void PyExitEventHandler(PyExitEventArgs args);

public static partial class PyVirtualMachine
{
    internal static PyModuleObject Execute(PyCallContext context, PyCodeObject code)
    {
        var module = new PyModuleObject(code.Name);
        ExecuteToObject(context, code.Bytecode, module);
        return module;
    }
    internal static void ExecuteToObject(PyCallContext context, Bytecode bytecode, PyModuleObject module)
    {
        // module will be reloaded
        module._pyAttributes = context.CurrentFrame.Variables._globals.Globals;

        _ = new BytecodeVirtualMachine(context, bytecode).Eval().PyUnwrap(context);

        Debug.Assert(ReferenceEquals(module.PyAttributes, context.CurrentFrame.Variables._globals.Globals));
        module.PyAttributes[PySpecialNames.Name] = PyStrObject.FromString(module.Name);
    }
}
