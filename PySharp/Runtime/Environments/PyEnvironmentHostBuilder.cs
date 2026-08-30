using PySharp.Runtime.IO;
using PySharp.Runtime.IO.Memory;

namespace PySharp.Runtime.Environments;

internal sealed class PyEnvironmentHostBuilder : IPyEnvironmentHostBuilder
{
    private Stream _in = Stream.Null;
    private Stream _out = Stream.Null;
    private Stream _error = Stream.Null;
    private IVirtualFileSystem? _fileSystem;

    public IPyEnvironmentHostBuilder UseIn(Stream reader)
    {
        _in = reader;
        return this;
    }

    public IPyEnvironmentHostBuilder UseOut(Stream writer)
    {
        _out = writer;
        return this;
    }

    public IPyEnvironmentHostBuilder UseError(Stream writer)
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
        Stream cin,
        Stream cout,
        Stream cerr,
        IVirtualFileSystem fileSystem) : PyEnvironmentHost
    {
        public override Stream AllocateStdIn() => cin;
        public override Stream AllocateStdOut() => cout;
        public override Stream AllocateStdErr() => cerr;
        public override IVirtualFileSystem FileSystem => fileSystem;
    }
}

public interface IPyEnvironmentHostBuilder
{
    IPyEnvironmentHostBuilder UseIn(Stream reader);
    IPyEnvironmentHostBuilder UseOut(Stream writer);
    IPyEnvironmentHostBuilder UseError(Stream writer);
    IPyEnvironmentHostBuilder UseFileSystem(IVirtualFileSystem fileSystem);
    PyEnvironmentHost Build();
}
