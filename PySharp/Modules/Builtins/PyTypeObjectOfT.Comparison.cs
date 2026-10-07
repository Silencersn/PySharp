using PySharp.Runtime;
using PySharp.Runtime.Calls;
using System.ComponentModel;

namespace PySharp.Modules.Builtins;

partial class PyTypeObject<TObject>
{
    // The default comparison bridge: the type-safe six-method surface
    // (Lt/Le/Eq/Ne/Gt/Ge overrides plus the object defaults) folded into
    // the single tp_richcompare-shaped slot entry. The receiver guard
    // declines for a foreign self; the mirror pairing and the Eq/Ne
    // identity fallbacks stay in the dispatch skeleton, exactly where
    // CPython keeps _Py_SwappedOp and do_richcmp.
    [EditorBrowsable(EditorBrowsableState.Never)]
    protected PyRichCompareFunction CreateRichCompareBridge()
    {
        return (context, self, other, op) => self is TObject selfOfT
            ? op switch
            {
                PyOperatorTypes.Lt => Lt(context, selfOfT, other),
                PyOperatorTypes.LtE => Le(context, selfOfT, other),
                PyOperatorTypes.Eq => Eq(context, selfOfT, other),
                PyOperatorTypes.NotEq => Ne(context, selfOfT, other),
                PyOperatorTypes.Gt => Gt(context, selfOfT, other),
                PyOperatorTypes.GtE => Ge(context, selfOfT, other),
                _ => PyNotImplementedObject.NotImplemented,
            }
            : PyNotImplementedObject.NotImplemented;
    }

    // Wires the RichCompare slot and the six dict wrappers. The attribute
    // face is a fixed-op view over the slot delegate (wrap_richcmpfunc),
    // so a wrapper never shares the slot's instance — unlike FillSlot,
    // which exists precisely to share it.
    [EditorBrowsable(EditorBrowsableState.Never)]
    protected void FillRichCompareSlot()
    {
        PyRichCompareFunction richCompare = CreateRichCompareBridge();
        Slots.RichCompare = richCompare;
        FillComparisonWrapper(PySpecialNames.Lt, richCompare, PyOperatorTypes.Lt);
        FillComparisonWrapper(PySpecialNames.Le, richCompare, PyOperatorTypes.LtE);
        FillComparisonWrapper(PySpecialNames.Eq, richCompare, PyOperatorTypes.Eq);
        FillComparisonWrapper(PySpecialNames.Ne, richCompare, PyOperatorTypes.NotEq);
        FillComparisonWrapper(PySpecialNames.Gt, richCompare, PyOperatorTypes.Gt);
        FillComparisonWrapper(PySpecialNames.Ge, richCompare, PyOperatorTypes.GtE);
    }

    private protected void FillComparisonWrapper(string name, PyRichCompareFunction richCompare, PyOperatorTypes op)
    {
        PyBinaryFunction view = (context, self, other) => richCompare(context, self, other, op);
        PyAttributes[name] = new PyWrapperDescriptorObject(view);
    }
}
