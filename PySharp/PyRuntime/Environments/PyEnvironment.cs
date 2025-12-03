using PySharp.AstNodes;
using PySharp.PyObjects.Builtins;
using PySharp.PyRuntime.IO;
using PySharp.PyRuntime.IO.Memory;
using PySharp.PyRuntime.IO.Physical;
using System.Diagnostics;

namespace PySharp.PyRuntime.Environments;

public sealed class PyEnvironmentContext : IDisposable
{
    private bool _disposed;
    private readonly PyEnvironment? _outerEnvironment;

    public PyEnvironmentContext(PyEnvironment? environment)
    {
        _outerEnvironment = PyVirtualMachine.PyEnvironmentAsyncLocal.Value;
        PyVirtualMachine.PyEnvironmentAsyncLocal.Value = environment;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        PyVirtualMachine.PyEnvironmentAsyncLocal.Value = _outerEnvironment;
    }
}

public sealed class PyEnvironment
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
        CurrentFrame = new PyFrame();
        IsInteractive = isInteractive;
        FileSystem = fileSystem ?? MemoryFileSystem.CreateBuilder().Build();
        OptimizationOptions = optimizationOptions ?? OptimizationOptions.O0;
    }

    internal void Init(PyEnvironmentOptions options)
    {
        ImportBuiltinModuleToFrame(CurrentFrame, "builtins", PySpecialNames.Builtins);
        CurrentFrame.SetValue(PySpecialNames.Name, PyStrObject.FromString(PySpecialNames.Main));
        if (!options.NotImplyImportSite)
            ImportBuiltinModuleToFrame(CurrentFrame, "site");
    }

    internal PyExceptionObject? CurrentError { get; set; }
    internal TextReader In { get; }
    internal TextWriter Out { get; }
    internal TextWriter Error { get; }
    internal Dictionary<string, PyModuleObject> Modules { get; } = [];
    internal PyFrame CurrentFrame { get; set; }
    internal List<string> Paths { get; }
    internal int ExitCode { get; set; }
    internal event PyExitEventHandler? Exit;
    internal bool IsInteractive { get; set; }
    internal IVirtualFileSystem FileSystem { get; }
    internal OptimizationOptions OptimizationOptions { get; }

    internal void OnExit()
    {
        var args = new PyExitEventArgs(ExitCode, CurrentError);
        Exit?.Invoke(args);
    }

    private void ImportBuiltinModuleToFrame(PyFrame frame, string name, string? alias = null)
    {
        if (!Modules.TryGetValue(name, out var module))
            module = PyStandardLibrary.TryCreateModule(name);
        Debug.Assert(module is not null);
        Modules[name] = module;
        module.OnImport(this);
        frame.SetValue(alias ?? name, module);
    }

    internal PyModuleObject? ImportModule(string name)
    {
        if (Modules.TryGetValue(name, out var module))
            return module;

        module = PyStandardLibrary.TryCreateModule(name);
        if (module is not null)
        {
            Modules[name] = module;
            module.OnImport(this);
            return module;
        }

        foreach (var path in Paths)
        {
            var filename = Path.Combine(path, $"{name}.py");
            if (!FileSystem.ExistsFile(filename))
                continue;

            var tokens = PyInterpreter.Tokenize(FileSystem.ReadAllText(filename));
            var node = PyInterpreter.Parse(tokens);
            module = PyVirtualMachine.ExecuteAstNodeWithinEnvironment(node, Path.GetFileNameWithoutExtension(filename));
            Modules[name] = module;
            module.OnImport(this);
            return module;
        }

        return null;
    }

    private readonly int _randomId = Random.Shared.Next();
    internal string IdWithThread => $"{Environment.CurrentManagedThreadId}-{_randomId}";

    public static IPyEnvironmentBuilder CreateBuilder()
    {
        return new PyEnvironmentBuilder(); 
    }
}
