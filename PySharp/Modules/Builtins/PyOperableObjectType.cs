using PySharp.Runtime;
using PySharp.Runtime.Calls;
using System.Collections.Generic;
using System.ComponentModel;

namespace PySharp.Modules.Builtins;

/// <summary>
/// The middle layer of the slot layering rework (docs/design/20261007/06
/// and 07): the C-form carrier of the operator slots. Its virtual entries
/// mirror CPython's <c>nb_*</c> / <c>tp_richcompare</c> slots — the
/// receiver of <paramref name="self"/> carries no type promise, a guard
/// over both operands is the implementer's explicit responsibility
/// (CPython static slots such as <c>set_sub</c> check
/// <c>PyAnySet_Check</c> on both sides), and declining means returning
/// NotImplemented so the dispatch skeleton in
/// <see cref="Runtime.PyOperatorProtocol"/> keeps walking.
/// </summary>
/// <remarks>
/// Layering: the slot store (<c>PyTypeSlots</c>) stays pure data; this
/// class is where a type's operator semantics live in C form; the
/// top-level <see cref="PyTypeObject{TObject}"/> keeps the type-safe
/// Python-shaped virtual API for extension authors and bridges into these
/// entries. There are no reflected entries: like CPython's
/// <c>slot_nb_add</c>, the reflection protocol lives inside the slot
/// implementation (forward guard, then the reflected fallback once the
/// forward path declines), never in a separate r* surface.
///
/// Wired into the inheritance chain between <see cref="PyTypeObject"/>
/// and <see cref="PyTypeObject{TObject}"/>: every builtin's operator
/// semantics ride these entries, and the surviving reflected synthesis
/// (<c>FillReflectedSlots</c>) derives the __r*__ dict views from the
/// forward slots.
/// </remarks>
/// <typeparam name="TObject">
/// Retained for the layout pairing with <see cref="PyTypeObject{TObject}"/>;
/// the entries below never constrain <paramref name="self"/> to it.
/// </typeparam>
public abstract partial class PyOperableObjectType<TObject> : PyTypeObject where TObject : PyObject
{
    public PyOperableObjectType()
    {
    }

    public PyOperableObjectType(string qualName, IReadOnlyList<PyTypeObject> bases) : base(qualName, bases)
    {
    }

    // forward binary number slots (nb_add, nb_subtract, ...). Every entry
    // follows the C convention: check both operands, compute, or decline
    // with NotImplemented — never assume the receiver's own layout.
    // [PySlot] is an override-detection marker (see PyTypeObject's
    // PySlotAttribute): an override wires its raw forward slot through
    // FillSlot — no sealed bridge wraps it, the entry is its own
    // order-agnostic slot implementation — and the reflected dict view
    // falls out of the FillReflectedSlots synthesis like any non-null
    // forward slot would
    [PySlot]
    protected internal virtual PyResult NbAdd(PyCallContext context, PyObject self, PyObject other)
        => PyNotImplementedObject.NotImplemented;

    [PySlot]
    protected internal virtual PyResult NbSub(PyCallContext context, PyObject self, PyObject other)
        => PyNotImplementedObject.NotImplemented;

    [PySlot]
    protected internal virtual PyResult NbMul(PyCallContext context, PyObject self, PyObject other)
        => PyNotImplementedObject.NotImplemented;

    [PySlot]
    protected internal virtual PyResult NbMatMul(PyCallContext context, PyObject self, PyObject other)
        => PyNotImplementedObject.NotImplemented;

    [PySlot]
    protected internal virtual PyResult NbTrueDiv(PyCallContext context, PyObject self, PyObject other)
        => PyNotImplementedObject.NotImplemented;

    [PySlot]
    protected internal virtual PyResult NbFloorDiv(PyCallContext context, PyObject self, PyObject other)
        => PyNotImplementedObject.NotImplemented;

    [PySlot]
    protected internal virtual PyResult NbMod(PyCallContext context, PyObject self, PyObject other)
        => PyNotImplementedObject.NotImplemented;

    [PySlot]
    protected internal virtual PyResult NbDivMod(PyCallContext context, PyObject self, PyObject other)
        => PyNotImplementedObject.NotImplemented;

    [PySlot]
    protected internal virtual PyResult NbPow(PyCallContext context, PyObject self, PyObject other, PyObject modulo)
        => PyNotImplementedObject.NotImplemented;

    [PySlot]
    protected internal virtual PyResult NbLShift(PyCallContext context, PyObject self, PyObject other)
        => PyNotImplementedObject.NotImplemented;

    [PySlot]
    protected internal virtual PyResult NbRShift(PyCallContext context, PyObject self, PyObject other)
        => PyNotImplementedObject.NotImplemented;

    [PySlot]
    protected internal virtual PyResult NbAnd(PyCallContext context, PyObject self, PyObject other)
        => PyNotImplementedObject.NotImplemented;

    [PySlot]
    protected internal virtual PyResult NbXor(PyCallContext context, PyObject self, PyObject other)
        => PyNotImplementedObject.NotImplemented;

    [PySlot]
    protected internal virtual PyResult NbOr(PyCallContext context, PyObject self, PyObject other)
        => PyNotImplementedObject.NotImplemented;

    // in-place slots (nb_inplace_add, ...): a NotImplemented result falls
    // back to the forward pair in the skeleton, matching CPython. [PySlot]
    // detection is the same as the forward slots, minus the reflected view
    // (nb_inplace_* has no r* variant)
    [PySlot]
    protected internal virtual PyResult NbInplaceAdd(PyCallContext context, PyObject self, PyObject other)
        => PyNotImplementedObject.NotImplemented;

