using PySharp.Modules.Builtins;
using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.Environments;
using PySharp.Runtime.IO;
using PySharp.Runtime.IO.Memory;
using System.Diagnostics.CodeAnalysis;

namespace PySharp.Tests;

[TestClass]
public sealed class ModuleProviderTests
{
    private sealed class ProviderHost : PyEnvironmentHost
    {
        private readonly IVirtualFileSystem _fileSystem;

        public ProviderHost(IVirtualFileSystem fileSystem)
        {
            _fileSystem = fileSystem;
        }

        public override Stream AllocateStdIn() => new MemoryStream();
        public override Stream AllocateStdOut() => new MemoryStream();
        public override Stream AllocateStdErr() => new MemoryStream();
        public override IVirtualFileSystem FileSystem => _fileSystem;
    }

    private static PyEnvironmentHost HostWith(params (string Name, string Content)[] files)
    {
        var builder = MemoryFileSystem.CreateBuilder();
        foreach (var (name, content) in files)
            builder.WithFile(name, content);
        return new ProviderHost(builder.Build());
    }

    private static PyModuleObject Run(PyEnvironment environment, string code)
    {
        using var context = PyCallContext.CreateInterpreterRootContext(environment);
        return PyInterpreter.RunCodeWithContext(context, code, "main", "<main>", isMain: true);
    }

    private static string GetStr(PyModuleObject module, string name)
    {
        Assert.IsTrue(module.PyAttributes.TryGetValue(name, out var value), $"attribute {name} missing");
        return ((PyStrObject)value).Value;
    }

    // A provider serving a single name from a factory; records create/exec
    // counts for lifecycle pins.
    private sealed class SingleModuleProvider : PyModuleProvider
    {
        private readonly string _name;
        private readonly Func<SingleModuleProvider, PyModuleObject> _factory;

        public SingleModuleProvider(string name, Func<SingleModuleProvider, PyModuleObject> factory)
        {
            _name = name;
            _factory = factory;
        }

        public int CreateCount { get; private set; }
        public int ExecCount { get; private set; }

        public bool VisibleInCacheDuringCreate { get; private set; }
        public bool VisibleInCacheDuringExec { get; private set; }
        public bool VisibleInCacheDuringOnImport { get; private set; }

        public override bool TryCreateModule(PyCallContext context, string fullName, IReadOnlyList<string>? path, [NotNullWhen(true)] out PyModuleObject? module)
        {
            if (fullName != _name)
            {
                module = null;
                return false;
            }

            CreateCount++;
            VisibleInCacheDuringCreate = context.PyEnvironment.Modules.ContainsKey(_name);
            module = _factory(this);
            return true;
        }

        protected internal override void ExecModule(PyCallContext context, PyModuleObject module)
        {
            ExecCount++;
            VisibleInCacheDuringExec = context.PyEnvironment.Modules.ContainsKey(_name);
        }

        public void ProbeOnImport(PyEnvironment environment)
        {
            VisibleInCacheDuringOnImport = environment.Modules.ContainsKey(_name);
        }
    }

    private sealed class MarkedModule : PyModuleObject
    {
        public MarkedModule(string name) : base(name)
        {
            AppendAttribute("SOURCE", PyStrObject.FromString("provider"));
        }
    }

    // Pins the register-before-initialize bargain: the reentrant load inside
    // OnImport must find this very instance in the cache instead of
    // reentering the provider for a second, half-built copy.
    private sealed class SelfImportingModule : PyModuleObject
    {
        public SelfImportingModule() : base("selfimp")
        {
            AppendAttribute("MARK", PyIntObject.FromInteger(1));
        }

        public int EnterCount { get; private set; }
        public bool FoundSelfReentrantly { get; private set; }

        public override void OnImport(PyCallContext context, PyEnvironment environment)
        {
            EnterCount++;
            FoundSelfReentrantly = environment.InternalTryLoadRootModule(context, Name, out var found)
                && ReferenceEquals(found, this);
        }
    }

    private sealed class FlakyModule : PyModuleObject
    {
        private readonly Func<bool> _shouldFail;

        public FlakyModule(Func<bool> shouldFail) : base("flakymod")
        {
            _shouldFail = shouldFail;
            AppendAttribute("READY", PyBoolObject.True);
        }

