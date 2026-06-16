using PySharp.Modules.Builtins;
using PySharp.Runtime.IO;
using PySharp.Utility;

namespace PySharp.Runtime.Environments;

public sealed partial class PyEnvironment : IDisposable
{
    private readonly TextReader _in;
    private readonly TextWriter _out;
    private readonly TextWriter _error;
    private readonly bool _isInteractive;
    private readonly List<string> _paths;
    private readonly List<string> _args;
    private bool _disposed;

    static PyEnvironment()
    {
    }

    internal PyEnvironment(
        PyEnvironmentHost host,
        bool isInteractive = false,
        IEnumerable<string>? paths = null,
        IEnumerable<string>? args = null,
        int optimizationLevel = 0)
    {
        Host = host;
        _in = host.AllocateStdIn();
        _out = host.AllocateStdOut();
        _error = host.AllocateStdErr();
        _isInteractive = isInteractive;
        _paths = paths is null ? [] : [.. paths];
        _args = args is null ? [] : [.. args];
        OptimizationLevel = optimizationLevel;
        ModuleProviders = [BuiltinModuleProvider.Shared, PathProvider.Shared];
    }

    public PyEnvironmentHost Host { get; }

    internal TextReader In => _in;
    internal TextWriter Out => _out;
    internal TextWriter Error => _error;
    internal Dictionary<string, PyModuleObject?> Modules { get; } = [];
    internal ConcurrentSet<Thread> Threads { get; } = [];
    internal List<string> Paths => _paths;
    internal List<string> Args => _args;
    internal List<PyModuleProvider> ModuleProviders { get; }
    internal int ExitCode { get; set; }
    internal event PyExitEventHandler? Exit;
    internal bool IsInteractive => _isInteractive;
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

        _in.Dispose();
        _out.Dispose();
        _error.Dispose();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        OnExit();
    }

    public static IPyEnvironmentBuilder CreateBuilder(PyEnvironmentHost host)
    {
        ArgumentNullException.ThrowIfNull(host);

        return new PyEnvironmentBuilder(host);
    }

    public static PyEnvironment CreateNull()
    {
        return new PyEnvironment(PyEnvironmentHost.CreateNull(), isInteractive: true);
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
