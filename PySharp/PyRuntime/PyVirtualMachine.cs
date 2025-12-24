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
    private static volatile int _asyncContextCounter = 0;
    private static PyEnvironment? _nonAsyncEnvironment;
    private static readonly AsyncLocal<PyEnvironment?> _pyEnvironmentAsyncLocal = new();
    internal static AsyncLocal<PyEnvironment?> PyEnvironmentAsyncLocal => _pyEnvironmentAsyncLocal;
    private static PyEnvironment GetAsyncPyEnvironment()
    {
        return _pyEnvironmentAsyncLocal.Value ?? throw new InvalidOperationException($"The {nameof(Environments.PyEnvironment)} is necessary");
    }
    private static PyEnvironment GetNonAsyncPyEnvironment()
    {
        return _nonAsyncEnvironment ?? throw new InvalidOperationException($"The {nameof(Environments.PyEnvironment)} is necessary");
    }
    internal static bool IsAsyncContext => _asyncContextCounter > 0;
    public static PyEnvironment PyEnvironment => IsAsyncContext ? GetAsyncPyEnvironment() : GetNonAsyncPyEnvironment();
    internal static PyEnvironment? InternalPyEnvironment => IsAsyncContext ? _pyEnvironmentAsyncLocal.Value : _nonAsyncEnvironment;

    public static void SetAsync(bool enable)
    {
        if (enable)
        {
            Interlocked.Increment(ref _asyncContextCounter);
        }
        else
        {
            if (_asyncContextCounter is 0)
                throw new InvalidOperationException($"Cannot decrement {nameof(_asyncContextCounter)} below zero. SetAsync(false) called more times than SetAsync(true).");
            Interlocked.Decrement(ref _asyncContextCounter);
        }
    }

    internal static void SetPyEnvironment(PyEnvironment? environment)
    {
        if (IsAsyncContext)
            PyEnvironmentAsyncLocal.Value = environment;
        else
            _nonAsyncEnvironment = environment;
    }

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

    internal static PyModuleObject Execute(PyCallContext context, ModuleNode moduleNode, string moduleName, bool newFrame)
    {
        var module = new PyModuleObject(moduleName);
        ExecuteToObject(context, moduleNode, module, newFrame);
        return module;
    }
    internal static void ExecuteToObject(PyCallContext context, ModuleNode moduleNode, PyModuleObject module, bool newFrame)
    {
        if (newFrame)
        {
            EnterFrame(PyFrame.CreateModuleFrame(CurrentFrame));
            PyEnvironment.Init(PyEnvironmentOptions.Default);
        }

        moduleNode.Execute(PyCallContext.Null, CurrentFrame);

        // module will be reloaded
        module._pyAttributes = CurrentFrame._globals.Globals;
        Debug.Assert(ReferenceEquals(module.PyAttributes, CurrentFrame._globals.Globals));
        if (AstUtils.TryGetDoc(moduleNode.Body, out var doc))
            module.PyAttributes[PySpecialNames.Doc] = doc;

        foreach (var pair in CurrentFrame.Globals)
        {
            // all statements have been executed,
            // there should be no uninitialized variables.
            Debug.Assert(pair.Value is not null);

            module.PyAttributes[pair.Key] = pair.Value;
        }
        module.PyAttributes[PySpecialNames.Name] = PyStrObject.FromString(module.Name);

        if (newFrame)
            ExitFrame();
    }
}
