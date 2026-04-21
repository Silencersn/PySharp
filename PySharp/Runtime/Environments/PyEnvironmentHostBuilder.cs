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

    public PyEnvironmentHost Build()
    {
        return new ConfigurablePyEnvironmentHost(
            _in,
            _out,
            _error,
            _fileSystem ?? MemoryFileSystem.CreateBuilder().Build());
    }

    private sealed class ConfigurablePyEnvironmentHost(
        TextReader cin,
        TextWriter cout,
        TextWriter cerr,
        IVirtualFileSystem fileSystem) : PyEnvironmentHost
    {
        public override TextReader AllocateStdIn() => cin;
        public override TextWriter AllocateStdOut() => cout;
        public override TextWriter AllocateStdErr() => cerr;
        public override IVirtualFileSystem FileSystem => fileSystem;
    }
}

public interface IPyEnvironmentHostBuilder
{
    IPyEnvironmentHostBuilder UseIn(TextReader reader);
    IPyEnvironmentHostBuilder UseOut(TextWriter writer);
    IPyEnvironmentHostBuilder UseError(TextWriter writer);
    IPyEnvironmentHostBuilder UseFileSystem(IVirtualFileSystem fileSystem);
    PyEnvironmentHost Build();
}
