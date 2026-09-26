using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.Environments;
using PySharp.Runtime.IO;
using PySharp.Runtime.IO.Memory;
using System.Text;

namespace PySharp.Tests;

// CPython accumulates a traceback as the exception travels: it restores the
// exception's own traceback in the throw machinery (PyErr_Restore with
// PyException_GetTraceback) and every frame the exception then passes through
// prepends its own entry (PyTraceback_Here). An exception thrown into a
// suspended generator is therefore recorded at the suspension point even when
// the generator has no handler that could catch it — the frame the injected
// exception is raised in ends up on the traceback like any other.
//
// Every expectation below was measured against CPython 3.14.4 running the
// same source; the frame lines are asserted together with the source line
// CPython prints beneath them, so a frame recorded at the wrong suspension
// point fails too.
[TestClass]
public sealed class TracebackTests
{
    private const string GeneratorEscape = """
        def gen():
            value = yield 1
            print(value)


        g = gen()
        next(g)
        g.throw(ValueError('boom'))
        """;

    private const string YieldFromGenerator = """
        def sub():
            yield 1


        def delegating():
            yield from sub()


        g = delegating()
        next(g)
        g.throw(ValueError('boom'))
        """;

    private const string YieldFromIterator = """
        def delegating():
            yield from [1, 2, 3]


        g = delegating()
        next(g)
        g.throw(ValueError('boom'))
        """;

    private const string CoroutineAwait = """
        class Awaitable:
            def __await__(self):
                yield 1


        async def coro():
            await Awaitable()


        c = coro()
        c.send(None)
        c.throw(ValueError('boom'))
        """;

    private const string NeverStarted = """
        def gen():
            value = yield 1
            print(value)


        g = gen()
        g.throw(ValueError('boom'))
        """;

    // the thrown exception was raised once before, so it already carries a
    // traceback: CPython keeps it as the tail behind the throw frames
    private const string AlreadyHasTraceback = """
        def gen():
            value = yield 1


        e = ValueError('boom')
        try:
            raise e
        except ValueError:
            pass

        g = gen()
        next(g)
        g.throw(e)
        """;

    // the generator catches the injected exception itself: the frame was
    // recorded before this fix too, and must stay recorded once
    private const string GeneratorHandler = """
        def gen():
            try:
                yield 1
            except ValueError:
                raise


        g = gen()
        next(g)
        g.throw(ValueError('boom'))
        """;

    // A redirected stderr prints plain text, the CPython behavior the
    // expected frames below were measured with; the process environment is
    // not consulted, so a NO_COLOR/FORCE_COLOR in the test runner cannot
    // change the output.
    private sealed class StderrHost(Stream error) : PyEnvironmentHost.ConsolePyEnvironmentHostBase
    {
        public override Stream AllocateStdIn() => Stream.Null;
        public override Stream AllocateStdOut() => Stream.Null;
        public override Stream AllocateStdErr() => error;
        public override IVirtualFileSystem FileSystem { get; } = MemoryFileSystem.CreateBuilder().Build();
        internal override bool StdErrRedirected => true;
        internal override Func<string, string?> ColorEnvironment => _ => null;
    }

    private static string RunCapturingStderr(string code)
    {
        var error = new MemoryStream();
        var host = new StderrHost(error);

        using var environment = host.CreateEnvironmentBuilder().Build();
        using var context = PyCallContext.CreateInterpreterRootContext(environment);
        PyInterpreter.PyTryCatch(context, () =>
            PyInterpreter.RunCodeWithContext(context, code, "<module>", "<traceback>", isMain: true));

        Assert.AreEqual(1, environment.ExitCode, "the uncaught exception must set the exit code");
        return Encoding.UTF8.GetString(error.ToArray());
    }

