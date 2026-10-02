using PySharp.Modules.Builtins;
using PySharp.Modules.IO;
using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.Calls.Extensions;
using PySharp.Runtime.Environments;
using PySharp.Runtime.IO;
using PySharp.Runtime.IO.Memory;

namespace PySharp.Tests;

[TestClass]
public sealed class StdIoTests
{
    private sealed class StdioHost : PyEnvironmentHost
    {
        private readonly Stream _in;
        private readonly Stream _out;
        private readonly Stream _err;
        private readonly bool _stdioIsTerminal;

        // fixture runs read like an interactive terminal by default so
        // stdout assertions see line-buffered output without flushing the
        // wrapper; the redirected-buffering tests opt out explicitly
        public StdioHost(Stream input, Stream output, Stream error, bool stdioIsTerminal = true)
        {
            _in = input;
            _out = output;
            _err = error;
            _stdioIsTerminal = stdioIsTerminal;
        }

        public override Stream AllocateStdIn() => _in;
        public override Stream AllocateStdOut() => _out;
        public override Stream AllocateStdErr() => _err;
        public override bool StdInIsTerminal => _stdioIsTerminal;
        public override bool StdOutIsTerminal => _stdioIsTerminal;
        public override bool StdErrIsTerminal => _stdioIsTerminal;
        public override IVirtualFileSystem FileSystem { get; } = MemoryFileSystem.CreateBuilder().Build();
    }

    private static PyTextIOWrapperObject CreateOutput(Stream stream, string name, string errors = "strict") =>
        PyTextIOWrapperObject.CreateStandardStream(
            stream, name, "w", readable: false, writable: true,
            System.Text.Encoding.UTF8, errors);

    private static PyTextIOWrapperObject CreateBufferedOutput(
        Stream stream, PyStandardStreamBuffering buffering, bool isTerminal) =>
        PyTextIOWrapperObject.CreateStandardStream(
            stream, "<stdout>", "w", readable: false, writable: true,
            System.Text.Encoding.UTF8, "strict", buffering, isTerminal);

    private static PyTextIOWrapperObject CreateInput(Stream stream, string name = "<stdin>") =>
        PyTextIOWrapperObject.CreateStandardStream(
            stream, name, "r", readable: true, writable: false,
            System.Text.Encoding.UTF8, "strict");

    // the std streams translate '\n' to the platform newline on write
    private static string Text(MemoryStream stream) =>
        System.Text.Encoding.UTF8.GetString(stream.ToArray()).Replace("\r\n", "\n");

    private static PyTextIOWrapperObject GetStream(PyModuleObject module, string name)
    {
        Assert.IsTrue(module.PyAttributes.TryGetValue(name, out var value), $"sys.{name} missing");
        return (PyTextIOWrapperObject)value;
    }

    // --- standard streams (sys.stdin/stdout/stderr) ---

    [TestMethod]
    public void Stdio_StdinReadLine_AtEof_ReturnsEmptyString()
    {
        var obj = CreateInput(new MemoryStream());
        var result = obj.ReadLine();
        Assert.IsFalse(result.IsError, "readline() at EOF should not be an error");
        Assert.AreEqual(string.Empty, ((PyStrObject)result.Value).Value);
    }

    [TestMethod]
    public void Stdio_StdinReadLine_PreservesTrailingNewline()
    {
        var obj = CreateInput(new MemoryStream(System.Text.Encoding.UTF8.GetBytes("hello\nworld\n")));
        Assert.AreEqual("hello\n", ((PyStrObject)obj.ReadLine().Value!).Value);
        Assert.AreEqual("world\n", ((PyStrObject)obj.ReadLine().Value!).Value);
    }

    [TestMethod]
    public void Stdio_StdoutWrite_ReturnsCharCount()
    {
        var stream = new MemoryStream();
        var obj = CreateOutput(stream, "<stdout>");
        var result = obj.Write(PyCallContext.CSharpRuntime, PyStrObject.FromString("hello"));
        Assert.IsFalse(result.IsError);
        Assert.AreEqual(5, ((PyIntObject)result.Value).Int32Value);
        Assert.AreEqual("hello", System.Text.Encoding.UTF8.GetString(stream.ToArray()));
    }

