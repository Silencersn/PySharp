using PySharp.AstNodes;
using PySharp.PyModules.Builtins;
using PySharp.PyRuntime.IO;
using PySharp.PyRuntime.IO.Memory;
using PySharp.Utility;
using System.Diagnostics;

namespace PySharp.PyRuntime.Environments;

public sealed partial class PyEnvironment
{
    public static PyEnvironment Console => CreateBuilder().StandardIO.WithConsole().Build();

    public static PyEnvironment Shared { get; }
    internal static PyEnvironment ParsingEnvironment { get; }

    static PyEnvironment()
    {
        Shared = new PyEnvironment();
        Shared.Init(PyEnvironmentOptions.Default);
        ParsingEnvironment = new PyEnvironment();
        ParsingEnvironment.Init(PyEnvironmentOptions.Default with { NotImplyImportSite = true });
    }

    internal PyEnvironment(
        TextReader? stdin = null,
        TextWriter? stdout = null,
        TextWriter? stderr = null,
        bool isInteractive = false,
        IVirtualFileSystem? fileSystem = null,
        OptimizationOptions? optimizationOptions = null)
    {
        In = stdin ?? TextReader.Null;
        Out = stdout ?? TextWriter.Null;
        Error = stderr ?? TextWriter.Null;
        Paths = [];
        Args = [];
        CurrentFrame = PyFrame.CreateModuleFrame(null);
        IsInteractive = isInteractive;
        FileSystem = fileSystem ?? MemoryFileSystem.CreateBuilder().Build();
        OptimizationOptions = optimizationOptions ?? OptimizationOptions.O0;
    }

    internal void Init(PyEnvironmentOptions options)
    {
        var builtins = LoadBuiltinModule("builtins");
        Debug.Assert(builtins is not null);
        CurrentFrame.SetValue(PySpecialNames.Builtins, builtins);
        CurrentFrame.SetValue(PySpecialNames.Name, PyStrObject.FromString(PySpecialNames.Main));
        if (!options.NotImplyImportSite)
        {
            var site = LoadBuiltinModule("site");
            Debug.Assert(site is not null);
        }
    }

    internal PyExceptionObject? CurrentError { get; set; }
    internal TextReader In { get; }
    internal TextWriter Out { get; }
    internal TextWriter Error { get; }
    internal Dictionary<string, PyModuleObject?> Modules { get; } = [];
    internal ConcurrentSet<Thread> Threads { get; } = [];

    private readonly ThreadLocal<PyFrame> _currentFrameThreadLocal = new();
    internal PyFrame CurrentFrame
    {
        get => _currentFrameThreadLocal.Value ?? throw new InvalidOperationException();
        set => _currentFrameThreadLocal.Value = value;
    }
    internal List<string> Paths { get; }
    internal List<string> Args { get; }
    internal int ExitCode { get; set; }
    internal event PyExitEventHandler? Exit;
    internal bool IsInteractive { get; set; }
    internal IVirtualFileSystem FileSystem { get; }
    internal OptimizationOptions OptimizationOptions { get; }

    internal void OnExit()
    {
        var args = new PyExitEventArgs(ExitCode, CurrentError);
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
