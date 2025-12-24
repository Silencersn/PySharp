using PySharp.AstNodes;
using PySharp.PyModules.Builtins;
using PySharp.PyRuntime.Calls;
using PySharp.PyRuntime.Environments;
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
    internal static PyModuleObject Execute(PyCallContext context, ModuleNode moduleNode, string moduleName, bool newFrame)
    {
        var module = new PyModuleObject(moduleName);
        ExecuteToObject(context, moduleNode, module, newFrame);
        return module;
    }
    internal static void ExecuteToObject(PyCallContext context, ModuleNode moduleNode, PyModuleObject module, bool newFrame)
    {
        if (newFrame)
            context.EnterFrame(PyFrame.CreateModuleFrame(context, context.CurrentFrame));

        moduleNode.Execute(context, context.CurrentFrame);

        // module will be reloaded
        module._pyAttributes = context.CurrentFrame._globals.Globals;
        Debug.Assert(ReferenceEquals(module.PyAttributes, context.CurrentFrame._globals.Globals));
        if (AstUtils.TryGetDoc(moduleNode.Body, out var doc))
            module.PyAttributes[PySpecialNames.Doc] = doc;

        foreach (var pair in context.CurrentFrame.Globals)
        {
            // all statements have been executed,
            // there should be no uninitialized variables.
            Debug.Assert(pair.Value is not null);

            module.PyAttributes[pair.Key] = pair.Value;
        }
        module.PyAttributes[PySpecialNames.Name] = PyStrObject.FromString(module.Name);

        if (newFrame)
            context.ExitFrame();
    }
}
