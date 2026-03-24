using PySharp.Runtime.Calls;

namespace PySharp.Modules.Builtins;

partial class PyTypeObject
{
    protected internal PyTypeSlots Slots { get; }

    protected internal sealed partial class PyTypeSlots
    {
        // TODO: support different protocols

        internal PyClsArgsKwargsFunction? New;

        internal static PyTypeSlots Create(IEnumerable<PyTypeObject> types)
        {
            var slots = new PyTypeSlots();
            foreach (var type in types)
            {
                slots.FillNullWith(type.Slots);
                slots.New ??= type.Slots.New;
            }
            return slots;
        }
    }
}
