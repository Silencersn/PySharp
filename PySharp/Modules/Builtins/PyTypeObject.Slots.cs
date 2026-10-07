using PySharp.Runtime;
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

        // tp_richcompare: the single comparison entry behind __lt__ ..
        // __ge__ (not a dunder-addressed field, so it is managed manually
        // like New — merged in Create, never a TrySetSlot switch case of
        // its own)
        internal PyRichCompareFunction? RichCompare;

        internal static PyTypeSlots Create(IEnumerable<PyTypeObject> types)
        {
            var slots = new PyTypeSlots();
            foreach (var type in types)
            {
                slots.FillNullWith(type.Slots);
                slots.New ??= type.Slots.New;
                slots.RichCompare ??= type.Slots.RichCompare;
            }
            return slots;
        }

        // The reflected dunders and their forward twins share one slot:
        // either name resolves to the shared dict-driven lookup, so the
        // update machinery asks for the other spelling before clearing
        internal static string? GetConvergedTwinName(string name) => name switch
        {
            PySpecialNames.Add or PySpecialNames.RAdd => name is PySpecialNames.Add ? PySpecialNames.RAdd : PySpecialNames.Add,
            PySpecialNames.Sub or PySpecialNames.RSub => name is PySpecialNames.Sub ? PySpecialNames.RSub : PySpecialNames.Sub,
            PySpecialNames.Mul or PySpecialNames.RMul => name is PySpecialNames.Mul ? PySpecialNames.RMul : PySpecialNames.Mul,
            PySpecialNames.MatMul or PySpecialNames.RMatMul => name is PySpecialNames.MatMul ? PySpecialNames.RMatMul : PySpecialNames.MatMul,
            PySpecialNames.TrueDiv or PySpecialNames.RTrueDiv => name is PySpecialNames.TrueDiv ? PySpecialNames.RTrueDiv : PySpecialNames.TrueDiv,
            PySpecialNames.FloorDiv or PySpecialNames.RFloorDiv => name is PySpecialNames.FloorDiv ? PySpecialNames.RFloorDiv : PySpecialNames.FloorDiv,
            PySpecialNames.Mod or PySpecialNames.RMod => name is PySpecialNames.Mod ? PySpecialNames.RMod : PySpecialNames.Mod,
            PySpecialNames.DivMod or PySpecialNames.RDivMod => name is PySpecialNames.DivMod ? PySpecialNames.RDivMod : PySpecialNames.DivMod,
            PySpecialNames.Pow or PySpecialNames.RPow => name is PySpecialNames.Pow ? PySpecialNames.RPow : PySpecialNames.Pow,
            PySpecialNames.LShift or PySpecialNames.RLShift => name is PySpecialNames.LShift ? PySpecialNames.RLShift : PySpecialNames.LShift,
            PySpecialNames.RShift or PySpecialNames.RRShift => name is PySpecialNames.RShift ? PySpecialNames.RRShift : PySpecialNames.RShift,
            PySpecialNames.And or PySpecialNames.RAnd => name is PySpecialNames.And ? PySpecialNames.RAnd : PySpecialNames.And,
            PySpecialNames.Xor or PySpecialNames.RXor => name is PySpecialNames.Xor ? PySpecialNames.RXor : PySpecialNames.Xor,
            PySpecialNames.Or or PySpecialNames.ROr => name is PySpecialNames.Or ? PySpecialNames.ROr : PySpecialNames.Or,
            _ => null,
        };

        internal sealed partial class PyNumberMethods;

        internal sealed partial class PySequenceMethods;
    }
}
