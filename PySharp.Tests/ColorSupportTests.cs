using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.Environments;
using PySharp.Runtime.IO;
using PySharp.Runtime.IO.Memory;
using System.Text;

namespace PySharp.Tests;

[TestClass]
public sealed class ColorSupportTests
{
    private static Func<string, string?> Env(params (string Key, string? Value)[] pairs)
    {
        var map = pairs.ToDictionary(p => p.Key, p => p.Value);
        return key => map.TryGetValue(key, out var value) ? value : null;
    }

    private sealed class TestColorHost(
        Stream error,
        Func<string, string?>? getenv = null,
        bool stderrRedirected = true) : PyEnvironmentHost.ConsolePyEnvironmentHostBase
    {
        public override Stream AllocateStdIn() => Stream.Null;
        public override Stream AllocateStdOut() => Stream.Null;
        public override Stream AllocateStdErr() => error;
        public override IVirtualFileSystem FileSystem { get; } =
            MemoryFileSystem.CreateBuilder().Build();
        internal override bool StdErrRedirected => stderrRedirected;
        internal override Func<string, string?> ColorEnvironment =>
            getenv ?? System.Environment.GetEnvironmentVariable;
    }

    [TestMethod]
    public void ColorEnvironment_DecidesBeforeTheTerminal()
    {
        // order mirrors CPython Lib/_colorize.py can_colorize
        Assert.IsNull(PyColorSupport.EnvironmentAllowsColor(Env()));
        Assert.IsFalse(PyColorSupport.EnvironmentAllowsColor(Env(("PYTHON_COLORS", "0"))));
        Assert.IsTrue(PyColorSupport.EnvironmentAllowsColor(Env(("PYTHON_COLORS", "1"))));

        // only the exact strings "0"/"1" count
        Assert.IsNull(PyColorSupport.EnvironmentAllowsColor(Env(("PYTHON_COLORS", "yes"))));

        // empty NO_COLOR/FORCE_COLOR count as unset
        Assert.IsNull(PyColorSupport.EnvironmentAllowsColor(Env(("NO_COLOR", ""))));
        Assert.IsFalse(PyColorSupport.EnvironmentAllowsColor(Env(("NO_COLOR", "1"))));
        Assert.IsTrue(PyColorSupport.EnvironmentAllowsColor(Env(("FORCE_COLOR", "1"))));

        Assert.IsFalse(PyColorSupport.EnvironmentAllowsColor(Env(("TERM", "dumb"))));
        Assert.IsNull(PyColorSupport.EnvironmentAllowsColor(Env(("TERM", "xterm"))));

        // precedence: PYTHON_COLORS > NO_COLOR > FORCE_COLOR > TERM
        Assert.IsFalse(PyColorSupport.EnvironmentAllowsColor(
            Env(("PYTHON_COLORS", "0"), ("FORCE_COLOR", "1"))));
        Assert.IsTrue(PyColorSupport.EnvironmentAllowsColor(
            Env(("PYTHON_COLORS", "1"), ("NO_COLOR", "1"))));
        Assert.IsTrue(PyColorSupport.EnvironmentAllowsColor(
            Env(("FORCE_COLOR", "1"), ("TERM", "dumb"))));
    }

    [TestMethod]
    public void ConsoleHost_RedirectedStderr_DisablesColorUntilEnvForcesIt()
    {
        var host = new TestColorHost(new MemoryStream());
        Assert.IsFalse(host.SupportsErrorColorOutput);

        var forcedHost = new TestColorHost(new MemoryStream(), Env(("PYTHON_COLORS", "1")));
        Assert.IsTrue(forcedHost.SupportsErrorColorOutput);

        // a live (non-redirected) stderr keeps colors with a neutral environment
        var ttyHost = new TestColorHost(new MemoryStream(), stderrRedirected: false);
        Assert.IsTrue(ttyHost.SupportsErrorColorOutput);
    }

    [TestMethod]
    public void UncaughtTraceback_RedirectedStderr_IsPlainText()
    {
        var error = new MemoryStream();
        var host = new TestColorHost(error);

        using var environment = host.CreateEnvironmentBuilder().Build();
        using var context = PyCallContext.CreateInterpreterRootContext(environment);
        PyInterpreter.PyTryCatch(context, () =>
            PyInterpreter.RunCodeWithContext(context, "raise ValueError('boom')", "<module>", "<color>", isMain: true));

        Assert.AreEqual(1, environment.ExitCode);
        var text = Encoding.UTF8.GetString(error.ToArray());
        Assert.IsFalse(text.Contains('\x1b'), $"unexpected ANSI escape: {text}");
        StringAssert.StartsWith(text, "Traceback");
        StringAssert.Contains(text, "ValueError: boom");
    }

    [TestMethod]
    public void UncaughtTraceback_RedirectedStderr_ExplicitKnobStillColorizes()
    {
        var error = new MemoryStream();
        var host = new TestColorHost(error);

        using var environment = host.CreateEnvironmentBuilder().UseStdErrColorSupport(true).Build();
        using var context = PyCallContext.CreateInterpreterRootContext(environment);
        PyInterpreter.PyTryCatch(context, () =>
            PyInterpreter.RunCodeWithContext(context, "raise ValueError('boom')", "<module>", "<color>", isMain: true));

        Assert.AreEqual(1, environment.ExitCode);
        var text = Encoding.UTF8.GetString(error.ToArray());
        StringAssert.Contains(text, "\x1b[31m");
        StringAssert.Contains(text, "ValueError: boom");
    }
}
