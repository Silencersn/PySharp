using PySharp.Runtime.IO;
using PySharp.Runtime.IO.Memory;
using PySharp.Runtime.IO.Physical;

namespace PySharp.Runtime.Environments;

internal sealed class PyEnvironmentHostBuilder : IPyEnvironmentHostBuilder
{
    private TextReader _in = TextReader.Null;
    private TextWriter _out = TextWriter.Null;
    private TextWriter _error = TextWriter.Null;
    private IVirtualFileSystem? _fileSystem;
    private bool _isInteractive;
    private readonly List<string> _paths = [];
    private readonly List<string> _args = [];

    public IPyEnvironmentHostBuilder UseIn(TextReader reader)
    {
        _in = reader;
        return this;
    }

    public IPyEnvironmentHostBuilder UseOut(TextWriter writer)
    {
        _out = writer;
        return this;
    }

    public IPyEnvironmentHostBuilder UseError(TextWriter writer)
    {
        _error = writer;
        return this;
    }

    public IPyEnvironmentHostBuilder UseFileSystem(IVirtualFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
        return this;
    }

    public IPyEnvironmentHostBuilder SetInteractive(bool isInteractive)
    {
        _isInteractive = isInteractive;
        return this;
    }

    public IPyEnvironmentHostBuilder AddPath(string path)
    {
        _paths.Add(path);
        return this;
    }

    public IPyEnvironmentHostBuilder AddArg(string arg)
    {
        _args.Add(arg);
        return this;
    }

    public PyEnvironmentHost Build()
    {
        return new ConfigurablePyEnvironmentHost(
            _in,
            _out,
            _error,
            _fileSystem ?? MemoryFileSystem.CreateBuilder().Build(),
            _isInteractive,
            [.. _paths],
            [.. _args]);
    }

    private sealed class ConfigurablePyEnvironmentHost(
        TextReader cin,
        TextWriter cout,
        TextWriter cerr,
        IVirtualFileSystem fileSystem,
        bool isInteractive,
        List<string> paths,
        List<string> args) : PyEnvironmentHost
    {
        public override TextReader In => cin;
        public override TextWriter Out => cout;
        public override TextWriter Error => cerr;
        public override IVirtualFileSystem FileSystem => fileSystem;
        public override bool IsInteractive => isInteractive;
        public override List<string> Paths { get; } = paths;
        public override List<string> Args { get; } = args;
    }
}

public interface IPyEnvironmentHostBuilder
{
    IPyEnvironmentHostBuilder UseIn(TextReader reader);
    IPyEnvironmentHostBuilder UseOut(TextWriter writer);
    IPyEnvironmentHostBuilder UseError(TextWriter writer);
    IPyEnvironmentHostBuilder UseFileSystem(IVirtualFileSystem fileSystem);
    IPyEnvironmentHostBuilder SetInteractive(bool isInteractive);
    IPyEnvironmentHostBuilder AddPath(string path);
    IPyEnvironmentHostBuilder AddArg(string arg);
    PyEnvironmentHost Build();
}
