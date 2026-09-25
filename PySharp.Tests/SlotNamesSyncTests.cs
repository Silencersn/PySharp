using System.Reflection;
using PySharp.Modules.Builtins;

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
    // mutations never propagate and deletions never clear). This count pin
    // turns that drift into a test failure.
    [TestMethod]
    public void AllSlotNames_CoversEverySlotField()
    {
        var slotsType = typeof(PyTypeObject).GetNestedType("PyTypeSlots", BindingFlags.NonPublic | BindingFlags.Public);
        Assert.IsNotNull(slotsType);

        // count delegate fields across the slots type and every group's
        // nested type, so a future second group (the "support different
        // protocols" TODO) does not turn a fully synced state into a false
        // positive
        int fieldCount = CountDelegateFields(slotsType);
        foreach (var nested in slotsType.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public))
            fieldCount += CountDelegateFields(nested);

        Assert.AreEqual(PyTypeObject.PyTypeSlots.AllSlotNames.Length, fieldCount,
            "PyTypeSlots gained or lost a slot field without a matching AllSlotNames entry (or vice versa)");
    }

    private static int CountDelegateFields(Type type) =>
        type.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
            .Count(f => typeof(Delegate).IsAssignableFrom(f.FieldType));
}
