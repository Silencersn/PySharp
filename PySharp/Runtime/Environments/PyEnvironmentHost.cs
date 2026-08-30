using PySharp.Runtime.IO;
using PySharp.Runtime.IO.Memory;
using PySharp.Runtime.IO.Physical;

namespace PySharp.Runtime.Environments;

public abstract class PyEnvironmentHost
{
    public abstract Stream AllocateStdIn();
    public abstract Stream AllocateStdOut();
    public abstract Stream AllocateStdErr();

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
        public override Stream AllocateStdIn() => Stream.Null;
        public override Stream AllocateStdOut() => Stream.Null;
        public override Stream AllocateStdErr() => Stream.Null;
        public override IVirtualFileSystem FileSystem { get; } = MemoryFileSystem.CreateBuilder().Build();
    }

    private sealed class ConsolePyEnvironmentHost : PyEnvironmentHost
    {
        public override Stream AllocateStdIn() => Console.OpenStandardInput();
        public override Stream AllocateStdOut() => Console.OpenStandardOutput();
        public override Stream AllocateStdErr() => Console.OpenStandardError();
        public override IVirtualFileSystem FileSystem { get; } = MemoryFileSystem.CreateBuilder().Build();
    }

    private sealed class PhysicalConsolePyEnvironmentHost : PyEnvironmentHost
    {
        public override Stream AllocateStdIn() => Console.OpenStandardInput();
        public override Stream AllocateStdOut() => Console.OpenStandardOutput();
        public override Stream AllocateStdErr() => Console.OpenStandardError();
        public override IVirtualFileSystem FileSystem { get; } = PhysicalFileSystem.Shared;
    }

    private sealed class ReplPyEnvironmentHost : PyEnvironmentHost
    {
        public override Stream AllocateStdIn() => Console.OpenStandardInput();
        public override Stream AllocateStdOut() => Console.OpenStandardOutput();
        public override Stream AllocateStdErr() => Console.OpenStandardError();
        public override IVirtualFileSystem FileSystem { get; } = MemoryFileSystem.CreateBuilder().Build();
    }
}
