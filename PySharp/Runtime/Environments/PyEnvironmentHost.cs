using PySharp.Runtime.IO;
using PySharp.Runtime.IO.Memory;
using PySharp.Runtime.IO.Physical;

namespace PySharp.Runtime.Environments;

public abstract class PyEnvironmentHost
{
    public abstract TextReader In { get; }
    public abstract TextWriter Out { get; }
    public abstract TextWriter Error { get; }

    public abstract IVirtualFileSystem FileSystem { get; }

    public abstract bool IsInteractive { get; }

    public abstract List<string> Paths { get; }
    public abstract List<string> Args { get; }

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
        public override TextReader In => TextReader.Null;
        public override TextWriter Out => TextWriter.Null;
        public override TextWriter Error => TextWriter.Null;
        public override IVirtualFileSystem FileSystem { get; } = MemoryFileSystem.CreateBuilder().Build();
        public override bool IsInteractive => true;
        public override List<string> Paths { get; } = [];
        public override List<string> Args { get; } = [];
    }

    private sealed class ConsolePyEnvironmentHost : PyEnvironmentHost
    {
        public override TextReader In => Console.In;
        public override TextWriter Out => Console.Out;
        public override TextWriter Error => Console.Error;
        public override IVirtualFileSystem FileSystem { get; } = MemoryFileSystem.CreateBuilder().Build();
        public override bool IsInteractive => false;
        public override List<string> Paths { get; } = [];
        public override List<string> Args { get; } = [];
    }

    private sealed class PhysicalConsolePyEnvironmentHost : PyEnvironmentHost
    {
        public override TextReader In => Console.In;
        public override TextWriter Out => Console.Out;
        public override TextWriter Error => Console.Error;
        public override IVirtualFileSystem FileSystem { get; } = PhysicalFileSystem.Shared;
        public override bool IsInteractive => false;
        public override List<string> Paths { get; } = [];
        public override List<string> Args { get; } = [];
    }

    private sealed class ReplPyEnvironmentHost : PyEnvironmentHost
    {
        public override TextReader In => Console.In;
        public override TextWriter Out => Console.Out;
        public override TextWriter Error => Console.Error;
        public override IVirtualFileSystem FileSystem { get; } = MemoryFileSystem.CreateBuilder().Build();
        public override bool IsInteractive => true;
        public override List<string> Paths { get; } = [];
        public override List<string> Args { get; } = [];
    }
}
