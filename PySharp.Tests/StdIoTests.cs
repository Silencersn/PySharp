using PySharp.Modules.Builtins;
using PySharp.Modules.IO;
using PySharp.Modules.Sys;
using PySharp.Runtime;
using PySharp.Runtime.Calls;
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

        public StdioHost(Stream input, Stream output, Stream error)
        {
            _in = input;
            _out = output;
            _err = error;
        }

        public override Stream AllocateStdIn() => _in;
        public override Stream AllocateStdOut() => _out;
        public override Stream AllocateStdErr() => _err;
        public override IVirtualFileSystem FileSystem { get; } = MemoryFileSystem.CreateBuilder().Build();
    }

    private static PyTextIOWrapperObject CreateOutput(Stream stream, string name, string errors = "strict") =>
        PyTextIOWrapperObject.CreateStandardStream(
            stream, name, "w", readable: false, writable: true,
            System.Text.Encoding.UTF8, errors);

    private static PyTextIOWrapperObject CreateInput(Stream stream, string name = "<stdin>") =>
        PyTextIOWrapperObject.CreateStandardStream(
            stream, name, "r", readable: true, writable: false,
            System.Text.Encoding.UTF8, "strict");

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
        Assert.AreEqual("", ((PyStrObject)result.Value).Value);
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
        Assert.AreEqual("", ((PyStrObject)result.Value).Value);
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
        Assert.AreEqual("", ((PyStrObject)obj.ReadLine().Value!).Value);
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
            Assert.AreEqual("", ((PyStrObject)stdinObj.ReadLine().Value!).Value);

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
}
