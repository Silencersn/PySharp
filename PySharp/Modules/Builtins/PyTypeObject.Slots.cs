using PySharp.Runtime.Calls;

namespace PySharp.Modules.Builtins;

partial class PyTypeObject
{
    // re-derived on every MRO rebuild (type_set_bases' update_all_slots)
    protected internal PyTypeSlots Slots { get; private set; }

    protected internal sealed partial class PyTypeSlots
    {
        // Number and Sequence are the generated protocol families; the rest
        // of the slots stay direct fields until a family needs its own group
        internal PyClsArgsKwargsFunction? New;
        internal PyNumberMethods? Number;
        internal PySequenceMethods? Sequence;

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

        internal sealed partial class PyNumberMethods;

        internal sealed partial class PySequenceMethods;
    }
}
