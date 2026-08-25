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

    [TestMethod]
    public void WarnExplicit_DefaultAction_DeduplicatesSameSite()
    {
        var (host, env, context) = CreateContext();
        try
        {
            context.WarnExplicit(PyStrObject.FromString("boom"), PyUserWarningObjectType.Shared, "mod.py", 7);
            context.WarnExplicit(PyStrObject.FromString("boom"), PyUserWarningObjectType.Shared, "mod.py", 7);
            Assert.AreEqual("mod.py:7: UserWarning: boom\n", host.Stderr.ToString());
        }
        finally
        {
            context.Dispose();
            env.Dispose();
        }
    }

    [TestMethod]
    public void Warn_IgnoreAction_SuppressesOutput()
    {
        var (host, env, context) = CreateContext();
        try
        {
            context.PyEnvironment.Warnings.AddFilter(PyUserWarningObjectType.Shared, WarningAction.Ignore);
            context.WarnExplicit(PyStrObject.FromString("boom"), PyUserWarningObjectType.Shared, "mod.py", 7);
            Assert.AreEqual(string.Empty, host.Stderr.ToString());
        }
        finally
        {
            context.Dispose();
            env.Dispose();
        }
    }

    [TestMethod]
    public void Warn_ErrorAction_RaisesCategoryException()
    {
        var (host, env, context) = CreateContext();
        try
        {
            context.PyEnvironment.Warnings.AddFilter(PyUserWarningObjectType.Shared, WarningAction.Error);
            var result = context.WarnExplicit(PyStrObject.FromString("boom"), PyUserWarningObjectType.Shared, "mod.py", 7);
            Assert.IsTrue(result.IsError);
            Assert.IsTrue(PyUserWarningObjectType.Shared.IsInstance(result.Exception!));
            Assert.AreEqual(string.Empty, host.Stderr.ToString());
        }
        finally
        {
            context.Dispose();
            env.Dispose();
        }
    }

    [TestMethod]
    public void Warn_DefaultActionError_MakesAllWarningsError()
    {
        var (host, env, context) = CreateContext();
        try
        {
            context.PyEnvironment.Warnings.SetDefaultAction(WarningAction.Error);
            var result = context.WarnExplicit(PyStrObject.FromString("boom"), PyUserWarningObjectType.Shared, "mod.py", 7);
            Assert.IsTrue(result.IsError);
            Assert.IsTrue(PyUserWarningObjectType.Shared.IsInstance(result.Exception!));
            Assert.AreEqual(string.Empty, host.Stderr.ToString());
        }
        finally
        {
            context.Dispose();
            env.Dispose();
        }
    }

    [TestMethod]
    public void Warn_FilterVersionInvalidation_ForgetsPreviousWarnings()
    {
        var (host, env, context) = CreateContext();
        try
        {
            context.WarnExplicit(PyStrObject.FromString("boom"), PyUserWarningObjectType.Shared, "mod.py", 7);
            context.WarnExplicit(PyStrObject.FromString("boom"), PyUserWarningObjectType.Shared, "mod.py", 7);
            Assert.AreEqual("mod.py:7: UserWarning: boom\n", host.Stderr.ToString());

            context.PyEnvironment.Warnings.ClearFilters();
            context.WarnExplicit(PyStrObject.FromString("boom"), PyUserWarningObjectType.Shared, "mod.py", 7);
            Assert.AreEqual("mod.py:7: UserWarning: boom\nmod.py:7: UserWarning: boom\n", host.Stderr.ToString());
        }
        finally
        {
            context.Dispose();
            env.Dispose();
        }
    }

    [TestMethod]
    public void Warn_BaseCategoryFilter_MatchesSubclass()
    {
        var (host, env, context) = CreateContext();
        try
        {
            context.PyEnvironment.Warnings.AddFilter(PyWarningObjectType.Shared, WarningAction.Ignore);
            context.WarnExplicit(PyStrObject.FromString("boom"), PyUserWarningObjectType.Shared, "mod.py", 7);
            Assert.AreEqual(string.Empty, host.Stderr.ToString());
        }
        finally
        {
            context.Dispose();
            env.Dispose();
        }
    }

    [TestMethod]
    public void Warn_UserDefinedWarning_DerivesCategory()
    {
        var (host, env, context) = CreateContext();
        try
        {
            var module = new PyModuleObject("<test>");
            PyInterpreter.RunCodeWithContext(
                context,
                "class MyWarning(UserWarning):\n    pass\nw = MyWarning('boom')",
                module, "<test>", isMain: true);

            context.Warn(module.PyAttributesDict["w"]);
            Assert.AreEqual("<test>:0: MyWarning: boom\n", host.Stderr.ToString());
        }
        finally
        {
            context.Dispose();
            env.Dispose();
        }
    }

    [TestMethod]
    public void Warn_UserDefinedWarningFilter_Matches()
    {
        var (host, env, context) = CreateContext();
        try
        {
            var module = new PyModuleObject("<test>");
            PyInterpreter.RunCodeWithContext(
                context,
                "class MyWarning(UserWarning):\n    pass\nw = MyWarning('boom')",
                module, "<test>", isMain: true);

            var w = module.PyAttributesDict["w"];
            context.PyEnvironment.Warnings.AddFilter((PyTypeObject<PyExceptionObject>)w.PyType, WarningAction.Ignore);
            context.Warn(w);
            Assert.AreEqual(string.Empty, host.Stderr.ToString());
        }
        finally
        {
            context.Dispose();
            env.Dispose();
        }
    }

    [TestMethod]
    public void Warn_AlwaysAction_ShowsRepeatedly()
    {
        var (host, env, context) = CreateContext();
        try
        {
            context.PyEnvironment.Warnings.AddFilter(PyUserWarningObjectType.Shared, WarningAction.Always);
            context.WarnExplicit(PyStrObject.FromString("boom"), PyUserWarningObjectType.Shared, "mod.py", 7);
            context.WarnExplicit(PyStrObject.FromString("boom"), PyUserWarningObjectType.Shared, "mod.py", 7);
            Assert.AreEqual("mod.py:7: UserWarning: boom\nmod.py:7: UserWarning: boom\n", host.Stderr.ToString());
        }
        finally
        {
            context.Dispose();
            env.Dispose();
        }
    }

    [TestMethod]
    public void Warn_AllAction_ShowsRepeatedly()
    {
        var (host, env, context) = CreateContext();
        try
        {
            context.PyEnvironment.Warnings.AddFilter(PyUserWarningObjectType.Shared, WarningAction.All);
            context.WarnExplicit(PyStrObject.FromString("boom"), PyUserWarningObjectType.Shared, "mod.py", 7);
            context.WarnExplicit(PyStrObject.FromString("boom"), PyUserWarningObjectType.Shared, "mod.py", 7);
            Assert.AreEqual("mod.py:7: UserWarning: boom\nmod.py:7: UserWarning: boom\n", host.Stderr.ToString());
        }
        finally
        {
            context.Dispose();
            env.Dispose();
        }
    }

    [TestMethod]
    public void Warn_ModuleAction_DedupsPerModule()
    {
        var (host, env, context) = CreateContext();
        try
        {
            context.PyEnvironment.Warnings.AddFilter(PyUserWarningObjectType.Shared, WarningAction.Module);
            context.WarnExplicit(PyStrObject.FromString("boom"), PyUserWarningObjectType.Shared, "mod.py", 7);
            context.WarnExplicit(PyStrObject.FromString("boom"), PyUserWarningObjectType.Shared, "mod.py", 9);
            context.WarnExplicit(PyStrObject.FromString("boom"), PyUserWarningObjectType.Shared, "mod2.py", 7);
            Assert.AreEqual("mod.py:7: UserWarning: boom\nmod2.py:7: UserWarning: boom\n", host.Stderr.ToString());
        }
        finally
        {
            context.Dispose();
            env.Dispose();
        }
    }

    [TestMethod]
    public void Warn_OnceAction_DedupsGlobally()
    {
        var (host, env, context) = CreateContext();
        try
        {
            context.PyEnvironment.Warnings.AddFilter(PyUserWarningObjectType.Shared, WarningAction.Once);
            context.WarnExplicit(PyStrObject.FromString("boom"), PyUserWarningObjectType.Shared, "mod.py", 7);
            context.WarnExplicit(PyStrObject.FromString("boom"), PyUserWarningObjectType.Shared, "mod2.py", 7);
            context.WarnExplicit(PyStrObject.FromString("boom"), PyUserWarningObjectType.Shared, "mod3.py", 9);
            Assert.AreEqual("mod.py:7: UserWarning: boom\n", host.Stderr.ToString());
        }
        finally
        {
            context.Dispose();
            env.Dispose();
        }
    }
}
