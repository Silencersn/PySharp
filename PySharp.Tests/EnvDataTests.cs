using PySharp.Modules.Builtins;
using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.Environments;
using PySharp.Runtime.IO;
using PySharp.Runtime.IO.Memory;
using PySharp.Runtime.PyAttributes;

namespace PySharp.Tests;

// The [PyExport]/[PyModuleInclude] faces stay at namespace level, mirroring
// the consumer-assembly shape documented in extending-pysharp: the test pins
// the supported route (static member functions + provider) that reads data
// injected via PyEnvironment.SetEnvData.
internal static partial class EnvDataProbeFunctions
{
    [PyExport("read_marker", nameof(ReadMarkerImpl))]
    public static partial PyBuiltinFunctionOrMethodObject ReadMarker { get; }

    [PyFunctionParameters("key")]
    private static PyResult ReadMarkerImpl(PyCallContext context, PyArguments arguments)
    {
        var key = ((PyStrObject)arguments[0]).Value;
        return context.PyEnvironment.TryGetEnvData<string>(key, out var marker)
            ? PyStrObject.FromString(marker)
            : PyNoneObject.None;
    }

    [PyExport("probe", nameof(ProbeImpl))]
    public static partial PyBuiltinFunctionOrMethodObject Probe { get; }

    [PyFunctionParameters("key")]
    private static PyResult ProbeImpl(PyCallContext context, PyArguments arguments)
    {
        var key = ((PyStrObject)arguments[0]).Value;
        return context.PyEnvironment.TryGetEnvData(key, out _)
            ? PyBoolObject.True
            : PyBoolObject.False;
    }
}

[PyModuleInclude(PyModuleIncludeScheme.StaticMembers, typeof(EnvDataProbeFunctions))]
internal sealed partial class EnvDataProbeModule : PyModuleObject
{
    public EnvDataProbeModule() : base("envprobe") { }
}

[TestClass]
public sealed class EnvDataTests
{
    private sealed class ProviderHost : PyEnvironmentHost
    {
        private readonly IVirtualFileSystem _fileSystem = MemoryFileSystem.CreateBuilder().Build();

        public override Stream AllocateStdIn() => new MemoryStream();
        public override Stream AllocateStdOut() => new MemoryStream();
        public override Stream AllocateStdErr() => new MemoryStream();
        public override IVirtualFileSystem FileSystem => _fileSystem;
    }

    private static PyEnvironment NewEnvironment()
    {
        var provider = PyModuleProvider.Create(new Dictionary<string, Func<PyModuleObject>>
        {
            ["envprobe"] = () => new EnvDataProbeModule(),
        });

        return new ProviderHost().CreateEnvironmentBuilder()
            .AddModuleProvider(provider)
            .Build();
    }

    private static PyModuleObject Run(PyEnvironment environment, string code)
    {
        using var context = PyCallContext.CreateInterpreterRootContext(environment);
        return PyInterpreter.RunCodeWithContext(context, code, "main", "<main>", isMain: true);
    }

    private static PyObject? GetAttr(PyModuleObject module, string name)
    {
        Assert.IsTrue(module.PyAttributes.TryGetValue(name, out var value), $"attribute {name} missing");
        return value;
    }

    [TestMethod]
    public void SetEnvData_ReadableFromExtensionImplementation()
    {
        using var environment = NewEnvironment();
        environment.SetEnvData("marker", "snapshot-42");

        var main = Run(environment, "import envprobe\nRESULT = envprobe.read_marker('marker')");

        Assert.AreEqual("snapshot-42", ((PyStrObject)GetAttr(main, "RESULT")!).Value);
    }

    [TestMethod]
    public void PythonFace_CoversPresenceTypeMismatchAndAbsence()
    {
        using var environment = NewEnvironment();
        environment.SetEnvData("text", "s");
        environment.SetEnvData("num", 7);

        var main = Run(environment, """
            import envprobe
            PRESENT = envprobe.probe('text')
            TYPED = envprobe.read_marker('text')
            MISMATCH = envprobe.read_marker('num')
            ABSENT = envprobe.probe('absent')
            """);

        Assert.AreSame(PyBoolObject.True, GetAttr(main, "PRESENT"));
        Assert.AreEqual("s", ((PyStrObject)GetAttr(main, "TYPED")!).Value);
        Assert.AreSame(PyNoneObject.None, GetAttr(main, "MISMATCH"));
        Assert.AreSame(PyBoolObject.False, GetAttr(main, "ABSENT"));
    }