        public override void OnImport(PyCallContext context, PyEnvironment environment)
        {
            if (_shouldFail())
                throw context.ValueError("flaky first attempt");
        }
    }

    private sealed class ProbeModule : PyModuleObject
    {
        private readonly SingleModuleProvider _owner;

        public ProbeModule(SingleModuleProvider owner, string name) : base(name)
        {
            _owner = owner;
        }

        public override void OnImport(PyCallContext context, PyEnvironment environment)
        {
            _owner.ProbeOnImport(environment);
        }
    }

    // A package whose __path__ points at a directory the environment path
    // does not cover, so the package itself resolves through the custom
    // provider while its submodule resolves through PathProvider.
    private sealed class CustomPackageModule : PyModuleObject
    {
        public CustomPackageModule() : base("custpkg")
        {
            AppendAttribute("MARK", PyIntObject.FromInteger(41));
            PyAttributes["__path__"] = PyListObject.CreateList(PyStrObject.FromString("/elsewhere/custpkg"));
            PyAttributes["__package__"] = PyStrObject.FromString("custpkg");
        }

        public bool OnImportRan { get; private set; }

        public override void OnImport(PyCallContext context, PyEnvironment environment)
        {
            OnImportRan = true;
        }
    }

    [TestMethod]
    public void AddModuleProvider_ImportsMappingModule_AndFiresOnImport()
    {
        var onImportRan = false;
        var created = 0;
        var provider = PyModuleProvider.Create(new Dictionary<string, Func<PyModuleObject>>
        {
            ["mymod"] = () => { created++; return new OnImportMarked(() => onImportRan = true); },
        });

        var host = HostWith();
        using var environment = host.CreateEnvironmentBuilder()
            .AddModuleProvider(provider)
            .Build();

        var main = Run(environment, "import mymod\nRESULT = mymod.SOURCE");

        Assert.AreEqual("provider", GetStr(main, "RESULT"));
        Assert.IsTrue(onImportRan, "OnImport should fire after the module is loaded");
        Assert.AreEqual(1, created);
        Assert.IsTrue(environment.Modules.ContainsKey("mymod"));
    }

    private sealed class OnImportMarked : PyModuleObject
    {
        private readonly Action _onImport;

        public OnImportMarked(Action onImport) : base("mymod")
        {
            _onImport = onImport;
            AppendAttribute("SOURCE", PyStrObject.FromString("provider"));
        }

        public override void OnImport(PyCallContext context, PyEnvironment environment)
        {
            _onImport();
        }
    }

    [TestMethod]
    public void SelfImportDuringOnImport_FindsRegisteredModule()
    {
        var provider = PyModuleProvider.Create(new Dictionary<string, Func<PyModuleObject>>
        {
            ["selfimp"] = () => new SelfImportingModule(),
        });

        var host = HostWith();
        using var environment = host.CreateEnvironmentBuilder()
            .AddModuleProvider(provider)
            .Build();

        Run(environment, "import selfimp");

        Assert.AreEqual(1, ((SelfImportingModule)environment.Modules["selfimp"]!).EnterCount,
            "a reentrant self import must not reenter the provider");
        Assert.IsTrue(((SelfImportingModule)environment.Modules["selfimp"]!).FoundSelfReentrantly);
    }

    [TestMethod]
    public void RegisterHappensBeforeExecAndOnImport()
    {
        var provider = new SingleModuleProvider("probemod", self => new ProbeModule(self, "probemod"));

        var host = HostWith();
        using var environment = host.CreateEnvironmentBuilder()
            .AddModuleProvider(provider)
            .Build();

        Run(environment, "import probemod");

        Assert.IsFalse(provider.VisibleInCacheDuringCreate, "create runs before registration");
        Assert.IsTrue(provider.VisibleInCacheDuringExec, "exec runs after registration");
        Assert.IsTrue(provider.VisibleInCacheDuringOnImport, "OnImport runs after registration");
        Assert.AreEqual(1, provider.ExecCount);
    }

