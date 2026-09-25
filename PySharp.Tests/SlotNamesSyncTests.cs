using System.Reflection;
using PySharp.Modules.Builtins;
using PySharp.Runtime;

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
            .Select(f => (Value: (string)f.GetValue(null)!, Name: f.Name))
            .GroupBy(p => p.Value, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Select(p => p.Name).ToHashSet(StringComparer.Ordinal), StringComparer.Ordinal);

        var allSlotNames = PyTypeObject.PyTypeSlots.AllSlotNames;

        // every slot field is reachable from some AllSlotNames entry
        var uncoveredFields = fieldNames
            .Where(field => !allSlotNames.Any(name => constantNamesByValue.GetValueOrDefault(name, []).Contains(field)))
            .ToList();
        Assert.AreEqual(0, uncoveredFields.Count,
            $"slot fields without an AllSlotNames entry (mutations never propagate, deletions never clear): {string.Join(", ", uncoveredFields)}");

        // every AllSlotNames entry reaches at least one slot field
        var orphanNames = allSlotNames
            .Where(name => !constantNamesByValue.GetValueOrDefault(name, []).Any(fieldNames.Contains))
            .ToList();
        Assert.AreEqual(0, orphanNames.Count,
            $"AllSlotNames entries resolving to no slot field: {string.Join(", ", orphanNames)}");
    }

    private static IEnumerable<string> CollectSlotFieldNames(Type slotsType)
    {
        foreach (var field in slotsType.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance))
            if (typeof(Delegate).IsAssignableFrom(field.FieldType))
                yield return field.Name;
        foreach (var nested in slotsType.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public))
            foreach (var field in nested.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance))
                if (typeof(Delegate).IsAssignableFrom(field.FieldType))
                    yield return field.Name;
    }
}
