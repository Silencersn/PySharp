using System.Reflection;
using PySharp.Modules.Builtins;
using PySharp.Runtime;
using PySharp.Runtime.PyAttributes;

#pragma warning disable MSTEST0037

namespace PySharp.Tests;

// Pins for the two structural invariants the slot machinery relies on but
// cannot express in the type system:
//
//   A. FillSlot shares ONE delegate instance between the slot field and the
//      dict's wrapper descriptor. TrySetWrappedSlot's specific extraction
//      (the generated `_func is TDelegate` test) and the propagation
//      provider-copy rule only stay faithful while this holds.
//   B. Inherited construction delegates keep reference identity: FillNullWith
//      bakes ancestor delegates into static types by reference, and the
//      New/Init provider-copy rule re-wires runtime classes to the provider's
//      own delegate. PyObject.New's default probes (ReferenceEquals against
//      object's delegates) and the slot_inherited-style reflected-synthesis
//      skips read that identity.
//
// Both are CPython idioms (PyWrapperDescrObject.d_wrapped and slot_inherited's
// *slot != *slot_base); CPython itself stores no slot provenance metadata —
// tp_dict is the single source of truth. When one of these invariants breaks,
// the identity-based code paths degrade silently, so the drift must surface
// here rather than at runtime.
[TestClass]
public sealed class SlotInvariantsTests
{
    // object's and type's dict entries for __setattr__/__delattr__ are
    // deliberately NOT the slot delegates: object wraps them in the
    // hackchecked pair (PyObject.FillSlots) and type swaps in
    // TypeSetAttr/TypeDelAttr wrappers (PostConstruct) — both are documented
    // structural divergences from CPython's hackcheck layering
    private static bool IsSetAttrExempt(PyTypeObject type, string name) =>
        (ReferenceEquals(type, PyObjectType.Shared) || ReferenceEquals(type, PyTypeObjectType.Shared))
        && (name is PySpecialNames.SetAttr or PySpecialNames.DelAttr);

    [TestMethod]
    public void BuiltinSlotWrappers_ShareSlotDelegate()
    {
        var violations = new List<string>();
        var (slotMembers, mappingErrors) = BuildSlotMemberMap();
        violations.AddRange(mappingErrors);

        int checkedPairs = 0;
        foreach (var type in EnumerateBuiltinSharedTypes(violations))
        {
            foreach (var name in PyTypeObject.PyTypeSlots.AllSlotNames)
            {
                if (IsSetAttrExempt(type, name))
                    continue;

                if (!type.PyAttributes.TryGetValue(name, out var value))
                    continue;
                if (value is not PyWrapperDescriptorObject wrapper)
                    continue; // __new__ keeps a bound method in the dict, not a wrapper

                if (!slotMembers.TryGetValue(name, out var members) || members.Count is 0)
                {
                    violations.Add($"{type.TpName}.{name}: no slot field or forwarding accessor matches this slot name");
                    continue;
                }

                // one dunder may map to several family slots (__add__ ->
                // Number.Add + Sequence.Concat); the wrapper must share its
                // delegate with the family slot it came from — exactly one
                // of them is filled for every native type
                var slotValues = members
                    .Select(member => member switch
                    {
                        FieldInfo field => field.GetValue(type.Slots),
                        PropertyInfo property => property.GetValue(type.Slots),
                        _ => null,
                    })
                    .ToList();

                checkedPairs++;
                if (!slotValues.Any(slot => slot is not null && ReferenceEquals(slot, wrapper._func)))
                    violations.Add($"{type.TpName}.{name}: slot delegate and wrapper._func must be one shared instance");
            }
        }

        Assert.IsTrue(checkedPairs >= 50, $"unexpectedly few wrapper/slot pairs checked: {checkedPairs}");
        Assert.AreEqual(0, violations.Count, string.Join(Environment.NewLine, violations));
    }

