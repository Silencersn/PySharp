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

public sealed class ProbeMirrorObject : PyObject
{
    public int Value { get; }

    public ProbeMirrorObject(int value) => Value = value;

    public override PyTypeObject DefaultPyType => ProbeMirrorObjectType.Shared;
}

[PyType("ProbeMirror", Module = "probetypes")]
public sealed partial class ProbeMirrorObjectType : PyTypeObject<ProbeMirrorObject>
{
    protected override PyResult New(PyCallContext context, PyTypeObject cls,
        IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        if (args.Count is not 1 || args[0] is not PyIntObject value || !value.IsInt32)
            return PyResult.TypeError("ProbeMirror() expects a single int argument");

        return new ProbeMirrorObject(value.Int32Value) { _pyType = cls };
    }

    // no [PySlot] on this override on purpose: a consumer assembly cannot
    // spell the private protected attribute, so the base declaration's
    // marker plus the generator's symbol-level inherit lookup is the only
    // wiring path (FillReflectedView over the override)
    protected override PyResult RAdd(PyCallContext context, ProbeMirrorObject self, PyObject other)
        => other is PyIntObject number && number.IsInt32
            ? PyIntObject.FromInteger(number.Int32Value * 100 + self.Value)
            : PyNotImplementedObject.NotImplemented;

    // same marker-less deal for the C-form middle layer: the raw entry
    // lands in the forward slot, and the reflected dict view falls out of
    // the FillReflectedSlots synthesis
    protected internal override PyResult NbSub(PyCallContext context, PyObject self, PyObject other)
        => self is ProbeMirrorObject mirror && other is PyIntObject number && number.IsInt32
            ? PyIntObject.FromInteger(mirror.Value * 100 - number.Int32Value)
            : PyNotImplementedObject.NotImplemented;
}

// the shared-delegate sibling scenario binary_op1 folds into one call: the
// base type carries the forward entry (its NbAdd declines on every call,
// the CPython reference shape `def __add__(self, other): return
// NotImplemented`), the two subtypes share that inherited delegate
// (neither overrides NbAdd), and only the right subtype overrides RAdd —
// pinning how far the sealed bridge's same-layout omission (any TObject
// other declines) is observable against CPython's exact
// Py_TYPE(self) != Py_TYPE(other) do_other test, which would flip the pair
// into the right subtype's __radd__
public class ProbePairBaseObject : PyObject
{
    public override PyTypeObject DefaultPyType => ProbePairBaseObjectType.Shared;
}

public sealed class ProbePairLeftObject : ProbePairBaseObject
{
    public override PyTypeObject DefaultPyType => ProbePairLeftObjectType.Shared;
}

public sealed class ProbePairRightObject : ProbePairBaseObject
{
    public override PyTypeObject DefaultPyType => ProbePairRightObjectType.Shared;
}

[PyType("ProbePairBase", Module = "probetypes")]
public sealed partial class ProbePairBaseObjectType : PyTypeObject<ProbePairBaseObject>
{
    protected override PyResult New(PyCallContext context, PyTypeObject cls,
        IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        if (args.Count is not 0)
            return PyResult.TypeError("ProbePairBase() expects no arguments");

        return new ProbePairBaseObject();
    }

    protected internal override PyResult NbAdd(PyCallContext context, PyObject self, PyObject other)
        => PyNotImplementedObject.NotImplemented;
}

[PyType("ProbePairLeft", Module = "probetypes")]
public sealed partial class ProbePairLeftObjectType : PyTypeObject<ProbePairLeftObject>
{
    public override IReadOnlyList<PyTypeObject> Bases => [ProbePairBaseObjectType.Shared];

    protected override PyResult New(PyCallContext context, PyTypeObject cls,
        IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        if (args.Count is not 0)
            return PyResult.TypeError("ProbePairLeft() expects no arguments");

        return new ProbePairLeftObject { _pyType = cls };
    }
}

[PyType("ProbePairRight", Module = "probetypes")]
public sealed partial class ProbePairRightObjectType : PyTypeObject<ProbePairRightObject>
{
    public override IReadOnlyList<PyTypeObject> Bases => [ProbePairBaseObjectType.Shared];

    protected override PyResult New(PyCallContext context, PyTypeObject cls,
        IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        if (args.Count is not 0)
            return PyResult.TypeError("ProbePairRight() expects no arguments");

        return new ProbePairRightObject { _pyType = cls };
    }

    // only the reflected entry is overridden: the forward Add slot stays
    // the inherited base delegate — the very instance the left subtype
    // resolves — which is what makes EvalBinarySlots' identical-delegate
    // skip fold the pair into the single slotv call
    protected override PyResult RAdd(PyCallContext context, ProbePairRightObject self, PyObject other)
        => PyIntObject.FromInteger(42);
}

