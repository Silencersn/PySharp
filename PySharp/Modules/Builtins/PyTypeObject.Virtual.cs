using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;

namespace PySharp.Modules.Builtins;

partial class PyTypeObject
{
    /// <summary>
    /// Marks a virtual partial method as a Python special method slot.
    /// The <see cref="PyTypeGenerator"/> source generator reads this attribute
    /// and generates a <c>FillSlots()</c> override that wires the method into
    /// the <see cref="PyTypeSlots"/> delegate table, making it callable
    /// through Python's slot dispatch mechanism.
    /// </summary>
    /// <param name="SlotsMember">
    /// Optional. Name of a nested slot group (e.g. <c>&quot;Number&quot;</c>)
    /// inside <see cref="PyTypeSlots"/> where the delegate should be stored.
    /// When set, the generator emits:
    /// <c>FillSlot(name, ref Slots.{SlotsMember}.{MethodName}, {MethodName});</c>
    /// When <c>null</c>, it emits:
    /// <c>FillSlot(name, ref Slots.{MethodName}, {MethodName});</c>
    /// </param>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    private protected sealed class PySlotAttribute : PyAttribute
    {
        public string? SlotsMember { get; set; }

        public PySlotAttribute() { }
    }

    [PySlot]
    protected virtual PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        return PyResult.TypeError(PySR.Runtime_Type_CannotCreateInstance, cls.FullName);
    }
}
