using PySharp.AstNodes;
using PySharp.PyModules.Builtins;
using PySharp.PyRuntime.Environments;
using PySharp.Tokenization;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;

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

    [return: NotNullIfNotNull(nameof(moduleName))]
    public static PyModuleObject? ExecuteAstNodeWithinEnvironment(ModuleNode moduleNode, string? moduleName = null)
    {
        var rootFrame = CurrentFrame;

        moduleNode.Execute(rootFrame);
        if (moduleName is null)
            return null;

        var module = new PyModuleObject(moduleName);
        foreach (var pair in rootFrame.Globals)
        {
            module.PyAttributes[pair.Key] = pair.Value;
        }

        return module;
    }

    [return: NotNullIfNotNull(nameof(moduleName))]
    public static PyModuleObject? ExecuteAstNode(ModuleNode moduleNode, string? moduleName = null, PyEnvironment? environment = null)
    {
        ArgumentNullException.ThrowIfNull(moduleNode);

        environment ??= PyEnvironment.Console;

        using var context = new PyEnvironmentContext(environment);

        PyEnvironment.Exit += static args =>
        {
            if (args.Exception is not null)
                Console.WriteLine($"[exception:\n{args.Exception.ToMessage()}]");
            Console.WriteLine($"[exit code: {args.ExitCode}]");
        };
        Console.WriteLine('[' + PyEnvironment.IdWithThread + " " + nameof(IsExceptionRaised) + " : " + IsExceptionRaised() + ']');

        PyModuleObject? module = null;
        try
        {
            module = ExecuteAstNodeWithinEnvironment(moduleNode, moduleName);
            Debug.Assert(CurrentFrame.IsRoot);
        }
        catch (Exception ex)
        {
            var temp = ex;
            while (temp is not null)
            {
                if (temp is PyRuntimeException pyRuntimeException)
                {
                    if (CurrentException is null)
                    {
                        Console.WriteLine("[WARNNING: CurrentError is null]");
                        CurrentException = pyRuntimeException.PyException;
                    }

                    if (pyRuntimeException.PyException.PyType == PyStandardExceptionTypes.SystemExit)
                        ClearException();
                    else if (PyEnvironment.ExitCode is 0)
                        PyEnvironment.ExitCode = 1;
                    break;
                }

                temp = temp.InnerException;
            }
            if (temp is null)
                throw;
        }

        PyEnvironment.OnExit();
        return module;
    }

    public static Task ExecuteAsync(ModuleNode module)
    {
        return Task.Run(() => ExecuteAstNode(module));
    }
}
