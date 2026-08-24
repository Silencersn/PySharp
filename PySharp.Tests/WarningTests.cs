using PySharp.Modules.Builtins;
using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.Calls.Extensions;
using PySharp.Runtime.Environments;
using PySharp.Runtime.IO;
using PySharp.Runtime.IO.Memory;

#pragma warning disable MSTEST0037

namespace PySharp.Tests;

[TestClass]
public sealed class WarningTests
{
    private sealed class StderrCaptureHost : PyEnvironmentHost
    {
        public StringWriter Stderr { get; } = new() { NewLine = "\n" };
        public override TextReader AllocateStdIn() => TextReader.Null;
        public override TextWriter AllocateStdOut() => TextWriter.Null;
        public override TextWriter AllocateStdErr() => Stderr;
        public override IVirtualFileSystem FileSystem { get; } = MemoryFileSystem.CreateBuilder().Build();
    }

    private static (StderrCaptureHost Host, PyEnvironment Env, PyCallContext Context) CreateContext()
    {
        var host = new StderrCaptureHost();
        var env = new PyEnvironment(host);
        var context = PyCallContext.CreateInterpreterRootContext(env);
        return (host, env, context);
    }

    [TestMethod]
    public void WarnExplicit_FormatsStandardWarning()
    {
        var (host, env, context) = CreateContext();
        try
        {
            context.WarnExplicit(PyStrObject.FromString("hello"), PyUserWarningObjectType.Shared, "test.py", 42);
            Assert.AreEqual("test.py:42: UserWarning: hello\n", host.Stderr.ToString());
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
            Assert.AreEqual("<sys>:0: UserWarning: boom\n", host.Stderr.ToString());
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
            Assert.AreEqual("<sys>:0: UserWarning: boom\n", host.Stderr.ToString());
        }
        finally
        {
            context.Dispose();
            env.Dispose();
        }
    }

    [TestMethod]
    public void Warn_InvalidCategory_ThrowsTypeError()
    {
        var (host, env, context) = CreateContext();
        try
        {
            try
            {
                _ = context.Warn(PyStrObject.FromString("x"), PyValueErrorObjectType.Shared).PyUnwrap(context);
                Assert.Fail("Expected a PyRuntimeException for a non-Warning category.");
            }
            catch (PyRuntimeException)
            {
                // expected: category is not a Warning subclass
            }
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
            Assert.AreEqual("mod.py:7: UserWarning: boom\n", host.Stderr.ToString());
        }
        finally
        {
            context.Dispose();
            env.Dispose();
        }
    }
}