    // Paths whose diagnostics go to stderr without failing the run (the
    // unraisable channel reports and continues) still need the exit code zeroed,
    // so this leaves the code unasserted.
    private static string RunCapturingStderrUnchecked(string code)
    {
        var error = new MemoryStream();
        var host = new StderrHost(error);

        using var environment = host.CreateEnvironmentBuilder().Build();
        using var context = PyCallContext.CreateInterpreterRootContext(environment);
        PyInterpreter.PyTryCatch(context, () =>
            PyInterpreter.RunCodeWithContext(context, code, "<module>", "<traceback>", isMain: true));

        Assert.AreEqual(0, environment.ExitCode, "the unraisable report must not fail the run");
        return Encoding.UTF8.GetString(error.ToArray());
    }

    // "line N in caller: source" per frame, in print order. A traceback ends
    // with a blank line that CPython does not print (issue #349), which
    // TrimEnd keeps out of the comparison.
    private static string[] FrameTrace(string stderr)
    {
        var lines = stderr.Replace("\r\n", "\n").TrimEnd('\n').Split('\n');
        var frames = new List<string>();
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (!line.StartsWith("  File \"", StringComparison.Ordinal))
                continue;

            const string LineMarker = ", line ";
            const string CallerMarker = ", in ";
            var lineAt = line.IndexOf(LineMarker, StringComparison.Ordinal);
            var callerAt = line.IndexOf(CallerMarker, StringComparison.Ordinal);
            Assert.IsTrue(lineAt > 0 && callerAt > lineAt, $"unparsable frame line: {line}");

            var number = line[(lineAt + LineMarker.Length)..callerAt];
            var caller = line[(callerAt + CallerMarker.Length)..];
            frames.Add($"line {number} in {caller}: {lines[i + 1].Trim()}");
        }
        return [.. frames];
    }

    private static void AssertFrames(string[] expected, string code)
    {
        var stderr = RunCapturingStderr(code);
        CollectionAssert.AreEqual(expected, FrameTrace(stderr), stderr);
        StringAssert.EndsWith(stderr.TrimEnd('\r', '\n'), "ValueError: boom", stderr);
    }

    [TestMethod]
    public void ThrowIntoGeneratorWithoutHandler_RecordsFrameAtTheYield()
    {
        AssertFrames([
            "line 8 in <module>: g.throw(ValueError('boom'))",
            "line 2 in gen: value = yield 1",
        ], GeneratorEscape);
    }

    [TestMethod]
    public void ThrowThroughYieldFrom_RecordsEveryDelegatingFrame()
    {
        AssertFrames([
            "line 11 in <module>: g.throw(ValueError('boom'))",
            "line 6 in delegating: yield from sub()",
            "line 2 in sub: yield 1",
        ], YieldFromGenerator);
    }

    [TestMethod]
    public void ThrowThroughYieldFromIterator_RecordsTheDelegatingFrame()
    {
        // the delegate has no throw() of its own, so the exception is raised
        // in the delegating frame itself: CPython falls back to throw_here and
        // re-enters that frame with the pending exception
        AssertFrames([
            "line 7 in <module>: g.throw(ValueError('boom'))",
            "line 2 in delegating: yield from [1, 2, 3]",
        ], YieldFromIterator);
    }

    [TestMethod]
    public void ThrowIntoCoroutineAtAwait_RecordsCoroutineAndAwaitFrames()
    {
        AssertFrames([
            "line 12 in <module>: c.throw(ValueError('boom'))",
            "line 7 in coro: await Awaitable()",
            "line 3 in __await__: yield 1",
        ], CoroutineAwait);
    }

    [TestMethod]
    public void ThrowIntoNeverStartedGenerator_RecordsFrameAtTheDefinition()
    {
        // the body never runs, but gen_send_ex2 still evaluates the frame with
        // the pending exception, so the frame is recorded at its first
        // instruction — the first line of the code object
        AssertFrames([
            "line 7 in <module>: g.throw(ValueError('boom'))",
            "line 1 in gen: def gen():",
        ], NeverStarted);
    }

    [TestMethod]
    public void ThrowExceptionWithExistingTraceback_KeepsItAsTheTail()
    {
        AssertFrames([
            "line 13 in <module>: g.throw(e)",
            "line 2 in gen: value = yield 1",
            "line 7 in <module>: raise e",
        ], AlreadyHasTraceback);
    }

    [TestMethod]
    public void ThrowIntoGeneratorThatHandlesIt_KeepsTheHandlerFrame()
    {
        AssertFrames([
            "line 10 in <module>: g.throw(ValueError('boom'))",
            "line 3 in gen: yield 1",
        ], GeneratorHandler);
    }

    private const string ExplicitReraise = """
        def inner():
            raise ValueError('boom')


        def mid():
            try:
                inner()
            except ValueError as e:
                raise e


        mid()
        """;

    // An explicit re-raise keeps the traceback the exception already carries and
    // prepends the raise site: CPython's do_raise sets the exception without
    // touching its traceback, and the frame records itself when the error
    // surfaces. Replacing the traceback with a fresh snapshot of the active
    // stack loses every frame the exception travelled through.
    [TestMethod]
    public void ExplicitReraise_KeepsTheExistingTracebackAndPrependsTheRaiseSite()
    {
        AssertFrames([
            "line 12 in <module>: mid()",
            "line 9 in mid: raise e",
            "line 7 in mid: inner()",
            "line 2 in inner: raise ValueError('boom')",
        ], ExplicitReraise);
    }

    private const string BareReraise = """
        def inner():
            raise ValueError('boom')


        def mid():
            try:
                inner()
            except ValueError:
                raise


        mid()
        """;

    // A bare raise reaches exception_unwind through RERAISE, not the error
    // label, so the re-raising frame is not recorded: only the frames the
    // exception actually travelled through appear.
    [TestMethod]
    public void BareReraise_DoesNotRecordTheReraisingFrame()
    {
        AssertFrames([
            "line 12 in <module>: mid()",
            "line 7 in mid: inner()",
            "line 2 in inner: raise ValueError('boom')",
        ], BareReraise);
    }

    private const string InlineComprehension = """
        def f(x):
            raise ValueError('boom')


        r = [f(i) for i in range(1)]
        """;

    // PEP 709 inlines a comprehension into its enclosing frame, so the only
    // entry the comprehension body contributes is the enclosing frame's own —
    // CPython has no separate frame to record, and duplicating it here would
    // print the module line twice.
    [TestMethod]
    public void InlineComprehension_RecordsTheEnclosingFrameOnce()
    {
        AssertFrames([
            "line 5 in <module>: r = [f(i) for i in range(1)]",
            "line 2 in f: raise ValueError('boom')",
        ], InlineComprehension);
    }

    private const string UnraisableCloseLookup = """
        class LookupRaises:
            def __iter__(self):
                return self

            def __next__(self):
                return 1

            def __getattr__(self, name):
                raise ValueError('boom-' + name)


        def delegated():
            yield from LookupRaises()


        g = delegated()
        next(g)
        g.close()
        """;

    // The unraisable channel prints the exception's own traceback
    // (write_unraisable_exc -> PyTraceBack_Print), not a snapshot of whatever
    // stack happens to be live when it is written. The failing close lookup
    // never unwinds the delegating or module frames, so snapshotting there added
    // both of them to a report CPython gives for the raising frame alone.
    [TestMethod]
    public void UnraisableCloseLookup_RecordsOnlyTheRaisingFrame()
    {
        var stderr = RunCapturingStderrUnchecked(UnraisableCloseLookup);

        CollectionAssert.AreEqual(
            new[] { "line 9 in __getattr__: raise ValueError('boom-' + name)" },
            FrameTrace(stderr), stderr);
        StringAssert.Contains(stderr, "Exception ignored while closing generator", stderr);
        StringAssert.Contains(stderr, "ValueError: boom-close", stderr);
    }
}
