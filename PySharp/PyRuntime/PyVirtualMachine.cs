using PySharp.AstNodes;
using PySharp.PyModules.Builtins;
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
    private static readonly AsyncLocal<PyEnvironment?> _pyEnvironmentAsyncLocal = new();
    internal static AsyncLocal<PyEnvironment?> PyEnvironmentAsyncLocal => _pyEnvironmentAsyncLocal;
    private static PyEnvironment GetPyEnvironment()
    {
        return _pyEnvironmentAsyncLocal.Value ?? throw new InvalidOperationException($"The {nameof(Environments.PyEnvironment)} is necessary");
    }
    public static PyEnvironment PyEnvironment => GetPyEnvironment();

    internal static PyExceptionObject? CurrentException
    {
        get => PyEnvironment.CurrentError;
        set => PyEnvironment.CurrentError = value;
    }
    internal static TextReader In => PyEnvironment.In;
    internal static TextWriter Out => PyEnvironment.Out;
    internal static TextWriter Error => PyEnvironment.Error;
    internal static PyFrame CurrentFrame => PyEnvironment.CurrentFrame;
    internal static bool IsInteractive => PyEnvironment.IsInteractive;

    internal static void EnterFrame(PyFrame frame)
    {
        PyEnvironment.CurrentFrame = frame;
    }

    internal static void ExitFrame()
    {
        Debug.Assert(PyEnvironment.CurrentFrame.Back is not null);
        PyEnvironment.CurrentFrame = PyEnvironment.CurrentFrame.Back;
    }

    internal static void Exit(int exitCode)
    {
        PyEnvironment.ExitCode = exitCode;
        RaiseSystemExit();
        throw new PyRuntimeException(CurrentException);
    }

    internal static PyModuleObject Execute(ModuleNode moduleNode, string moduleName, bool newFrame)
    {
        var module = new PyModuleObject(moduleName);
        ExecuteToObject(moduleNode, module, newFrame);
        return module;
    }
    internal static void ExecuteToObject(ModuleNode moduleNode, PyModuleObject module, bool newFrame)
    {
        if (newFrame)
        {
            EnterFrame(CurrentFrame.CreateFrame(newGlobals: true));
            PyEnvironment.Init(PyEnvironmentOptions.Default);
        }

        moduleNode.Execute(CurrentFrame);

        // module will be reloaded
        module.PyAttributes.Clear();

        foreach (var pair in CurrentFrame.Globals)
        {
            // all statements have been executed,
            // there should be no uninitialized variables.
            Debug.Assert(pair.Value is not null);

            module.PyAttributes[pair.Key] = pair.Value;
        }

        if (newFrame)
            ExitFrame();
    }
}
