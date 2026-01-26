using PySharp.AstNodes;
using PySharp.Compilation;
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
    internal static PyModuleObject Execute(PyCallContext context, PyCompilation compilation, string moduleName)
    {
        var module = new PyModuleObject(moduleName);
        ExecuteToObject(context, compilation, module);
        return module;
    }
    internal static void ExecuteToObject(PyCallContext context, PyCompilation compilation, PyModuleObject module)
    {
        // module will be reloaded
        module._pyAttributes = context.CurrentFrame._globals.Globals;

        compilation.Execute(context);

        Debug.Assert(ReferenceEquals(module.PyAttributes, context.CurrentFrame._globals.Globals));
        module.PyAttributes[PySpecialNames.Name] = PyStrObject.FromString(module.Name);
    }
}
