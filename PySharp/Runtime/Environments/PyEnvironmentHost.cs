using PySharp.Runtime.IO;
using PySharp.Runtime.IO.Memory;
using PySharp.Runtime.IO.Physical;
using System.Text;

namespace PySharp.Runtime.Environments;

public abstract class PyEnvironmentHost
{
    // Moved from PyFileObject.Utf8NoBom, cached so that it is not recreated on each access.
    internal static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

    // Default encoding used for stdio wrappers; subclasses may override it.
    public virtual Encoding DefaultEncoding => Utf8NoBom;

    public virtual bool SupportsColorOutput => true;

    public virtual IPyEnvironmentBuilder CreateEnvironmentBuilder()
    {
        return new PyEnvironmentBuilder(this);
    }

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

    internal abstract class ConsolePyEnvironmentHostBase : PyEnvironmentHost
    {
        // stdio encoding handover seam; tests override these to inject a
        // deterministic stand-in for Console.OutputEncoding
        internal virtual Encoding StdOutEncoding => Console.OutputEncoding;
        internal virtual Encoding StdErrEncoding => Console.OutputEncoding;

        public override Stream AllocateStdIn() => Console.OpenStandardInput();
        public override Stream AllocateStdOut() => Console.OpenStandardOutput();
        public override Stream AllocateStdErr() => Console.OpenStandardError();

        public override IPyEnvironmentBuilder CreateEnvironmentBuilder()
        {
            return base.CreateEnvironmentBuilder()
                .UseStdInEncoding(Console.InputEncoding)
                .UseStdOutEncoding(WithoutBom(StdOutEncoding))
                .UseStdErrEncoding(WithoutBom(StdErrEncoding));
        }

        // Console.OutputEncoding may hand over the BOM-emitting UTF8
        // singleton; CPython never writes a preamble to stdout/stderr.
        // Non-UTF-8 console code pages pass through unchanged.
        internal static Encoding WithoutBom(Encoding encoding) =>
            encoding.GetPreamble() is [0xEF, 0xBB, 0xBF]
                ? Utf8NoBom
                : encoding;
    }

    private sealed class ConsolePyEnvironmentHost : ConsolePyEnvironmentHostBase
    {
        public override IVirtualFileSystem FileSystem { get; } = MemoryFileSystem.CreateBuilder().Build();
    }

    private sealed class PhysicalConsolePyEnvironmentHost : ConsolePyEnvironmentHostBase
    {
        public override IVirtualFileSystem FileSystem { get; } = PhysicalFileSystem.Shared;
    }

    private sealed class ReplPyEnvironmentHost : ConsolePyEnvironmentHostBase
    {
        public override IVirtualFileSystem FileSystem { get; } = MemoryFileSystem.CreateBuilder().Build();
    }
}