    [PySlot]
    protected internal virtual PyResult NbInplaceSub(PyCallContext context, PyObject self, PyObject other)
        => PyNotImplementedObject.NotImplemented;

    [PySlot]
    protected internal virtual PyResult NbInplaceMul(PyCallContext context, PyObject self, PyObject other)
        => PyNotImplementedObject.NotImplemented;

    [PySlot]
    protected internal virtual PyResult NbInplaceMatMul(PyCallContext context, PyObject self, PyObject other)
        => PyNotImplementedObject.NotImplemented;

    [PySlot]
    protected internal virtual PyResult NbInplaceTrueDiv(PyCallContext context, PyObject self, PyObject other)
        => PyNotImplementedObject.NotImplemented;

    [PySlot]
    protected internal virtual PyResult NbInplaceFloorDiv(PyCallContext context, PyObject self, PyObject other)
        => PyNotImplementedObject.NotImplemented;

    [PySlot]
    protected internal virtual PyResult NbInplaceMod(PyCallContext context, PyObject self, PyObject other)
        => PyNotImplementedObject.NotImplemented;

    [PySlot]
    protected internal virtual PyResult NbInplacePow(PyCallContext context, PyObject self, PyObject other, PyObject modulo)
        => PyNotImplementedObject.NotImplemented;

    [PySlot]
    protected internal virtual PyResult NbInplaceLShift(PyCallContext context, PyObject self, PyObject other)
        => PyNotImplementedObject.NotImplemented;

    [PySlot]
    protected internal virtual PyResult NbInplaceRShift(PyCallContext context, PyObject self, PyObject other)
        => PyNotImplementedObject.NotImplemented;

    [PySlot]
    protected internal virtual PyResult NbInplaceAnd(PyCallContext context, PyObject self, PyObject other)
        => PyNotImplementedObject.NotImplemented;

    [PySlot]
    protected internal virtual PyResult NbInplaceXor(PyCallContext context, PyObject self, PyObject other)
        => PyNotImplementedObject.NotImplemented;

    [PySlot]
    protected internal virtual PyResult NbInplaceOr(PyCallContext context, PyObject self, PyObject other)
        => PyNotImplementedObject.NotImplemented;

    // tp_richcompare: the single comparison entry — the six-slot mirror
    // (Lt<->Gt, Le<->Ge) and the Py_EQ/Py_NE identity fallbacks live in
    // the skeleton and the public entries, not in per-op slots. [PySlot]
    // detection wires an override with its six fixed-op dict views through
    // the FillRichCompareSlot overload
    [PySlot]
    protected internal virtual PyResult RichCompare(PyCallContext context, PyObject self, PyObject other, PyOperatorTypes op)
        => PyNotImplementedObject.NotImplemented;

    // The SLOT1BINFULL-shaped bridge factory (typeobject.c slot_nb_add is
    // the reference): a slot implementation that runs the type-safe forward
    // virtual entry for a TObject self, and — once that declines, or when
    // the self position carries a foreign object (the original-order call
    // of a right-side type, CPython binary_op1's third step) — falls back
    // to the reflected virtual entry with the pair flipped back. With the
    // reflected slot fields retired, that flipped fallback is what keeps
    // top-level R* overrides reachable from binary dispatch.
    //
    // Frozen-baseline note: the same-layout omission below declines for
    // any TObject other, which is coarser than CPython's exact
    // Py_TYPE(self) != Py_TYPE(other) do_other test. Observable when
    // sibling subtypes share one inherited slot delegate and only the
    // right one overrides R*: the identical-delegate skip folds the pair
    // into the single forward call, where CPython would flip into the
    // right subtype's __radd__ — a registered frozen-baseline divergence
    // (docs/design/20261007/08).
    [EditorBrowsable(EditorBrowsableState.Never)]
    protected PyBinaryFunction BridgeBinarySlot(
        Func<PyCallContext, TObject, PyObject, PyResult> forward,
        Func<PyCallContext, TObject, PyObject, PyResult> reflected)
    {
        return (context, self, other) =>
        {
            if (self is TObject typedSelf)
            {
                var result = forward(context, typedSelf, other);
                if (!result.IsNotImplemented)
                    return result;
                // binary_op1: identical operand types resolve to a single
                // shared slot — the reflected method never runs
                if (other is TObject)
                    return PyNotImplementedObject.NotImplemented;
            }
            if (other is TObject typedOther)
            {
                var result = reflected(context, typedOther, self);
                if (!result.IsNotImplemented)
                    return result;
            }
            return PyNotImplementedObject.NotImplemented;
        };
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    protected PyTernaryFunction BridgeTernarySlot(
        Func<PyCallContext, TObject, PyObject, PyObject, PyResult> forward,
        Func<PyCallContext, TObject, PyObject, PyObject, PyResult> reflected)
    {
        return (context, self, other, third) =>
        {
            if (self is TObject typedSelf)
            {
                var result = forward(context, typedSelf, other, third);
                if (!result.IsNotImplemented)
                    return result;
                if (other is TObject)
                    return PyNotImplementedObject.NotImplemented;
            }
            if (other is TObject typedOther)
            {
                var result = reflected(context, typedOther, self, third);
                if (!result.IsNotImplemented)
                    return result;
            }
            return PyNotImplementedObject.NotImplemented;
        };
    }
}
