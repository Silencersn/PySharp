using PySharp.Runtime;
using PySharp.Runtime.Calls;

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
/// During the migration this class is not wired into the inheritance
/// chain — pilot types adopt it in stage 3, and the reflected-slot
/// synthesis (<c>FillReflectedSlots</c>) is only retired in stage 4.
/// </remarks>
/// <typeparam name="TObject">
/// Retained for the layout pairing with <see cref="PyTypeObject{TObject}"/>;
/// the entries below never constrain <paramref name="self"/> to it.
/// </typeparam>
public abstract partial class PyOperableObjectType<TObject> : PyTypeObject where TObject : PyObject
{
    // forward binary number slots (nb_add, nb_subtract, ...). Every entry
    // follows the C convention: check both operands, compute, or decline
    // with NotImplemented — never assume the receiver's own layout
    protected internal virtual PyResult NbAdd(PyCallContext context, PyObject self, PyObject other)
        => PyNotImplementedObject.NotImplemented;

    protected internal virtual PyResult NbSub(PyCallContext context, PyObject self, PyObject other)
        => PyNotImplementedObject.NotImplemented;

    protected internal virtual PyResult NbMul(PyCallContext context, PyObject self, PyObject other)
        => PyNotImplementedObject.NotImplemented;

    protected internal virtual PyResult NbMatMul(PyCallContext context, PyObject self, PyObject other)
        => PyNotImplementedObject.NotImplemented;

    protected internal virtual PyResult NbTrueDiv(PyCallContext context, PyObject self, PyObject other)
        => PyNotImplementedObject.NotImplemented;

    protected internal virtual PyResult NbFloorDiv(PyCallContext context, PyObject self, PyObject other)
        => PyNotImplementedObject.NotImplemented;

    protected internal virtual PyResult NbMod(PyCallContext context, PyObject self, PyObject other)
        => PyNotImplementedObject.NotImplemented;

    protected internal virtual PyResult NbDivMod(PyCallContext context, PyObject self, PyObject other)
        => PyNotImplementedObject.NotImplemented;

    protected internal virtual PyResult NbPow(PyCallContext context, PyObject self, PyObject other, PyObject modulo)
        => PyNotImplementedObject.NotImplemented;

    protected internal virtual PyResult NbLShift(PyCallContext context, PyObject self, PyObject other)
        => PyNotImplementedObject.NotImplemented;

    protected internal virtual PyResult NbRShift(PyCallContext context, PyObject self, PyObject other)
        => PyNotImplementedObject.NotImplemented;

    protected internal virtual PyResult NbAnd(PyCallContext context, PyObject self, PyObject other)
        => PyNotImplementedObject.NotImplemented;

    protected internal virtual PyResult NbXor(PyCallContext context, PyObject self, PyObject other)
        => PyNotImplementedObject.NotImplemented;

    protected internal virtual PyResult NbOr(PyCallContext context, PyObject self, PyObject other)
        => PyNotImplementedObject.NotImplemented;

    // in-place slots (nb_inplace_add, ...): a NotImplemented result falls
    // back to the forward pair in the skeleton, matching CPython
    protected internal virtual PyResult NbInplaceAdd(PyCallContext context, PyObject self, PyObject other)
        => PyNotImplementedObject.NotImplemented;

    protected internal virtual PyResult NbInplaceSub(PyCallContext context, PyObject self, PyObject other)
        => PyNotImplementedObject.NotImplemented;

    protected internal virtual PyResult NbInplaceMul(PyCallContext context, PyObject self, PyObject other)
        => PyNotImplementedObject.NotImplemented;

    protected internal virtual PyResult NbInplaceMatMul(PyCallContext context, PyObject self, PyObject other)
        => PyNotImplementedObject.NotImplemented;

    protected internal virtual PyResult NbInplaceTrueDiv(PyCallContext context, PyObject self, PyObject other)
        => PyNotImplementedObject.NotImplemented;

    protected internal virtual PyResult NbInplaceFloorDiv(PyCallContext context, PyObject self, PyObject other)
        => PyNotImplementedObject.NotImplemented;

    protected internal virtual PyResult NbInplaceMod(PyCallContext context, PyObject self, PyObject other)
        => PyNotImplementedObject.NotImplemented;

    protected internal virtual PyResult NbInplacePow(PyCallContext context, PyObject self, PyObject other, PyObject modulo)
        => PyNotImplementedObject.NotImplemented;

    protected internal virtual PyResult NbInplaceLShift(PyCallContext context, PyObject self, PyObject other)
        => PyNotImplementedObject.NotImplemented;

    protected internal virtual PyResult NbInplaceRShift(PyCallContext context, PyObject self, PyObject other)
        => PyNotImplementedObject.NotImplemented;

    protected internal virtual PyResult NbInplaceAnd(PyCallContext context, PyObject self, PyObject other)
        => PyNotImplementedObject.NotImplemented;

    protected internal virtual PyResult NbInplaceXor(PyCallContext context, PyObject self, PyObject other)
        => PyNotImplementedObject.NotImplemented;

    protected internal virtual PyResult NbInplaceOr(PyCallContext context, PyObject self, PyObject other)
        => PyNotImplementedObject.NotImplemented;

    // tp_richcompare: the single comparison entry — the six-slot mirror
    // (Lt<->Gt, Le<->Ge) and the Py_EQ/Py_NE identity fallbacks live in
    // the skeleton and the public entries, not in per-op slots
    protected internal virtual PyResult RichCompare(PyCallContext context, PyObject self, PyObject other, PyOperatorTypes op)
        => PyNotImplementedObject.NotImplemented;
}