    // the standard streams mirror CPython's create_stdio newline=None
    // (issue #396): writes expand '\n' to os.linesep while user '\r' bytes
    // survive, reads fold \r\n and lone \r into '\n'
    [TestMethod]
    public void Stdio_StdoutWrite_ExpandsNewlineToPlatformNewline()
    {
        var stream = new MemoryStream();
        var obj = CreateOutput(stream, "<stdout>");
        var result = obj.Write(PyCallContext.CSharpRuntime, PyStrObject.FromString("x\n"));
        Assert.IsFalse(result.IsError);
        Assert.AreEqual("x" + Environment.NewLine, System.Text.Encoding.UTF8.GetString(stream.ToArray()));
    }

    [TestMethod]
    public void Stdio_StdoutWrite_PreservesUserCarriageReturns()
    {
        var stream = new MemoryStream();
        var obj = CreateOutput(stream, "<stdout>");
        var result = obj.Write(PyCallContext.CSharpRuntime, PyStrObject.FromString("a\rb\n"));
        Assert.IsFalse(result.IsError);
        // CPython 3.14 (newline=None): "a\rb\n" -> a \r b \r \n on Windows
        Assert.AreEqual("a\rb" + Environment.NewLine, System.Text.Encoding.UTF8.GetString(stream.ToArray()));
    }

    [TestMethod]
    public void Stdio_StdinRead_FoldsUniversalNewlines()
    {
        var obj = CreateInput(new MemoryStream(System.Text.Encoding.UTF8.GetBytes("a\r\nb\rc\n")));
        var result = obj.Read(PyCallContext.CSharpRuntime);
        Assert.IsFalse(result.IsError);
        Assert.AreEqual("a\nb\nc\n", ((PyStrObject)result.Value).Value);
    }

    [TestMethod]
    public void Stdio_StdinReadLine_SplitsOnLoneCarriageReturn()
    {
        var obj = CreateInput(new MemoryStream(System.Text.Encoding.UTF8.GetBytes("a\r\nb\rc\n")));
        Assert.AreEqual("a\n", ((PyStrObject)obj.ReadLine().Value!).Value);
        Assert.AreEqual("b\n", ((PyStrObject)obj.ReadLine().Value!).Value);
        Assert.AreEqual("c\n", ((PyStrObject)obj.ReadLine().Value!).Value);
    }

    [TestMethod]
    public void Stdio_Stdin_NotWritable_Stdout_NotReadable()
    {
        var stdin = CreateInput(new MemoryStream());
        Assert.IsTrue(stdin._isReadable);
        Assert.IsFalse(stdin._isWritable);

        var stdout = CreateOutput(new MemoryStream(), "<stdout>");
        Assert.IsFalse(stdout._isReadable);
        Assert.IsTrue(stdout._isWritable);
    }

    [TestMethod]
    public void Stdio_StdinWrite_ReturnsNotWritableError()
    {
        var stdin = CreateInput(new MemoryStream());
        var result = stdin.Write(PyCallContext.CSharpRuntime, PyStrObject.FromString("x"));
        Assert.IsTrue(result.IsError);
    }

    [TestMethod]
    public void Stdio_StdoutRead_ReturnsNotReadableError()
    {
        var stdout = CreateOutput(new MemoryStream(), "<stdout>");
        var result = stdout.Read(PyCallContext.CSharpRuntime);
        Assert.IsTrue(result.IsError);
    }

    // --- the unified type (open() and the standard streams share it) ---

    [TestMethod]
    public void FileObject_TextReadLine_AtEof_ReturnsEmptyString()
    {
        var obj = new PyTextIOWrapperObject(new MemoryStream(), "r", "test",
            isTextMode: true, isReadable: true, isWritable: false, isSeekable: false);
        var result = obj.ReadLine();
        Assert.IsFalse(result.IsError);
        Assert.AreEqual(string.Empty, ((PyStrObject)result.Value).Value);
    }

