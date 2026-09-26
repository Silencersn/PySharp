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

    // Hosts without terminal state keep the legacy opt-in behavior; the
    // console host decides error coloring per stream like CPython does.
    public virtual bool SupportsErrorColorOutput => SupportsColorOutput;

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

    /// <summary>
    /// Host for the test suite's in-process fixture runner: identical to the
    /// console host except that stdin is already at EOF. A fixture that reads
    /// <c>sys.stdin</c> or calls <c>input()</c> then sees end-of-input, which
    /// is what a non-interactive script run means and what the subprocess-based
    /// CPython comparison layer already provides (it closes the child's stdin).
    /// Without this, such a fixture would block forever on the test host's own
    /// standard input instead of reaching the EOF its assertions describe.
    /// </summary>
    internal static PyEnvironmentHost CreateFixtureRunner()
    {
        return new FixtureRunnerPyEnvironmentHost();
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

        // terminal-state and environment seams for the color decision;
        // tests override them instead of touching process state
        internal virtual bool StdOutRedirected => Console.IsOutputRedirected;
        internal virtual bool StdErrRedirected => Console.IsErrorRedirected;
        internal virtual Func<string, string?> ColorEnvironment => System.Environment.GetEnvironmentVariable;

        public override bool SupportsColorOutput =>
            PyColorSupport.EnvironmentAllowsColor(ColorEnvironment) ?? !StdOutRedirected;

        public override bool SupportsErrorColorOutput =>
            PyColorSupport.EnvironmentAllowsColor(ColorEnvironment) ?? !StdErrRedirected;

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

    private sealed class FixtureRunnerPyEnvironmentHost : ConsolePyEnvironmentHostBase
    {
        // Stream.Null reads as an immediate EOF and discards writes; stdin
        // never blocks on the test host's real standard input.
        public override Stream AllocateStdIn() => Stream.Null;
        public override IVirtualFileSystem FileSystem { get; } = PhysicalFileSystem.Shared;
    }
}