    [TestMethod]
    public void RuntimeSubclass_InheritsSlotDelegateByReference()
    {
        var module = PyInterpreter.RunCode("""
            class PinA: pass
            class PinB(PinA): pass
            class PinIter:
                def __iter__(self): return self
            class PinIterSub(PinIter): pass
            class PinStr(str): pass
            """);
        Assert.IsNotNull(module);

        Assert.IsTrue(module.PyAttributes.TryGetValue("PinA", out var pinAValue));
        Assert.IsTrue(module.PyAttributes.TryGetValue("PinB", out var pinBValue));
        Assert.IsTrue(module.PyAttributes.TryGetValue("PinIter", out var pinIterValue));
        Assert.IsTrue(module.PyAttributes.TryGetValue("PinIterSub", out var pinIterSubValue));
        Assert.IsTrue(module.PyAttributes.TryGetValue("PinStr", out var pinStrValue));
        var pinA = (PyTypeObject)pinAValue;
        var pinB = (PyTypeObject)pinBValue;
        var pinIter = (PyTypeObject)pinIterValue;
        var pinIterSub = (PyTypeObject)pinIterSubValue;
        var pinStr = (PyTypeObject)pinStrValue;

        // object's defaults stay in place until overridden — PyObject.New's
        // ReferenceEquals probes depend on this identity holding for classes
        // that never touched __new__/__init__ — and the New/Init provider-copy
        // rule keeps a native base's construction delegates by reference too
        Assert.AreSame(PyObjectType.Shared.Slots.New, pinA.Slots.New);
        Assert.AreSame(PyObjectType.Shared.Slots.Init, pinA.Slots.Init);
        Assert.AreSame(PyObjectType.Shared.Slots.New, pinB.Slots.New);
        Assert.AreSame(PyObjectType.Shared.Slots.Init, pinB.Slots.Init);
        Assert.AreSame(PyStrObjectType.Shared.Slots.New, pinStr.Slots.New);

        // a dunder nobody provides leaves the slot empty
        Assert.IsNull(pinA.Slots.Iter);

        // a user-function provider resolves through the generic path: the slot
        // carries a converted closure (the analog of CPython's slot_tp_*
        // dispatchers), NOT the ancestor's own delegate — only identity is
        // that the slot is wired at all
        Assert.IsNotNull(pinIter.Slots.Iter);
        Assert.IsNotNull(pinIterSub.Slots.Iter);

        // a native base's dict entry is a wrapper descriptor sharing its slot
        // delegate, so the specific path preserves the exact delegate on the
        // runtime subclass — the same call goes straight to the native code
        Assert.AreSame(PyStrObjectType.Shared.Slots.Repr, pinStr.Slots.Repr);
    }

    // Enumerates every [PyType] registered into builtins as a TypeSingleton
    // and resolves its Shared singleton — the closest thing to a registry of
    // all static native types
    private static IEnumerable<PyTypeObject> EnumerateBuiltinSharedTypes(List<string> violations)
    {
        var seen = new HashSet<PyTypeObject>(ReferenceEqualityComparer.Instance);
        foreach (var attribute in typeof(PyBuiltinsModuleObject).GetCustomAttributes<PyModuleIncludeAttribute>())
        {
            if (attribute.Scheme is not PyModuleIncludeScheme.TypeSingleton)
                continue;

            var shared = attribute.SourceType.GetProperty("Shared", BindingFlags.Public | BindingFlags.Static);
            if (shared?.GetValue(null) is not PyTypeObject type)
            {
                violations.Add($"{attribute.SourceType.Name}: TypeSingleton source type without a Shared PyTypeObject");
                continue;
            }

            if (seen.Add(type))
                yield return type;
        }
    }

    // Maps each slot dunder name to the PyTypeSlots members holding its
    // delegates. Slot fields live on PyTypeSlots either directly (Repr) or
    // behind the generated forwarding accessors for group fields
    // (RAdd => Number.RAdd), so both fields and properties are searched.
    // The member names come from the PySpecialNames constants carrying the
    // same dunder value — the generator derives field names and constant
    // names from the same [PySpecialMethod] declaration. One dunder may
    // carry several constants (Add/Concat = "__add__") mapping to several
    // family slots.
    private static (Dictionary<string, List<MemberInfo>> Map, List<string> Errors) BuildSlotMemberMap()
    {
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        var errors = new List<string>();

        var slotsType = typeof(PyTypeObject).GetNestedType("PyTypeSlots", all);
        if (slotsType is null)
        {
            errors.Add("PyTypeObject.PyTypeSlots not found via reflection");
            return (new Dictionary<string, List<MemberInfo>>(), errors);
        }

        var slotMembers = slotsType.GetMembers(all)
            .Where(m => (m is FieldInfo field && typeof(Delegate).IsAssignableFrom(field.FieldType))
                     || (m is PropertyInfo property && typeof(Delegate).IsAssignableFrom(property.PropertyType)))
            .GroupBy(m => m.Name, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        // dunder value -> every string constant of PySpecialNames carrying it
        var constants = typeof(PySpecialNames).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(string) && f.GetValue(null) is string)
            .Select(f => (Value: (string)f.GetValue(null)!, Name: f.Name))
            .GroupBy(p => p.Value, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Select(p => p.Name).ToArray(), StringComparer.Ordinal);

        var map = new Dictionary<string, List<MemberInfo>>(StringComparer.Ordinal);
        foreach (var name in PyTypeObject.PyTypeSlots.AllSlotNames)
        {
            var members = constants.GetValueOrDefault(name, [])
                .Select(candidate => slotMembers.GetValueOrDefault(candidate))
                .Where(member => member is not null)
                .Cast<MemberInfo>()
                .Distinct()
                .ToList();

            if (members.Count is 0)
                errors.Add($"slot name {name}: no PySpecialNames constant resolves to a PyTypeSlots member");
            map[name] = members;
        }

        return (map, errors);
    }
}
