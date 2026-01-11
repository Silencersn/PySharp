using PySharp.AstNodes;
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
    internal static PyModuleObject Execute(PyCallContext context, ModuleNode moduleNode, string moduleName)
    {
        var module = new PyModuleObject(moduleName);
        ExecuteToObject(context, moduleNode, module);
        return module;
    }
    internal static void ExecuteToObject(PyCallContext context, ModuleNode moduleNode, PyModuleObject module)
    {
        moduleNode.Execute(context, context.CurrentFrame);

        // module will be reloaded
        module._pyAttributes = context.CurrentFrame._globals.Globals;
        Debug.Assert(ReferenceEquals(module.PyAttributes, context.CurrentFrame._globals.Globals));
        if (AstUtils.TryGetDoc(moduleNode.Body, out var doc))
            module.PyAttributes[PySpecialNames.Doc] = doc;

        module.PyAttributes[PySpecialNames.Name] = PyStrObject.FromString(module.Name);
    }
}
