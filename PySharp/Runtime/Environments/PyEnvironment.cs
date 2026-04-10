using PySharp.Modules.Builtins;
using PySharp.Runtime.IO;
using PySharp.Runtime.IO.Memory;
using PySharp.Utility;

namespace PySharp.Runtime.Environments;

public sealed partial class PyEnvironment
{
    static PyEnvironment()
    {
    }

    internal PyEnvironment(PyEnvironmentHost host, int optimizationLevel = 0)
    {
        Host = host;
        OptimizationLevel = optimizationLevel;
    }

    public PyEnvironmentHost Host { get; }

    internal TextReader In => Host.In;
    internal TextWriter Out => Host.Out;
    internal TextWriter Error => Host.Error;
    internal Dictionary<string, PyModuleObject?> Modules { get; } = [];
    internal ConcurrentSet<Thread> Threads { get; } = [];
    internal List<string> Paths => Host.Paths;
    internal List<string> Args => Host.Args;
    internal int ExitCode { get; set; }
    internal event PyExitEventHandler? Exit;
    internal bool IsInteractive => Host.IsInteractive;
    internal IVirtualFileSystem FileSystem => Host.FileSystem;
    internal int OptimizationLevel { get; }

    public PyStrObject.InternPool InternPool { get; } = new();

    internal void OnExit()
    {
        var args = new PyExitEventArgs(ExitCode, null /* TODO: how to process this arg? */);
        Exit?.Invoke(args);

        foreach (var thread in Threads)
            // this Interrupt calling may be failed
            //
            // if the thread could not be interrupted,
            // just wait to stay consistent with CPython
            //
            thread.Interrupt();
        foreach (var thread in Threads)
            thread.Join();
    }

    public static IPyEnvironmentBuilder CreateBuilder()
    {
        return new PyEnvironmentBuilder();
    }

    public static PyEnvironment CreateNull()
    {
        return new PyEnvironment(PyEnvironmentHost.CreateNull());
    }

    public static PyEnvironment CreateConsole()
    {
        return new PyEnvironment(PyEnvironmentHost.CreateConsole());
    }
}

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
