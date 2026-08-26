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

    [TestMethod]
    public void WarningsModule_Warn_EmitsUserWarning()
    {
        var (host, env, context) = CreateContext();
        try
        {
            var module = new PyModuleObject("<test>");
            PyInterpreter.RunCodeWithContext(
                context,
                "import warnings\nwarnings.warn(\"boom\")",
                module, "<test>", isMain: true);
            Assert.AreEqual("<test>:2: UserWarning: boom\n  warnings.warn(\"boom\")\n", host.Stderr.ToString());
        }
        finally
        {
            context.Dispose();
            env.Dispose();
        }
    }

    [TestMethod]
    public void WarningsModule_Warn_Category()
    {
        var (host, env, context) = CreateContext();
        try
        {
            var module = new PyModuleObject("<test>");
            PyInterpreter.RunCodeWithContext(
                context,
                "import warnings\nwarnings.warn(\"boom\", DeprecationWarning)",
                module, "<test>", isMain: true);
            Assert.AreEqual("<test>:2: DeprecationWarning: boom\n  warnings.warn(\"boom\", DeprecationWarning)\n", host.Stderr.ToString());
        }
        finally
        {
            context.Dispose();
            env.Dispose();
        }
    }

    [TestMethod]
    public void WarningsModule_Warn_InvalidCategory_RaisesTypeError()
    {
        var (host, env, context) = CreateContext();
        try
        {
            var module = new PyModuleObject("<test>");
            Assert.ThrowsExactly<PyRuntimeException>(() =>
                PyInterpreter.RunCodeWithContext(
                    context,
                    "import warnings\nwarnings.warn(\"boom\", int)",
                    module, "<test>", isMain: true));
        }
        finally
        {
            context.Dispose();
            env.Dispose();
        }
    }

    [TestMethod]
    public void WarningsModule_Warn_InvalidStacklevel_RaisesTypeError()
    {
        var (host, env, context) = CreateContext();
        try
        {
            var module = new PyModuleObject("<test>");
            Assert.ThrowsExactly<PyRuntimeException>(() =>
                PyInterpreter.RunCodeWithContext(
                    context,
                    "import warnings\nwarnings.warn(\"boom\", stacklevel=\"bad\")",
                    module, "<test>", isMain: true));
        }
        finally
        {
            context.Dispose();
            env.Dispose();
        }
    }

    [TestMethod]
    public void WarningsModule_SimpleFilter_Ignore_Suppresses()
    {
        var (host, env, context) = CreateContext();
        try
        {
            var module = new PyModuleObject("<test>");
            PyInterpreter.RunCodeWithContext(
                context,
                "import warnings\nwarnings.simplefilter(\"ignore\")\nwarnings.warn(\"boom\")",
                module, "<test>", isMain: true);
            Assert.AreEqual(string.Empty, host.Stderr.ToString());
        }
        finally
        {
            context.Dispose();
            env.Dispose();
        }
    }

    [TestMethod]
    public void WarningsModule_SimpleFilter_Error_Raises()
    {
        var (host, env, context) = CreateContext();
        try
        {
            var module = new PyModuleObject("<test>");
            Assert.ThrowsExactly<PyRuntimeException>(() =>
                PyInterpreter.RunCodeWithContext(
                    context,
                    "import warnings\nwarnings.simplefilter(\"error\")\nwarnings.warn(\"boom\")",
                    module, "<test>", isMain: true));
        }
        finally
        {
            context.Dispose();
            env.Dispose();
        }
    }

    [TestMethod]
    public void WarningsModule_CatchWarnings_RestoresFilter()
    {
        var (host, env, context) = CreateContext();
        try
        {
            var module = new PyModuleObject("<test>");
            PyInterpreter.RunCodeWithContext(
                context,
                "import warnings\nwith warnings.catch_warnings(action=\"ignore\"):\n    warnings.warn(\"inside\")\nwarnings.warn(\"outside\")",
                module, "<test>", isMain: true);
            Assert.AreEqual("<test>:4: UserWarning: outside\n  warnings.warn(\"outside\")\n", host.Stderr.ToString());
        }
        finally
        {
            context.Dispose();
            env.Dispose();
        }
    }

    [TestMethod]
    public void WarningsModule_CatchWarnings_NestedContextsRestoreOuterState()
    {
        var (host, env, context) = CreateContext();
        try
        {
            var module = new PyModuleObject("<test>");
            PyInterpreter.RunCodeWithContext(
                context,
                "import warnings\nwith warnings.catch_warnings(action=\"ignore\"):\n    with warnings.catch_warnings(action=\"always\"):\n        warnings.warn(\"inner\")\n    warnings.warn(\"outer\")\nwarnings.warn(\"after\")",
                module, "<test>", isMain: true);
            Assert.AreEqual("<test>:4: UserWarning: inner\n          warnings.warn(\"inner\")\n<test>:6: UserWarning: after\n  warnings.warn(\"after\")\n", host.Stderr.ToString());
        }
        finally
        {
            context.Dispose();
            env.Dispose();
        }
    }

    [TestMethod]
    public void WarningsModule_CatchWarnings_RestoresAfterException()
    {
        var (host, env, context) = CreateContext();
        try
        {
            var module = new PyModuleObject("<test>");
            PyInterpreter.RunCodeWithContext(
                context,
                "import warnings\ntry:\n    with warnings.catch_warnings(action=\"ignore\"):\n        raise ValueError(\"boom\")\nexcept ValueError:\n    pass\nwarnings.warn(\"after\")",
                module, "<test>", isMain: true);
            Assert.AreEqual("<test>:7: UserWarning: after\n  warnings.warn(\"after\")\n", host.Stderr.ToString());
        }
        finally
        {
            context.Dispose();
            env.Dispose();
        }
    }

    [TestMethod]
    public void WarningsModule_FilterWarnings_MessageRegex()
    {
        var (host, env, context) = CreateContext();
        try
        {
            var module = new PyModuleObject("<test>");
            PyInterpreter.RunCodeWithContext(
                context,
                "import warnings\nwarnings.filterwarnings(\"ignore\", message=\"boom\")\nwarnings.warn(\"boom\")\nwarnings.warn(\"other\")",
                module, "<test>", isMain: true);
            Assert.AreEqual("<test>:4: UserWarning: other\n  warnings.warn(\"other\")\n", host.Stderr.ToString());
        }
        finally
        {
            context.Dispose();
            env.Dispose();
        }
    }

    [TestMethod]
    public void WarningsModule_FilterWarnings_MessageRegex_IgnoreCase()
    {
        var (host, env, context) = CreateContext();
        try
        {
            var module = new PyModuleObject("<test>");
            PyInterpreter.RunCodeWithContext(
                context,
                "import warnings\nwarnings.filterwarnings(\"ignore\", message=\"BOOM\")\nwarnings.warn(\"boom\")",
                module, "<test>", isMain: true);
            Assert.AreEqual(string.Empty, host.Stderr.ToString());
        }
        finally
        {
            context.Dispose();
            env.Dispose();
        }
    }

    [TestMethod]
    public void WarningsModule_FilterWarnings_ModuleRegex()
    {
        var (host, env, context) = CreateContext();
        try
        {
            context.PyEnvironment.Warnings.AddFilter(
                new WarningFilter(WarningAction.Ignore, PyUserWarningObjectType.Shared, null, "test", 0));
            context.WarnExplicit(PyStrObject.FromString("boom"), PyUserWarningObjectType.Shared, "test.py", 7);
            context.WarnExplicit(PyStrObject.FromString("boom"), PyUserWarningObjectType.Shared, "other.py", 7);
            Assert.AreEqual("other.py:7: UserWarning: boom\n", host.Stderr.ToString());
        }
        finally
        {
            context.Dispose();
            env.Dispose();
        }
    }

    [TestMethod]
    public void WarningsModule_FilterWarnings_Lineno()
    {
        var (host, env, context) = CreateContext();
        try
        {
            context.PyEnvironment.Warnings.AddFilter(
                new WarningFilter(WarningAction.Ignore, PyUserWarningObjectType.Shared, null, null, 7));
            context.WarnExplicit(PyStrObject.FromString("boom"), PyUserWarningObjectType.Shared, "mod.py", 7);
            context.WarnExplicit(PyStrObject.FromString("boom"), PyUserWarningObjectType.Shared, "mod.py", 9);
            Assert.AreEqual("mod.py:9: UserWarning: boom\n", host.Stderr.ToString());
        }
        finally
        {
            context.Dispose();
            env.Dispose();
        }
    }

    [TestMethod]
    public void WarningsModule_FilterWarnings_Append_KeepsPrecedence()
    {
        var (host, env, context) = CreateContext();
        try
        {
            context.PyEnvironment.Warnings.AddFilter(PyUserWarningObjectType.Shared, WarningAction.Ignore);
            context.PyEnvironment.Warnings.AddFilter(
                new WarningFilter(WarningAction.Always, PyUserWarningObjectType.Shared, null, null, 0), append: true);
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
    public void WarningsModule_ResetWarnings_ClearsFilters()
    {
        var (host, env, context) = CreateContext();
        try
        {
            var module = new PyModuleObject("<test>");
            PyInterpreter.RunCodeWithContext(
                context,
                "import warnings\nwarnings.filterwarnings(\"ignore\")\nwarnings.resetwarnings()\nwarnings.warn(\"boom\")",
                module, "<test>", isMain: true);
            Assert.AreEqual("<test>:4: UserWarning: boom\n  warnings.warn(\"boom\")\n", host.Stderr.ToString());
        }
        finally
        {
            context.Dispose();
            env.Dispose();
        }
    }

    [TestMethod]
    public void WarningsModule_FilterWarnings_InvalidAction_RaisesValueError()
    {
        var (host, env, context) = CreateContext();
        try
        {
            var module = new PyModuleObject("<test>");
            Assert.ThrowsExactly<PyRuntimeException>(() =>
                PyInterpreter.RunCodeWithContext(
                    context,
                    "import warnings\nwarnings.filterwarnings(\"bogus\")",
                    module, "<test>", isMain: true));
        }
        finally
        {
            context.Dispose();
            env.Dispose();
        }
    }

    [TestMethod]
    public void WarningsModule_FilterWarnings_NonStringMessage_RaisesTypeError()
    {
        var (host, env, context) = CreateContext();
        try
        {
            var module = new PyModuleObject("<test>");
            Assert.ThrowsExactly<PyRuntimeException>(() =>
                PyInterpreter.RunCodeWithContext(
                    context,
                    "import warnings\nwarnings.filterwarnings(\"ignore\", message=123)",
                    module, "<test>", isMain: true));
        }
        finally
        {
            context.Dispose();
            env.Dispose();
        }
    }

    [TestMethod]
    public void WarningsModule_FilterWarnings_NegativeLineno_RaisesValueError()
    {
        var (host, env, context) = CreateContext();
        try
        {
            var module = new PyModuleObject("<test>");
            Assert.ThrowsExactly<PyRuntimeException>(() =>
                PyInterpreter.RunCodeWithContext(
                    context,
                    "import warnings\nwarnings.filterwarnings(\"ignore\", lineno=-1)",
                    module, "<test>", isMain: true));
        }
        finally
        {
            context.Dispose();
            env.Dispose();
        }
    }

    [TestMethod]
    public void WarningsModule_FilterWarnings_NonIntLineno_RaisesTypeError()
    {
        var (host, env, context) = CreateContext();
        try
        {
            var module = new PyModuleObject("<test>");
            Assert.ThrowsExactly<PyRuntimeException>(() =>
                PyInterpreter.RunCodeWithContext(
                    context,
                    "import warnings\nwarnings.filterwarnings(\"ignore\", lineno=\"x\")",
                    module, "<test>", isMain: true));
        }
        finally
        {
            context.Dispose();
            env.Dispose();
        }
    }

    [TestMethod]
    public void WarningsModule_FilterWarnings_InvalidCategory_RaisesTypeError()
    {
        var (host, env, context) = CreateContext();
        try
        {
            var module = new PyModuleObject("<test>");
            Assert.ThrowsExactly<PyRuntimeException>(() =>
                PyInterpreter.RunCodeWithContext(
                    context,
                    "import warnings\nwarnings.filterwarnings(\"ignore\", category=int)",
                    module, "<test>", isMain: true));
        }
        finally
        {
            context.Dispose();
            env.Dispose();
        }
    }
}
