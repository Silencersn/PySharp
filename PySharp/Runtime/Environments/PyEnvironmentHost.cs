using PySharp.Runtime.IO;
using PySharp.Runtime.IO.Memory;
using PySharp.Runtime.IO.Physical;

namespace PySharp.Runtime.Environments;

public abstract class PyEnvironmentHost
{
    public abstract TextReader AllocateStdIn();
    public abstract TextWriter AllocateStdOut();
    public abstract TextWriter AllocateStdErr();

    public abstract IVirtualFileSystem FileSystem { get; }

    public static PyEnvironmentHost CreateNull()
    {
        return new NullPyEnvironmentHost();
    }
    public static PyEnvironmentHost CreateConsole(bool usingPhysicalFileSystem = false)
    {
        return usingPhysicalFileSystem
            ? new PhysicalConsolePyEnvironmentHost()
            : new ConsolePyEnvironmentHost();
    }
    public static PyEnvironmentHost CreateRepl()
    {
        return new ReplPyEnvironmentHost();
    }

    public static IPyEnvironmentHostBuilder CreateBuilder()
    {
        return new PyEnvironmentHostBuilder();
    }

    private sealed class NullPyEnvironmentHost : PyEnvironmentHost
    {
        public override TextReader AllocateStdIn() => TextReader.Null;
        public override TextWriter AllocateStdOut() => TextWriter.Null;
        public override TextWriter AllocateStdErr() => TextWriter.Null;
        public override IVirtualFileSystem FileSystem { get; } = MemoryFileSystem.CreateBuilder().Build();
    }

    private sealed class ConsolePyEnvironmentHost : PyEnvironmentHost
    {
        public override TextReader AllocateStdIn() => Console.In;
        public override TextWriter AllocateStdOut() => Console.Out;
        public override TextWriter AllocateStdErr() => Console.Error;
        public override IVirtualFileSystem FileSystem { get; } = MemoryFileSystem.CreateBuilder().Build();
    }

    private sealed class PhysicalConsolePyEnvironmentHost : PyEnvironmentHost
    {
        public override TextReader AllocateStdIn() => Console.In;
        public override TextWriter AllocateStdOut() => Console.Out;
        public override TextWriter AllocateStdErr() => Console.Error;
        public override IVirtualFileSystem FileSystem { get; } = PhysicalFileSystem.Shared;
    }

    private sealed class ReplPyEnvironmentHost : PyEnvironmentHost
    {
        public override TextReader AllocateStdIn() => Console.In;
        public override TextWriter AllocateStdOut() => Console.Out;
        public override TextWriter AllocateStdErr() => Console.Error;
        public override IVirtualFileSystem FileSystem { get; } = MemoryFileSystem.CreateBuilder().Build();
    }
}
