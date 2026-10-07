using PySharp.Modules.Builtins;
using PySharp.Runtime;
using System.Reflection;

#pragma warning disable MSTEST0037

namespace PySharp.Tests;

[TestClass]
public sealed class SlotNamesSyncTests
{
    // The generated slot-name list must stay in lockstep with the PyTypeSlots
    // fields. Declaration-driven slots sync automatically (the generator
    // derives fields, switch branches and AllSlotNames from the same
    // [PySpecialMethod] set), but __new__ is a manually managed field whose
    // switch branches are hardcoded — adding a manual slot field without
    // updating the list fails silently at runtime (IsSlotName false, so
    // mutations never propagate and deletions never clear).
    //
    // The relation is many-to-one since protocol families landed: one dunder
    // name may map to several slot fields (__add__ -> Number.Add +
    // Sequence.Concat), so a bare count no longer pins the sync — coverage
    // in both directions does.
    // The six comparison dunders resolve onto the manually managed
    // RichCompare field (their TrySetSlot cases install the shared
    // dict-driven delegate). The field is reachable through any of the
    // six names instead of a same-named constant, and the names reach no
    // same-named field — the convergence is asserted explicitly below.
    private static readonly HashSet<string> ConvergedComparisonNames =
    [
        PySpecialNames.Lt,
        PySpecialNames.Le,
        PySpecialNames.Eq,
        PySpecialNames.Ne,
        PySpecialNames.Gt,
        PySpecialNames.Ge,
    ];

    private const string ConvergedComparisonField = "RichCompare";

    // reflected dunder -> its forward twin: the reflected names resolve
    // onto the forward slots (the reflection protocol lives inside the
    // slot, CPython SLOT1BINFULL), so reachability runs through the
    // forward constants — the same convergence the comparison names take
    // through RichCompare
    private static readonly Dictionary<string, string> ReflectedDunderToForward = new()
    {
        [PySpecialNames.RAdd] = PySpecialNames.Add,
        [PySpecialNames.RSub] = PySpecialNames.Sub,
        [PySpecialNames.RMul] = PySpecialNames.Mul,
        [PySpecialNames.RMatMul] = PySpecialNames.MatMul,
        [PySpecialNames.RTrueDiv] = PySpecialNames.TrueDiv,
        [PySpecialNames.RFloorDiv] = PySpecialNames.FloorDiv,
        [PySpecialNames.RMod] = PySpecialNames.Mod,
        [PySpecialNames.RDivMod] = PySpecialNames.DivMod,
        [PySpecialNames.RPow] = PySpecialNames.Pow,
        [PySpecialNames.RLShift] = PySpecialNames.LShift,
        [PySpecialNames.RRShift] = PySpecialNames.RShift,
        [PySpecialNames.RAnd] = PySpecialNames.And,
        [PySpecialNames.RXor] = PySpecialNames.Xor,
        [PySpecialNames.ROr] = PySpecialNames.Or,
    };

    [TestMethod]
    public void AllSlotNames_CoversEverySlotField()
    {
        var slotsType = typeof(PyTypeObject).GetNestedType("PyTypeSlots", BindingFlags.NonPublic | BindingFlags.Public);
        Assert.IsNotNull(slotsType);

        var fieldNames = CollectSlotFieldNames(slotsType).ToHashSet(StringComparer.Ordinal);

        // dunder value -> the PySpecialNames constant names carrying it; a
        // slot field is reachable from AllSlotNames through the constant of
        // the same name (Add, Concat => "__add__")
        var constantNamesByValue = typeof(PySpecialNames).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(string) && f.GetValue(null) is string)
            .Select(f => (Value: (string)f.GetValue(null)!, f.Name))
            .GroupBy(p => p.Value, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Select(p => p.Name).ToHashSet(StringComparer.Ordinal), StringComparer.Ordinal);

        var allSlotNames = PyTypeObject.PyTypeSlots.AllSlotNames;

        // the convergence stays paired: the shared field exists and every
        // one of its six names is listed
        Assert.IsTrue(fieldNames.Contains(ConvergedComparisonField),
            $"{ConvergedComparisonField} field missing while comparison dunders are slot names");
        CollectionAssert.IsSubsetOf(ConvergedComparisonNames.ToList(), allSlotNames.ToList(),
            "comparison dunders must stay in AllSlotNames or mutations stop propagating");

        // every slot field is reachable from some AllSlotNames entry
        var uncoveredFields = fieldNames
            .Where(field => field is not ConvergedComparisonField)
            .Where(field => !allSlotNames.Any(name => constantNamesByValue.GetValueOrDefault(name, []).Contains(field)))
            .ToList();
        Assert.AreEqual(0, uncoveredFields.Count,
            $"slot fields without an AllSlotNames entry (mutations never propagate, deletions never clear): {string.Join(", ", uncoveredFields)}");

        // every AllSlotNames entry reaches at least one slot field — the
        // reflected dunders through their forward twins' constants
        var orphanNames = allSlotNames
            .Where(name => !ConvergedComparisonNames.Contains(name))
            .Where(name => !constantNamesByValue.GetValueOrDefault(ReflectedDunderToForward.GetValueOrDefault(name, name), []).Any(fieldNames.Contains))
            .ToList();
        Assert.AreEqual(0, orphanNames.Count,
            $"AllSlotNames entries resolving to no slot field: {string.Join(", ", orphanNames)}");
    }

    private static IEnumerable<string> CollectSlotFieldNames(Type slotsType)
    {
        foreach (var field in slotsType.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance))
        {
            if (typeof(Delegate).IsAssignableFrom(field.FieldType))
                yield return field.Name;
        }
        foreach (var nested in slotsType.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public))
        {
            foreach (var field in nested.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance))
            {
                if (typeof(Delegate).IsAssignableFrom(field.FieldType))
                    yield return field.Name;
            }
        }
    }
}