    [TestMethod]
    public void FailedOnImport_RollsBackCache_AndRetriesOnNextImport()
    {
        var shouldFail = true;
        var created = new List<FlakyModule>();
        var provider = PyModuleProvider.Create(new Dictionary<string, Func<PyModuleObject>>
        {
            ["flakymod"] = () => { var module = new FlakyModule(() => shouldFail); created.Add(module); return module; },
        });

        var host = HostWith();
        using var environment = host.CreateEnvironmentBuilder()
            .AddModuleProvider(provider)
            .Build();

        Assert.ThrowsExactly<PyRuntimeException>(() => Run(environment, "import flakymod"));
        Assert.IsFalse(environment.Modules.ContainsKey("flakymod"),
            "a failed initialization must not leave the module registered");

        shouldFail = false;
        var main = Run(environment, "import flakymod\nRESULT = flakymod.READY");

        Assert.IsTrue(main.PyAttributes.TryGetValue("RESULT", out var result), "RESULT missing");
        Assert.AreSame(PyBoolObject.True, result);
        Assert.HasCount(2, created,
            "a rolled-back import must re-create the module on the next attempt");
        Assert.AreSame(created[1], environment.Modules["flakymod"]);
    }

    [TestMethod]
    public void PackageOnImport_Fires_AndSubmoduleResolvesThroughPath()
    {
        var packageProvider = PyModuleProvider.Create(new Dictionary<string, Func<PyModuleObject>>
        {
            ["custpkg"] = () => new CustomPackageModule(),
        });

        var host = HostWith(("/elsewhere/custpkg/sub.py", "import custpkg\nZ = custpkg.MARK + 1"));
        using var environment = host.CreateEnvironmentBuilder()
            .AddPath("/lib")
            .AddModuleProvider(packageProvider)
            .Build();

        var main = Run(environment, "import custpkg.sub\nRESULT = custpkg.sub.Z");

        Assert.IsTrue(main.PyAttributes.TryGetValue("RESULT", out var result), "RESULT missing");
        Assert.AreEqual(42, ((PyIntObject)result!).Int32Value);
        Assert.IsTrue(((CustomPackageModule)environment.Modules["custpkg"]!).OnImportRan,
            "a package resolved through a custom provider gets OnImport just like plain modules");
    }

    [TestMethod]
    public void InsertModuleProvider_ShadowsPathModule()
    {
        var provider = PyModuleProvider.Create(new Dictionary<string, Func<PyModuleObject>>
        {
            ["dupmod"] = () => new MarkedModule("dupmod"),
        });

        var host = HostWith(("/lib/dupmod.py", "SOURCE = 'path'"));
        using var environment = host.CreateEnvironmentBuilder()
            .AddPath("/lib")
            .InsertModuleProvider(provider)
            .Build();

        var main = Run(environment, "import dupmod\nRESULT = dupmod.SOURCE");

        Assert.AreEqual("provider", GetStr(main, "RESULT"));
    }

    [TestMethod]
    public void AddModuleProvider_PathWinsOverTrailingProvider()
    {
        var provider = PyModuleProvider.Create(new Dictionary<string, Func<PyModuleObject>>
        {
            ["dupmod"] = () => new MarkedModule("dupmod"),
        });

        var host = HostWith(("/lib/dupmod.py", "SOURCE = 'path'"));
        using var environment = host.CreateEnvironmentBuilder()
            .AddPath("/lib")
            .AddModuleProvider(provider)
            .Build();

        var main = Run(environment, "import dupmod\nRESULT = dupmod.SOURCE");

        Assert.AreEqual("path", GetStr(main, "RESULT"));
    }

    [TestMethod]
    public void ClearModuleProviders_EnvironmentUsesOnlyCustomChain()
    {
        var provider = PyModuleProvider.Create(new Dictionary<string, Func<PyModuleObject>>
        {
            ["mysys"] = () => new MarkedModule("mysys"),
        });

        var host = HostWith(("/lib/onlypath.py", "SOURCE = 'path'"));
        using var environment = host.CreateEnvironmentBuilder()
            .AddPath("/lib")
            .ClearModuleProviders()
            .AddModuleProvider(provider)
            .Build();

        var main = Run(environment, "import mysys\nRESULT = mysys.SOURCE");
        Assert.AreEqual("provider", GetStr(main, "RESULT"));

        Assert.ThrowsExactly<PyRuntimeException>(() => Run(environment, "import onlypath"),
            "with the default chain cleared, the path provider is gone");
        Assert.ThrowsExactly<PyRuntimeException>(() => Run(environment, "import math"),
            "with the default chain cleared, the builtin registry is gone too");
    }
}
