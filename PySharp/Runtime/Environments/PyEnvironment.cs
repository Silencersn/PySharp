using PySharp.Compilation.AstNodes;
using PySharp.Modules.Builtins;
using PySharp.Runtime.IO;
using PySharp.Runtime.IO.Memory;
using PySharp.Utility;

namespace PySharp.Runtime.Environments;

public sealed partial class PyEnvironment
{
    public static PyEnvironment Console => CreateBuilder().StandardIO.WithConsole().Build();

    public static PyEnvironment Shared { get; }
    internal static PyEnvironment ParsingEnvironment { get; }

    static PyEnvironment()
    {
        Shared = new PyEnvironment();
        ParsingEnvironment = new PyEnvironment();
    }

    internal PyEnvironment(
        TextReader? stdin = null,
        TextWriter? stdout = null,
        TextWriter? stderr = null,
        bool isInteractive = false,
        IVirtualFileSystem? fileSystem = null,
        int optimizationLevel = 0)
    {
        In = stdin ?? TextReader.Null;
        Out = stdout ?? TextWriter.Null;
        Error = stderr ?? TextWriter.Null;
        Paths = [];
        Args = [];
        IsInteractive = isInteractive;
        FileSystem = fileSystem ?? MemoryFileSystem.CreateBuilder().Build();
        OptimizationLevel = optimizationLevel;
    }

    internal TextReader In { get; }
    internal TextWriter Out { get; }
    internal TextWriter Error { get; }
    internal Dictionary<string, PyModuleObject?> Modules { get; } = [];
    internal ConcurrentSet<Thread> Threads { get; } = [];
    internal List<string> Paths { get; }
    internal List<string> Args { get; }
    internal int ExitCode { get; set; }
    internal event PyExitEventHandler? Exit;
    internal bool IsInteractive { get; set; }
    internal IVirtualFileSystem FileSystem { get; }
    internal int OptimizationLevel { get; }

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
}
