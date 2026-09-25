using PySharp.Compilation;
using PySharp.Modules.Builtins;
using PySharp.Runtime.Calls;
using PySharp.Runtime.Environments;
using PySharp.Runtime.IO;
using PySharp.Runtime.IO.Memory;

namespace PySharp.Tests;

[TestClass]
public sealed class WarningTests
{
    private sealed class StderrCaptureHost : PyEnvironmentHost
    {
        public MemoryStream Stderr { get; } = new();
        public override Stream AllocateStdIn() => Stream.Null;
        public override Stream AllocateStdOut() => Stream.Null;
        public override Stream AllocateStdErr() => Stderr;
        public override IVirtualFileSystem FileSystem { get; } = MemoryFileSystem.CreateBuilder().Build();
    }

    private static (StderrCaptureHost Host, PyEnvironment Env, PyCallContext Context) CreateContext()
    {
        var host = new StderrCaptureHost();
        var env = new PyEnvironment(host);
        var context = PyCallContext.CreateInterpreterRootContext(env);
        return (host, env, context);
    }

    private static string GetStderr(StderrCaptureHost host, PyEnvironment env)
    {
        env.Error.Flush();
        return System.Text.Encoding.UTF8.GetString(host.Stderr.ToArray()).Replace("\r\n", "\n");
    }

    [TestMethod]
    public void WarnExplicit_FormatsStandardWarning()
    {
        var (host, env, context) = CreateContext();
        try
        {
            context.WarnExplicit(PyStrObject.FromString("hello"), PyUserWarningObjectType.Shared, "test.py", 42);
            Assert.AreEqual("test.py:42: UserWarning: hello\n", GetStderr(host, env));
        }
        finally
        {
            context.Dispose();
            env.Dispose();
        }
    }

    [TestMethod]
    public void Warn_DefaultsToUserWarning()
    {
        var (host, env, context) = CreateContext();
        try
        {
            context.Warn(PyStrObject.FromString("boom"));
            Assert.AreEqual("<sys>:0: UserWarning: boom\n", GetStderr(host, env));
        }
        finally
        {
            context.Dispose();
            env.Dispose();
        }
    }

    [TestMethod]
    public void Warn_DerivesCategoryFromWarningInstance()
    {
        var (host, env, context) = CreateContext();
        try
        {
            var instance = PyUserWarningObjectType.Shared.Create(PyStrObject.FromString("boom"));
            context.Warn(instance);
            Assert.AreEqual("<sys>:0: UserWarning: boom\n", GetStderr(host, env));
        }
        finally
        {
            context.Dispose();
            env.Dispose();
        }
    }

    [TestMethod]
    public void Warn_WithExplicitLocation_WritesStandardFormat()
    {
        var (host, env, context) = CreateContext();
        try
        {
            context.Warn(PyUserWarningObjectType.Shared, "boom", "mod.py", 7);
            Assert.AreEqual("mod.py:7: UserWarning: boom\n", GetStderr(host, env));
        }
        finally
        {
            context.Dispose();
            env.Dispose();
        }
    }

    [TestMethod]
    public void WarnSyntax_WarnsAgainOnSecondCompile()
    {
        // The syntax-warning dedup set lives on a per-compile session, like
        // CPython's parser state: two compiles of the same source each warn,
        // and independent literals inside one compile keep separate warnings.
        var (host, env, context) = CreateContext();
        try
        {
            var first = Compiler.Compile("x = \"\\400\"", CompileMode.Exec, context, "test.py");
            Assert.IsFalse(first.IsError);
            Assert.AreEqual(1, GetStderr(host, env).Split("is an invalid octal escape sequence").Length - 1);

            var second = Compiler.Compile("x = \"\\400\"", CompileMode.Exec, context, "test.py");
            Assert.IsFalse(second.IsError);
            Assert.AreEqual(2, GetStderr(host, env).Split("is an invalid octal escape sequence").Length - 1);

            var third = Compiler.Compile("y = \"\\400\" \"\\400\"", CompileMode.Exec, context, "test.py");
            Assert.IsFalse(third.IsError);
            Assert.AreEqual(4, GetStderr(host, env).Split("is an invalid octal escape sequence").Length - 1);
        }
        finally
        {
            context.Dispose();
            env.Dispose();
        }
    }
}