    [TestMethod]
    public void FileObject_BinaryReadLine_AtEof_ReturnsEmptyBytes()
    {
        var obj = new PyTextIOWrapperObject(new MemoryStream(), "rb", "test",
            isTextMode: false, isReadable: true, isWritable: false, isSeekable: false);
        var result = obj.ReadLine();
        Assert.IsFalse(result.IsError);
        Assert.AreEqual(0, ((PyBytesObject)result.Value).Length);
    }

    [TestMethod]
    public void FileObject_TextReadLine_PreservesTrailingNewline()
    {
        var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("hello\nworld\n"));
        var obj = new PyTextIOWrapperObject(stream, "r", "test",
            isTextMode: true, isReadable: true, isWritable: false, isSeekable: false);
        Assert.AreEqual("hello\n", ((PyStrObject)obj.ReadLine().Value!).Value);
        Assert.AreEqual("world\n", ((PyStrObject)obj.ReadLine().Value!).Value);
        Assert.AreEqual(string.Empty, ((PyStrObject)obj.ReadLine().Value!).Value);
    }

    [TestMethod]
    public void FileObject_Next_RaisesStopIterationAtEof()
    {
        var obj = new PyTextIOWrapperObject(new MemoryStream(), "r", "test",
            isTextMode: true, isReadable: true, isWritable: false, isSeekable: false);
        var result = PySpecialMethods.Next(PyCallContext.CSharpRuntime, obj);
        Assert.IsTrue(result.IsStopIteration, "iteration at EOF should raise StopIteration");
    }

    // --- Sys module wiring ---

    [TestMethod]
    public void SysModule_OnImport_ExposesStandardStreams()
    {
        var host = new StdioHost(new MemoryStream(), new MemoryStream(), new MemoryStream());
        var env = new PyEnvironment(host);
        var context = PyCallContext.CreateInterpreterRootContext(env);
        try
        {
            var module = env.LoadBuiltinModule(context, "sys");

            Assert.IsTrue(module.PyAttributes.TryGetValue("stdin", out var stdin));
            Assert.IsInstanceOfType(stdin, typeof(PyTextIOWrapperObject));
            var stdinObj = (PyTextIOWrapperObject)stdin;
            Assert.AreEqual(string.Empty, ((PyStrObject)stdinObj.ReadLine().Value!).Value);

            Assert.IsTrue(module.PyAttributes.TryGetValue("stdout", out var stdout));
            Assert.IsInstanceOfType(stdout, typeof(PyTextIOWrapperObject));

            Assert.IsTrue(module.PyAttributes.TryGetValue("stderr", out var stderr));
            Assert.IsInstanceOfType(stderr, typeof(PyTextIOWrapperObject));

            // all three streams share the one TextIOWrapper type, like CPython
            Assert.AreSame(stdin.PyType, stdout.PyType);
            Assert.AreSame(stdout.PyType, stderr.PyType);
            Assert.AreSame(PyTextIOWrapperObjectType.Shared, stdout.PyType);
        }
        finally
        {
            context.Dispose();
            env.Dispose();
        }
    }

    [TestMethod]
    public void SysModule_StandardStreams_ReportModeNameAndEncoding()
    {
        var host = new StdioHost(new MemoryStream(), new MemoryStream(), new MemoryStream());
        var env = new PyEnvironment(host);
        var context = PyCallContext.CreateInterpreterRootContext(env);
        try
        {
            var module = env.LoadBuiltinModule(context, "sys");
            var stdin = GetStream(module, "stdin");
            var stdout = GetStream(module, "stdout");
            var stderr = GetStream(module, "stderr");

            Assert.AreEqual("r", stdin._mode);
            Assert.AreEqual("w", stdout._mode);
            Assert.AreEqual("<stdout>", stdout._name);
            Assert.AreEqual("utf-8", stdout._encodingName);

            // CPython's create_stdio gives stderr the backslashreplace handler
            Assert.AreEqual("strict", stdout._errorsName);
            Assert.AreEqual("backslashreplace", stderr._errorsName);
        }
        finally
        {
            context.Dispose();
            env.Dispose();
        }
    }

    [TestMethod]
    public void SysModule_ClosingStdout_LeavesTheHostStreamUsable()
    {
        // the host owns the process's console handle: close() closes the
        // wrapper only, so the environment's own stream stays usable
        var outStream = new MemoryStream();
        var host = new StdioHost(new MemoryStream(), outStream, new MemoryStream());
        var env = new PyEnvironment(host);
        var context = PyCallContext.CreateInterpreterRootContext(env);
        try
        {
            var module = env.LoadBuiltinModule(context, "sys");
            var stdout = GetStream(module, "stdout");

            Assert.IsFalse(stdout.Close().IsError);
            Assert.IsTrue(stdout.IsClosed);
            Assert.IsTrue(stdout.Write(context, PyStrObject.FromString("x")).IsError);
            Assert.IsTrue(outStream.CanWrite, "the host stream must survive the wrapper's close");
        }
        finally
        {
            context.Dispose();
            env.Dispose();
        }
    }

    // --- standard stream buffering (create_stdio semantics) ---

    [TestMethod]
    public void Stdio_StdoutLineBuffering_HoldsPartialWriteAndFlushesOnNewline()
    {
        var stream = new MemoryStream();
        var stdout = CreateBufferedOutput(stream, PyStandardStreamBuffering.Line, isTerminal: true);
        var context = PyCallContext.CSharpRuntime;

        Assert.IsFalse(stdout.Write(context, PyStrObject.FromString("par")).IsError);
        Assert.AreEqual(string.Empty, Text(stream), "a partial line must stay buffered");

        Assert.IsFalse(stdout.Write(context, PyStrObject.FromString("tial\n")).IsError);
        Assert.AreEqual("partial\n", Text(stream));

        // CPython flushes a line-buffered write on a bare '\r' too
        // (Modules/_io/textio.c: needflush checks '\n' or '\r')
        Assert.IsFalse(stdout.Write(context, PyStrObject.FromString("cr\r")).IsError);
        Assert.AreEqual("partial\ncr\r", Text(stream));
    }

    [TestMethod]
    public void Stdio_StdoutBlockBuffering_HoldsUntilFlush()
    {
        var stream = new MemoryStream();
        var stdout = CreateBufferedOutput(stream, PyStandardStreamBuffering.Block, isTerminal: false);
        var context = PyCallContext.CSharpRuntime;

        Assert.IsFalse(stdout.Write(context, PyStrObject.FromString("held\n")).IsError);
        Assert.AreEqual(string.Empty, Text(stream), "block-buffered stdout must not leak per write");

        Assert.IsFalse(stdout.Flush().IsError);
        Assert.AreEqual("held\n", Text(stream));
    }

    [TestMethod]
    public void Stdio_StdoutBlockBuffering_LandsAtThe8192Boundary()
    {
        var stream = new MemoryStream();
        var stdout = CreateBufferedOutput(stream, PyStandardStreamBuffering.Block, isTerminal: false);
        var context = PyCallContext.CSharpRuntime;

        Assert.IsFalse(stdout.Write(context, PyStrObject.FromString(new string('x', 8191))).IsError);
        Assert.AreEqual(0, stream.Length, "8191 bytes stay pending");

        Assert.IsFalse(stdout.Write(context, PyStrObject.FromString("x")).IsError);
        Assert.AreEqual(8192, stream.Length, "crossing the 8192 threshold lands the chunk");
    }

    [TestMethod]
    public void Stdio_CloseFlushesPendingBufferedOutput()
    {
        var stream = new MemoryStream();
        var stdout = CreateBufferedOutput(stream, PyStandardStreamBuffering.Block, isTerminal: false);
        var context = PyCallContext.CSharpRuntime;

        Assert.IsFalse(stdout.Write(context, PyStrObject.FromString("bye\n")).IsError);
        Assert.IsFalse(stdout.Close().IsError);
        Assert.AreEqual("bye\n", Text(stream));
    }

    [TestMethod]
    public void Stdio_StdoutShutdownFlush_LandsBufferedOutput()
    {
        var stream = new MemoryStream();
        var stdout = CreateBufferedOutput(stream, PyStandardStreamBuffering.Block, isTerminal: false);
        var context = PyCallContext.CSharpRuntime;

        Assert.IsFalse(stdout.Write(context, PyStrObject.FromString("final\n")).IsError);
        stdout.FlushAtShutdown();
        Assert.AreEqual("final\n", Text(stream));
        Assert.IsFalse(stdout.IsClosed, "the shutdown flush must not close the wrapper");
    }

    [TestMethod]
    public void Stdio_MergedSink_LineBufferedStderrPrecedesBlockBufferedStdout()
    {
        // the merged-capture ordering contract (CPython 2>&1): stdout is
        // block-buffered and lands at flush/exit while stderr streams per
        // line, so the err lines must all precede the out lines
        var sink = new MemoryStream();
        var stdout = CreateBufferedOutput(sink, PyStandardStreamBuffering.Block, isTerminal: false);
        var stderr = CreateBufferedOutput(sink, PyStandardStreamBuffering.Line, isTerminal: false);
        var context = PyCallContext.CSharpRuntime;

        Assert.IsFalse(stdout.Write(context, PyStrObject.FromString("out1\n")).IsError);
        Assert.IsFalse(stderr.Write(context, PyStrObject.FromString("err1\n")).IsError);
        Assert.IsFalse(stdout.Write(context, PyStrObject.FromString("out2\n")).IsError);
        Assert.IsFalse(stderr.Write(context, PyStrObject.FromString("err2\n")).IsError);
        stdout.FlushAtShutdown();

        Assert.AreEqual("err1\nerr2\nout1\nout2\n", Text(sink));
    }

    [TestMethod]
    public void Stdio_EnvironmentDispose_FlushesTheRedirectedStdout()
    {
        // flush_std_files: the block-buffered stdout must land when the
        // environment is disposed, on every exit path
        var stream = new MemoryStream();
        var host = new StdioHost(new MemoryStream(), stream, new MemoryStream(), stdioIsTerminal: false);
        var env = new PyEnvironment(host);
        var context = PyCallContext.CreateInterpreterRootContext(env);
        var module = env.LoadBuiltinModule(context, "sys");
        var stdout = GetStream(module, "stdout");

        Assert.IsFalse(stdout.Write(context, PyStrObject.FromString("exit-flush\n")).IsError);
        Assert.AreEqual(string.Empty, Text(stream), "redirected stdout holds its buffer while running");

        context.Dispose();
        env.Dispose();
        Assert.AreEqual("exit-flush\n", Text(stream), "disposing the environment must flush sys.stdout");
    }

    [TestMethod]
    public void Stdio_EnvironmentDispose_ToleratesReboundNonWrapperStreams()
    {
        // flush_std_files flushes whatever sys.stdout/sys.stderr is bound
        // to; only the built-in wrapper type is reachable from the teardown,
        // and any other binding must be skipped without error
        var host = new StdioHost(new MemoryStream(), new MemoryStream(), new MemoryStream());
        var env = new PyEnvironment(host);
        var context = PyCallContext.CreateInterpreterRootContext(env);
        var module = env.LoadBuiltinModule(context, "sys");
        module.PyAttributes["stdout"] = PyStrObject.FromString("not a stream");
        module.PyAttributes["stderr"] = PyNoneObject.None;
        context.Dispose();
        env.Dispose();
    }

    [TestMethod]
    public void SysModule_StandardStreams_CarryCreateStdioBuffering()
    {
        // redirected wiring: stdout/stdin block-buffered, stderr always
        // line-buffered — create_stdio's decision table
        var redirected = new StdioHost(new MemoryStream(), new MemoryStream(), new MemoryStream(), stdioIsTerminal: false);
        var redirectedEnv = new PyEnvironment(redirected);
        var redirectedContext = PyCallContext.CreateInterpreterRootContext(redirectedEnv);
        try
        {
            var module = redirectedEnv.LoadBuiltinModule(redirectedContext, "sys");
            Assert.AreEqual(PyStandardStreamBuffering.Block, GetStream(module, "stdout")._buffering);
            Assert.AreEqual(PyStandardStreamBuffering.Block, GetStream(module, "stdin")._buffering);
            Assert.AreEqual(PyStandardStreamBuffering.Line, GetStream(module, "stderr")._buffering);
            Assert.IsFalse(GetStream(module, "stdout")._isTerminal);
            Assert.IsFalse(GetStream(module, "stdin")._isTerminal);
            Assert.IsFalse(GetStream(module, "stderr")._isTerminal);
        }
        finally
        {
            redirectedContext.Dispose();
            redirectedEnv.Dispose();
        }

        // terminal wiring: stdout/stdin line-buffered like CPython on a tty
        var tty = new StdioHost(new MemoryStream(), new MemoryStream(), new MemoryStream());
        var ttyEnv = new PyEnvironment(tty);
        var ttyContext = PyCallContext.CreateInterpreterRootContext(ttyEnv);
        try
        {
            var module = ttyEnv.LoadBuiltinModule(ttyContext, "sys");
            Assert.AreEqual(PyStandardStreamBuffering.Line, GetStream(module, "stdout")._buffering);
            Assert.AreEqual(PyStandardStreamBuffering.Line, GetStream(module, "stdin")._buffering);
            Assert.AreEqual(PyStandardStreamBuffering.Line, GetStream(module, "stderr")._buffering);
            Assert.IsTrue(GetStream(module, "stdout")._isTerminal);
            Assert.IsTrue(GetStream(module, "stderr")._isTerminal);
        }
        finally
        {
            ttyContext.Dispose();
            ttyEnv.Dispose();
        }
    }

    [TestMethod]
    public void Stdio_StandardStreams_ReportIsattyAndBufferingAttributes()
    {
        var tty = CreateBufferedOutput(new MemoryStream(), PyStandardStreamBuffering.Line, isTerminal: true);
        var redirected = CreateBufferedOutput(new MemoryStream(), PyStandardStreamBuffering.Block, isTerminal: false);

        Assert.AreSame(PyBoolObject.True, IsAtty(tty).Value);
        Assert.AreSame(PyBoolObject.False, IsAtty(redirected).Value);

        Assert.AreSame(PyBoolObject.True, GetBoolAttr(tty, "line_buffering").Value);
        Assert.AreSame(PyBoolObject.False, GetBoolAttr(tty, "write_through").Value);
        Assert.AreSame(PyBoolObject.False, GetBoolAttr(redirected, "line_buffering").Value);
        Assert.AreSame(PyBoolObject.False, GetBoolAttr(redirected, "write_through").Value);
    }

    [TestMethod]
    public void Stdio_ClosedIsatty_RaisesTheClosedError()
    {
        var stdout = CreateBufferedOutput(new MemoryStream(), PyStandardStreamBuffering.Line, isTerminal: true);
        Assert.IsFalse(stdout.Close().IsError);
        Assert.IsTrue(IsAtty(stdout).IsError, "isatty on a closed file must raise, like CPython's IOBase");
    }

    private static PyResult IsAtty(PyTextIOWrapperObject stream) =>
        stream.CallMethod(PyCallContext.CSharpRuntime, "isatty");

    private static PyResult GetBoolAttr(PyTextIOWrapperObject stream, string name) =>
        PyOperators.GetAttr(PyCallContext.CSharpRuntime, stream, name);
}
