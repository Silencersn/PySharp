using PySharp.Modules.Builtins;
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

    // --- PyStdIoObject (sys.stdin/stdout/stderr) ---

    [TestMethod]
    public void Stdio_StdinReadLine_AtEof_ReturnsEmptyString()
    {
        var obj = PyStdIoObject.CreateInput(new StringReader(""), "<stdin>");
        var result = obj.ReadLine();
        Assert.IsFalse(result.IsError, "readline() at EOF should not be an error");
        Assert.AreEqual("", ((PyStrObject)result.Value).Value);
    }

    [TestMethod]
    public void Stdio_StdinReadLine_PreservesTrailingNewline()
    {
        var obj = PyStdIoObject.CreateInput(new StringReader("hello\nworld\n"), "<stdin>");
        Assert.AreEqual("hello\n", ((PyStrObject)obj.ReadLine().Value!).Value);
        Assert.AreEqual("world\n", ((PyStrObject)obj.ReadLine().Value!).Value);
    }

    [TestMethod]
    public void Stdio_StdoutWrite_ReturnsCharCount()
    {
        var writer = new StringWriter() { NewLine = "\n" };
        var obj = PyStdIoObject.CreateOutput(writer, "<stdout>");
        var result = obj.Write(PyCallContext.CSharpRuntime, PyStrObject.FromString("hello"));
        Assert.IsFalse(result.IsError);
        Assert.AreEqual(5, ((PyIntObject)result.Value).Int32Value);
        Assert.AreEqual("hello", writer.ToString());
    }

    [TestMethod]
    public void Stdio_Stdin_NotWritable_Stdout_NotReadable()
    {
        var stdin = PyStdIoObject.CreateInput(new StringReader(""), "<stdin>");
        Assert.IsTrue(stdin.IsReadable);
        Assert.IsFalse(stdin.IsWritable);

        var stdout = PyStdIoObject.CreateOutput(new StringWriter(), "<stdout>");
        Assert.IsFalse(stdout.IsReadable);
        Assert.IsTrue(stdout.IsWritable);
    }

    [TestMethod]
    public void Stdio_StdinWrite_ReturnsNotWritableError()
    {
        var stdin = PyStdIoObject.CreateInput(new StringReader(""), "<stdin>");
        var result = stdin.Write(PyCallContext.CSharpRuntime, PyStrObject.FromString("x"));
        Assert.IsTrue(result.IsError);
    }

    [TestMethod]
    public void Stdio_StdoutRead_ReturnsNotReadableError()
    {
        var stdout = PyStdIoObject.CreateOutput(new StringWriter(), "<stdout>");
        var result = stdout.Read(PyCallContext.CSharpRuntime);
        Assert.IsTrue(result.IsError);
    }

    // --- PyFileObject (open()) ---

    [TestMethod]
    public void FileObject_TextReadLine_AtEof_ReturnsEmptyString()
    {
        var obj = new PyFileObject(new MemoryStream(), "r", "test",
            isTextMode: true, isReadable: true, isWritable: false, isSeekable: false);
        var result = obj.ReadLine();
        Assert.IsFalse(result.IsError);
        Assert.AreEqual("", ((PyStrObject)result.Value).Value);
    }

    [TestMethod]
    public void FileObject_BinaryReadLine_AtEof_ReturnsEmptyBytes()
    {
        var obj = new PyFileObject(new MemoryStream(), "rb", "test",
            isTextMode: false, isReadable: true, isWritable: false, isSeekable: false);
        var result = obj.ReadLine();
        Assert.IsFalse(result.IsError);
        Assert.AreEqual(0, ((PyBytesObject)result.Value).Length);
    }

    [TestMethod]
    public void FileObject_TextReadLine_PreservesTrailingNewline()
    {
        var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("hello\nworld\n"));
        var obj = new PyFileObject(stream, "r", "test",
            isTextMode: true, isReadable: true, isWritable: false, isSeekable: false);
        Assert.AreEqual("hello\n", ((PyStrObject)obj.ReadLine().Value!).Value);
        Assert.AreEqual("world\n", ((PyStrObject)obj.ReadLine().Value!).Value);
        Assert.AreEqual("", ((PyStrObject)obj.ReadLine().Value!).Value);
    }

    [TestMethod]
    public void FileObject_Next_RaisesStopIterationAtEof()
    {
        var obj = new PyFileObject(new MemoryStream(), "r", "test",
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
            Assert.IsInstanceOfType(stdin, typeof(PyStdIoObject));
            var stdinObj = (PyStdIoObject)stdin;
            Assert.AreEqual("", ((PyStrObject)stdinObj.ReadLine().Value!).Value);

            Assert.IsTrue(module.PyAttributes.TryGetValue("stdout", out var stdout));
            Assert.IsInstanceOfType(stdout, typeof(PyStdIoObject));

            Assert.IsTrue(module.PyAttributes.TryGetValue("stderr", out var stderr));
            Assert.IsInstanceOfType(stderr, typeof(PyStdIoObject));
        }
        finally
        {
            context.Dispose();
            env.Dispose();
        }
    }
}
