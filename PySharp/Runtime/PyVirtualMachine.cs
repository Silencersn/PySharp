using PySharp.Compilation.Bytecodes;
using PySharp.Modules.Builtins;
using PySharp.Runtime.Calls;
using PySharp.Runtime.Calls.Extensions;
using System.Diagnostics;

namespace PySharp.Runtime;

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
        ExecuteToObject(context, code, module);
        return module;
    }
    internal static void ExecuteToObject(PyCallContext context, PyCodeObject code, PyModuleObject module)
    {
        // module will be reloaded
        module._pyAttributes = context.CurrentInternalFrame.Variables.Globals.Dict;

        context.CurrentInternalFrame.CodeObject = code;
        _ = PyCore.Eval(context, code.Bytecode).PyUnwrap(context);

        Debug.Assert(ReferenceEquals(module.PyAttributes, context.CurrentInternalFrame.Variables.Globals.Dict));
        module.PyAttributes[PySpecialNames.Name] = PyStrObject.FromString(module.Name);
    }
}