[PyModuleInclude(PyModuleIncludeScheme.TypeSingleton, typeof(ProbeValueObjectType))]
[PyModuleInclude(PyModuleIncludeScheme.TypeSingleton, typeof(ProbeMirrorObjectType))]
[PyModuleInclude(PyModuleIncludeScheme.TypeSingleton, typeof(ProbePairBaseObjectType))]
[PyModuleInclude(PyModuleIncludeScheme.TypeSingleton, typeof(ProbePairLeftObjectType))]
[PyModuleInclude(PyModuleIncludeScheme.TypeSingleton, typeof(ProbePairRightObjectType))]
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
    public void ReflectedArithmetic_SynthesizedSlot_DispatchesFromPython()
    {
        using var environment = NewEnvironment();

        var main = Run(environment, """
            from probetypes import ProbeValue
            v = ProbeValue(3)
            # the reflected resolution runs the right type's slot in the
            # original operand order (binary_op1's third step, left operand
            # in the self position); the bridge's flipped fallback lands in
            # ProbeValue.__add__, so an int + ProbeValue works
            assert 4 + v == 7
            assert 10 + v == 13
            # a non-int left operand still declines inside the right
            # type's slot: the reflected fallback must not swallow it
            for left in ("s", 4.5):
                try:
                    left + v
                except TypeError:
                    pass
                else:
                    raise AssertionError(f"{left!r} + ProbeValue must stay a TypeError")
            assert hasattr(type(v), "__radd__")
            """);
    }

    [TestMethod]
    public void SameLayoutSibling_SharedForwardDelegate_FoldsIntoOneCall()
    {
        using var environment = NewEnvironment();

        // known frozen-baseline divergence: CPython's slot_nb_add would
        // flip the pair into the right subtype's __radd__ and resolve to
        // 42; the identical-delegate skip folds the pair into the single
        // forward call, whose NotImplemented ends the resolution
        // (registered in docs/design/20261007/08, section four)
        var main = Run(environment, """
            from probetypes import ProbePairLeft, ProbePairRight
            a = ProbePairLeft()
            b = ProbePairRight()
            assert hasattr(type(b), "__radd__")
            try:
                a + b
            except TypeError:
                RESULT = "TypeError"
            else:
                raise AssertionError("the registered divergence changed: a + b resolved")
            """);

        Assert.AreEqual("TypeError", ((PyStrObject)GetAttr(main, "RESULT")).Value);
    }

    [TestMethod]
    public void ReflectedOverride_WithoutMarker_WiresOutsidePySharp()
    {
        using var environment = NewEnvironment();

        var main = Run(environment, """
            from probetypes import ProbeMirror
            m = ProbeMirror(7)
            # the dict view over the RAdd override must exist without any
            # [PySlot] on the override — consumer assemblies cannot spell
            # the private protected attribute, so only the base
            # declaration's marker plus the generator's inherit lookup can
            # wire it
            assert hasattr(ProbeMirror, "__radd__")
            # a direct attribute call lands in the override (the
            # identifiable arithmetic proves the override body ran), with
            # the receiver as the RIGHT operand
            assert m.__radd__(4) == 407
            # a Python subclass picks the same wrapper up through the MRO;
            # its instances still satisfy the layout guard
            class SubMirror(ProbeMirror):
                pass
            s = SubMirror(3)
            assert hasattr(SubMirror, "__radd__")
            assert s.__radd__(4) == 403
            RESULT = m.__radd__(10)
            """);

        Assert.AreEqual(1007, ((PyIntObject)GetAttr(main, "RESULT")).Int32Value);
    }

    [TestMethod]
    public void CFormOverride_WithoutMarker_WiresOutsidePySharp()
    {
        using var environment = NewEnvironment();

        var main = Run(environment, """
            from probetypes import ProbeMirror
            m = ProbeMirror(7)
            # forward dispatch runs the raw C-form entry in the slot
            assert m - 3 == 697
            # original order: the int side declines, and the mirror slot
            # runs with the LEFT operand in the self position — the CPython
            # static-slot convention, where the double-sided guard declines
            # and the pair stays a TypeError
            try:
                3 - m
            except TypeError:
                pass
            else:
                raise AssertionError("int - ProbeMirror must stay a TypeError")
            # the synthesized reflected view flips only when the other
            # operand carries this layout (wrap_binaryfunc_r)
            assert hasattr(ProbeMirror, "__rsub__")
            assert m.__rsub__(3) is NotImplemented
            RESULT = m - 10
            """);

        Assert.AreEqual(690, ((PyIntObject)GetAttr(main, "RESULT")).Int32Value);
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