    [TestMethod]
    public void EnvData_Semantics_OverwriteMissingMismatchAndNullKey()
    {
        using var environment = PyEnvironment.CreateNull();

        environment.SetEnvData("k", 1);
        Assert.IsTrue(environment.TryGetEnvData<int>("k", out var value));
        Assert.AreEqual(1, value);

        environment.SetEnvData("k", 2);
        Assert.IsTrue(environment.TryGetEnvData<int>("k", out value));
        Assert.AreEqual(2, value);

        Assert.IsFalse(environment.TryGetEnvData<int>("absent", out _));

        environment.SetEnvData("s", "text");
        Assert.IsFalse(environment.TryGetEnvData<int>("s", out _), "a type mismatch must read as absent");
        Assert.IsTrue(environment.TryGetEnvData("s", out var raw));
        Assert.AreEqual("text", raw);

        environment.SetEnvData("n", null);
        Assert.IsFalse(environment.TryGetEnvData<object>("n", out _), "a null value matches no T");
        Assert.IsTrue(environment.TryGetEnvData("n", out var nulled));
        Assert.IsNull(nulled);

        environment.SetEnvData("r", 5);
        Assert.IsTrue(environment.RemoveEnvData("r", out var removed));
        Assert.AreEqual(5, removed);
        Assert.IsFalse(environment.TryGetEnvData<int>("r", out _));
        Assert.IsFalse(environment.RemoveEnvData("r", out _));

        Assert.ThrowsExactly<ArgumentNullException>(() => environment.SetEnvData(null!, 1));
        Assert.ThrowsExactly<ArgumentNullException>(() => environment.TryGetEnvData<int>(null!, out _));
    }

    [TestMethod]
    public void EnvData_DoesNotLeakAcrossEnvironments()
    {
        using var first = NewEnvironment();
        first.SetEnvData("marker", "first");

        using var second = NewEnvironment();

        var main = Run(second, "import envprobe\nRESULT = envprobe.read_marker('marker')");

        Assert.AreSame(PyNoneObject.None, GetAttr(main, "RESULT"));
    }

    [TestMethod]
    public void PythonExecution_LeavesInjectedValueIntact()
    {
        var payload = new List<int> { 1, 2, 3 };
        using var environment = NewEnvironment();
        environment.SetEnvData("payload", payload);

        Run(environment, """
            import envprobe
            X = envprobe.probe('payload')
            Y = [i * 2 for i in range(10)]
            """);

        Assert.IsTrue(environment.TryGetEnvData("payload", out var stored));
        Assert.AreSame(payload, stored);
    }

    [TestMethod]
    public void OnDisposing_ReclaimsEnvData_AndFiresOnce()
    {
        var disposed = 0;
        var environment = NewEnvironment();
        environment.SetEnvData("payload", new List<int> { 1, 2, 3 });
        environment.OnDisposing += () =>
        {
            disposed++;
            Assert.IsTrue(environment.TryGetEnvData("payload", out _), "env data is still readable at teardown time");
            environment.RemoveEnvData("payload", out _);
        };

        environment.Dispose();
        environment.Dispose();   // idempotent: the hook must not fire again

        Assert.AreEqual(1, disposed);
        Assert.IsFalse(environment.TryGetEnvData("payload", out _));
    }

    [TestMethod]
    public void OnDisposing_SubscriberException_PropagatesAfterTeardown()
    {
        var environment = NewEnvironment();
        environment.OnDisposing += () => throw new InvalidOperationException("boom");

        // try/finally in Dispose: the teardown completes, then the handler
        // exception flies to the Dispose caller.
        Assert.ThrowsExactly<InvalidOperationException>(
            () => environment.Dispose(),
            "handler exceptions fly after the teardown has completed");

        environment.Dispose();   // idempotent: no second raise, no second throw
    }
}
