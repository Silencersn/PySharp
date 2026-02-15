using PySharp.Runtime.IO;
using PySharp.Runtime.IO.Memory;
using PySharp.Runtime.IO.Physical;

namespace PySharp.Runtime.Environments;

internal sealed class PyEnvironmentBuilder :
    IPyEnvironmentBuilder,
    IPyEnvironmentStandardIOBuilder,
    IPyEnvironmentFileSystemBuilder,
    IPyEnvironmentInterpreterModeBuilder,
    IPyEnvironmentInitializationBuilder,
    IPyEnvironmentSystemBuilder
{
    private TextReader _stdin;
    private TextWriter _stdout;
    private TextWriter _stderr;
    private IVirtualFileSystem? _fileSystem;
    private bool _isInteractive;

    private bool _syncExit;
    private bool _importSite;
    private readonly List<string> _paths;
    private readonly List<string> _args;

    internal PyEnvironmentBuilder()
    {
        _stdin = TextReader.Null;
        _stdout = TextWriter.Null;
        _stderr = TextWriter.Null;
        _fileSystem = null;
        _isInteractive = false;

        _syncExit = false;
        _importSite = true;
        _paths = [];
        _args = [];
    }

    public IPyEnvironmentStandardIOBuilder StandardIO => this;
    public IPyEnvironmentFileSystemBuilder FileSystem => this;
    public IPyEnvironmentInterpreterModeBuilder InterpreterMode => this;
    public IPyEnvironmentInitializationBuilder Initialization => this;
    public IPyEnvironmentSystemBuilder System => this;

    public PyEnvironment Build()
    {
        var environment = new PyEnvironment(
            stdin: _stdin,
            stdout: _stdout,
            stderr: _stderr,
            fileSystem: _fileSystem,
            isInteractive: _isInteractive);

        var options = new PyEnvironmentOptions()
        {
            NotImplyImportSite = !_importSite,
        };

        if (_syncExit)
            environment.Exit += static args => Environment.Exit(args.ExitCode);

        environment.Paths.AddRange(_paths);
        environment.Args.AddRange(_args);

        return environment;
    }

    IPyEnvironmentStandardIOBuilder IPyEnvironmentStandardIOBuilder.WithInput(TextReader? input)
    {
        _stdin = input ?? TextReader.Null;
        return this;
    }
    IPyEnvironmentStandardIOBuilder IPyEnvironmentStandardIOBuilder.WithOutput(TextWriter? output)
    {
        _stdout = output ?? TextWriter.Null;
        return this;
    }
    IPyEnvironmentStandardIOBuilder IPyEnvironmentStandardIOBuilder.WithError(TextWriter? error)
    {
        _stderr = error ?? TextWriter.Null;
        return this;
    }

    IPyEnvironmentBuilder IPyEnvironmentFileSystemBuilder.WithPhysicalFileSystem()
    {
        _fileSystem = PhysicalFileSystem.Shared;
        return this;
    }

    IPyEnvironmentBuilder IPyEnvironmentFileSystemBuilder.WithMemoryFileSystem(MemoryFileSystem? memoryFileSystem)
    {
        _fileSystem = memoryFileSystem;
        return this;
    }

    IPyEnvironmentBuilder IPyEnvironmentInterpreterModeBuilder.Default()
    {
        _isInteractive = false;
        return this;
    }

    IPyEnvironmentBuilder IPyEnvironmentInterpreterModeBuilder.Interactive()
    {
        _isInteractive = true;
        return this;
    }

    IPyEnvironmentInitializationBuilder IPyEnvironmentInitializationBuilder.SyncExit()
    {
        _syncExit = true;
        return this;
    }

    IPyEnvironmentInitializationBuilder IPyEnvironmentInitializationBuilder.NotImplyImportSite()
    {
        _importSite = false;
        return this;
    }

    IPyEnvironmentSystemBuilder IPyEnvironmentSystemBuilder.AppendSysPath(string? path)
    {
        if (path is not null)
            _paths.Add(path);
        return this;
    }

    IPyEnvironmentSystemBuilder IPyEnvironmentSystemBuilder.AppendArgument(string? arg)
    {
        if (arg is not null)
            _args.Add(arg);
        return this;
    }
}



public interface IPyEnvironmentBuilder
{
    IPyEnvironmentStandardIOBuilder StandardIO { get; }
    IPyEnvironmentFileSystemBuilder FileSystem { get; }
    IPyEnvironmentInterpreterModeBuilder InterpreterMode { get; }
    IPyEnvironmentInitializationBuilder Initialization { get; }
    IPyEnvironmentSystemBuilder System { get; }

    PyEnvironment Build();
}

public interface IPyEnvironmentStandardIOBuilder : IPyEnvironmentBuilder
{
    IPyEnvironmentStandardIOBuilder WithInput(TextReader? input);
    IPyEnvironmentStandardIOBuilder WithOutput(TextWriter? output);
    IPyEnvironmentStandardIOBuilder WithError(TextWriter? error);

    IPyEnvironmentStandardIOBuilder WithConsoleInput() => WithInput(Console.In);
    IPyEnvironmentStandardIOBuilder WithConsoleOutput() => WithOutput(Console.Out);
    IPyEnvironmentStandardIOBuilder WithConsoleError() => WithError(Console.Error);
    IPyEnvironmentStandardIOBuilder WithConsole() => WithConsoleInput().WithConsoleOutput().WithConsoleError();
}

public interface IPyEnvironmentFileSystemBuilder : IPyEnvironmentBuilder
{
    IPyEnvironmentBuilder WithPhysicalFileSystem();
    IPyEnvironmentBuilder WithMemoryFileSystem(MemoryFileSystem? memoryFileSystem);
    IPyEnvironmentBuilder WithEmptyMemoryFileSystem() => WithMemoryFileSystem(MemoryFileSystem.CreateBuilder().Build());
}

public interface IPyEnvironmentInterpreterModeBuilder : IPyEnvironmentBuilder
{
    IPyEnvironmentBuilder Default();
    IPyEnvironmentBuilder Interactive();
}

public interface IPyEnvironmentInitializationBuilder : IPyEnvironmentBuilder
{
    IPyEnvironmentInitializationBuilder SyncExit();
    IPyEnvironmentInitializationBuilder NotImplyImportSite();
}

public interface IPyEnvironmentSystemBuilder : IPyEnvironmentBuilder
{
    IPyEnvironmentSystemBuilder AppendSysPath(string? path);
    IPyEnvironmentSystemBuilder AppendSysPaths(IEnumerable<string?>? paths)
    {
        foreach (var path in paths ?? [])
            AppendSysPath(path);
        return this;
    }
    IPyEnvironmentSystemBuilder AppendArgument(string? arg);
    IPyEnvironmentSystemBuilder AppendArguments(IEnumerable<string?>? args)
    {
        foreach (var arg in args ?? [])
            AppendArgument(arg);
        return this;
    }
}