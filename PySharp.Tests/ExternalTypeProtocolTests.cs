using PySharp.Modules.Builtins;
using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.Environments;
using PySharp.Runtime.IO;
using PySharp.Runtime.IO.Memory;
using PySharp.Runtime.PyAttributes;

namespace PySharp.Tests;

// This assembly consumes PySharp through a ProjectReference, so the public
// PyTypeGenerator runs here exactly as it would inside a consumer package:
// the FillSlots override it emits for [PyType] types dereferences the
// referenced assembly's Slots.* fields and *Bridge wrappers. These types pin
// that consumer wiring end to end — protocol overrides must compile outside
// PySharp AND dispatch from Python (repr/str, len, subscript, +, reflected +).

public sealed class ProbeValueObject : PyObject
{
    public int Count { get; }

    public ProbeValueObject(int count) => Count = count;

    public override PyTypeObject DefaultPyType => ProbeValueObjectType.Shared;
}

[PyType("ProbeValue", Module = "probetypes")]
public sealed partial class ProbeValueObjectType : PyTypeObject<ProbeValueObject>
{
    protected override PyResult New(PyCallContext context, PyTypeObject cls,
        IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        if (args.Count is not 1 || args[0] is not PyIntObject count || !count.IsInt32)
            return PyResult.TypeError("ProbeValue() expects a single int argument");

        return new ProbeValueObject(count.Int32Value);
    }

    protected override PyResult Repr(PyCallContext context, ProbeValueObject self)
        => PyStrObject.FromString($"<ProbeValue {self.Count}>");

    protected override PyResult Len(PyCallContext context, ProbeValueObject self)
        => PyIntObject.FromInteger(self.Count);

    protected override PyResult GetItem(PyCallContext context, ProbeValueObject self, PyObject key)
    {
        if (key is not PyStrObject text)
            return PyResult.TypeError("key must be str");

        return PyStrObject.FromString(string.Concat(Enumerable.Repeat(text.Value, self.Count)));
    }

    protected override PyResult Add(PyCallContext context, ProbeValueObject self, PyObject other)
        => other is PyIntObject number && number.IsInt32
            ? PyIntObject.FromInteger(self.Count + number.Int32Value)
            : PyNotImplementedObject.NotImplemented;
}

[PyModuleInclude(PyModuleIncludeScheme.TypeSingleton, typeof(ProbeValueObjectType))]
internal sealed partial class ProbeTypesModule : PyModuleObject
{
    public ProbeTypesModule() : base("probetypes") { }
}

[TestClass]
public sealed class ExternalTypeProtocolTests
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
            ["probetypes"] = () => new ProbeTypesModule(),
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

    private static PyObject GetAttr(PyModuleObject module, string name)
    {
        Assert.IsTrue(module.PyAttributes.TryGetValue(name, out var value), $"attribute {name} missing");
        return value;
    }

    [TestMethod]
    public void ProtocolOverrides_CompileOutsidePySharp_AndDispatchFromPython()
    {
        using var environment = NewEnvironment();

        var main = Run(environment, """
            from probetypes import ProbeValue
            v = ProbeValue(3)
            assert repr(v) == "<ProbeValue 3>"
            assert str(v) == "<ProbeValue 3>"
            assert len(v) == 3
            assert v["ab"] == "ababab"
            assert v + 4 == 7
            assert v.__repr__() == "<ProbeValue 3>"
            REPR = v.__repr__()
            """);

        Assert.AreEqual("<ProbeValue 3>", ((PyStrObject)GetAttr(main, "REPR")).Value);
    }

    [TestMethod]
    public void GroupSlotOverride_LandsInNumberFamily()
    {
        using var environment = NewEnvironment();

        var main = Run(environment, """
            from probetypes import ProbeValue
            v = ProbeValue(2)
            assert v + 10 == 12
            try:
                "s" + v
            except TypeError:
                pass
            else:
                raise AssertionError("str + ProbeValue must not dispatch to the number slot")
            RESULT = v + 8
            """);

        Assert.AreEqual(10, ((PyIntObject)GetAttr(main, "RESULT")).Int32Value);
    }
}
