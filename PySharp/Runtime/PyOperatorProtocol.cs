using PySharp.Modules.Builtins;
using PySharp.Runtime.Calls;
using PySharp.Runtime.Comparison;
using System.Diagnostics;

namespace PySharp.Runtime;

// The operator-protocol skeleton: the runtime face of the slot layering
// rework (docs/design/20261007/06 and 07). It centralizes the dispatch
// knowledge PyOperators currently spreads across its two 18-arm switches —
// the forward/reflected slot pairing, the comparison mirror (Lt<->Gt,
// Le<->Ge), the operand-position swap, the sequence fallback and the
// TypeError spellings — expressed against C-form slot accessors the way
// CPython's binary_op1 consumes nb_* slots.
//
// The accessor table is the adaptation view over the current slot store:
// Forward(type) reads the type's forward slot (left-position pass-through
// semantics), Reflected(type) reads the reflected slot (right-position
// swapped semantics; the swap itself lives in EvalReflectiveOperator's
// argument order, exactly where it lives today).
//
// This skeleton is a pure addition during the migration — nothing calls it
// yet. The dispatcher switchover lands as a single revertible commit, and
// the known divergences from CPython (the semantic type-equality check
// below, the simplified divmod protocol kept in PySpecialMethods) are
// carried over verbatim per the frozen-baseline principle.
internal static class PyOperatorProtocol
{
    private static string OperatorToString(PyOperatorTypes op)
    {
        return op switch
        {
            PyOperatorTypes.Add => "+",
            PyOperatorTypes.Sub => "-",
            PyOperatorTypes.Mult => "*",
            PyOperatorTypes.MatMult => "@",
            PyOperatorTypes.TrueDiv => "/",
            PyOperatorTypes.FloorDiv => "//",
            PyOperatorTypes.Mod => "%",
            PyOperatorTypes.Pow => "**",
            PyOperatorTypes.LShift => "<<",
            PyOperatorTypes.RShift => ">>",
            PyOperatorTypes.BitAnd => "&",
            PyOperatorTypes.BitOr => "|",
            PyOperatorTypes.BitXor => "^",
            PyOperatorTypes.Lt => "<",
            PyOperatorTypes.LtE => "<=",
            PyOperatorTypes.Eq => "==",
            PyOperatorTypes.NotEq => "!=",
            PyOperatorTypes.Gt => ">",
            PyOperatorTypes.GtE => ">=",
            _ => throw new UnreachableException(),
        };
    }

    private static bool IsComparisonOp(PyOperatorTypes op)
    {
        return op is PyOperatorTypes.Lt or PyOperatorTypes.LtE or PyOperatorTypes.Eq
            or PyOperatorTypes.NotEq or PyOperatorTypes.Gt or PyOperatorTypes.GtE;
    }

    private static string OperatorTypeErrorName(PyOperatorTypes op)
    {
        return IsComparisonOp(op)
            ? PySR.Runtime_Operator_UnsupportedBetween
            : PySR.Runtime_Operator_UnsupportedOperand;
    }

    // CPython's binop_type_error operator spellings: pow shares one
    // display across ** and pow(), and in-place ops name the augmented
    // form ("+=", "**=", ...) instead of the plain operator.
    private static string OperatorErrorToString(PyOperatorTypes op, bool inPlace)
    {
        var name = OperatorToString(op);
        if (inPlace)
            return name + "=";
        return op is PyOperatorTypes.Pow ? "** or pow()" : name;
    }

    // The adaptation view: the single forward slot per binary operator.
    // The reflected rows and the operand swap are gone — the reflection
    // protocol lives inside the slot implementations (the sealed bridges'
    // flipped fallback, the dict-driven lookups' three-step shape), so the
    // dispatcher only ever calls slots in the original operand order, the
    // way CPython's binary_op1 consumes nb_* slots. No comparison row
    // exists either: the six comparison dunders resolve onto the single
    // RichCompare slot (see SwapComparisonOp), and Eq/NotEq never dispatch
    // through ReflectiveOperator (they go through their own two-sided
    // entries).
    private static Func<PyTypeObject, PyBinaryFunction?> GetForwardSlotAccessor(PyOperatorTypes op)
    {
        return op switch
        {
            PyOperatorTypes.Add => static t => t.Slots.Add,
            PyOperatorTypes.Sub => static t => t.Slots.Sub,
            PyOperatorTypes.Mult => static t => t.Slots.Mul,
            PyOperatorTypes.MatMult => static t => t.Slots.MatMul,
            PyOperatorTypes.TrueDiv => static t => t.Slots.TrueDiv,
            PyOperatorTypes.FloorDiv => static t => t.Slots.FloorDiv,
            PyOperatorTypes.Mod => static t => t.Slots.Mod,
            PyOperatorTypes.LShift => static t => t.Slots.LShift,
            PyOperatorTypes.RShift => static t => t.Slots.RShift,
            PyOperatorTypes.BitAnd => static t => t.Slots.And,
            PyOperatorTypes.BitXor => static t => t.Slots.Xor,
            PyOperatorTypes.BitOr => static t => t.Slots.Or,
            _ => throw new UnreachableException(),
        };
    }

    // _Py_SwappedOp as table data: the right-side view of < is the
    // left-side view of >, and equality mirrors onto itself
    private static PyOperatorTypes SwapComparisonOp(PyOperatorTypes op)
    {
        return op switch
        {
            PyOperatorTypes.Lt => PyOperatorTypes.Gt,
            PyOperatorTypes.LtE => PyOperatorTypes.GtE,
            PyOperatorTypes.Gt => PyOperatorTypes.Lt,
            PyOperatorTypes.GtE => PyOperatorTypes.LtE,
            PyOperatorTypes.Eq or PyOperatorTypes.NotEq => op,
            _ => throw new UnreachableException(),
        };
    }

    // one side of CPython do_richcmp: the receiver type's single
    // comparison slot with its own operand order and op spelling
    private static PyResult EvalCompareSlot(PyCallContext context, PyObject self, PyObject other, PyOperatorTypes op)
    {
        var func = self.PyType.Slots.RichCompare;
        if (func is null)
            return PyNotImplementedObject.NotImplemented;
        return func(context, self, other, op);
    }

    // do_richcmp: the left type's slot as (left, right, op), then — still
    // NotImplemented — the right type's slot with the swapped operands and
    // the mirrored op; comparisons always try both directions
    internal static PyResult EvalCompare(PyCallContext context, PyObject left, PyObject right, PyOperatorTypes op)
    {
        var result = EvalCompareSlot(context, left, right, op);
        if (!result.IsNotImplemented)
            return result;
        return EvalCompareSlot(context, right, left, SwapComparisonOp(op));
    }

    // the right-first order: the right type's slot as (right, left,
    // mirrored op) runs before the left type's forward spelling
    private static PyResult EvalCompareRightFirst(PyCallContext context, PyObject left, PyObject right, PyOperatorTypes op)
    {
        var result = EvalCompareSlot(context, right, left, SwapComparisonOp(op));
        if (!result.IsNotImplemented)
            return result;
        return EvalCompareSlot(context, left, right, op);
    }

    // CPython binary_op1 (Objects/abstract.c) over the forward slots: both
    // slots are called in the ORIGINAL operand order — slotv(v, w) and
    // slotw(v, w) — and differ only in which operand's type supplies the
    // slot; the reflection protocol lives inside the slot itself (the
    // bridges' flipped fallback, the lookups' reflected-name steps). The
    // w-before-v try runs when w's type is a proper subclass of v's, and
    // identical delegates on both sides resolve to the one slotv call
    // (binary_op1's slotw == slotv)
    private static PyResult EvalBinarySlots(PyCallContext context, PyOperatorTypes op, PyObject left, PyObject right, bool rightFirst, bool allowReflected)
    {
        var forward = GetForwardSlotAccessor(op);
        var slotv = forward(left.PyType);
        var slotw = allowReflected ? forward(right.PyType) : null;
        if (ReferenceEquals(slotw, slotv))
            slotw = null;

        PyResult result;
        if (slotv is not null && slotw is not null && rightFirst)
        {
            result = slotw(context, left, right);
            if (!result.IsNotImplemented)
                // error or non-NotImplemented value
                return result;
            slotw = null;
        }
        if (slotv is not null)
        {
            result = slotv(context, left, right);
            if (!result.IsNotImplemented)
                // error or non-NotImplemented value
                return result;
        }
        if (slotw is not null)
        {
            result = slotw(context, left, right);
            if (!result.IsNotImplemented)
                // error or non-NotImplemented value
                return result;
        }
        return PyNotImplementedObject.NotImplemented;
    }

    // binary_op1's ternary shape for pow: the modulo rides along to every
    // call untouched
    private static PyResult EvalPowSlot(PyCallContext context, PyObject left, PyObject right, PyObject modulo, bool rightFirst, bool allowReflected)
    {
        var slotv = left.PyType.Slots.Pow;
        var slotw = allowReflected ? right.PyType.Slots.Pow : null;
        if (ReferenceEquals(slotw, slotv))
            slotw = null;

        PyResult result;
        if (slotv is not null && slotw is not null && rightFirst)
        {
            result = slotw(context, left, right, modulo);
            if (!result.IsNotImplemented)
                return result;
            slotw = null;
        }
        if (slotv is not null)
        {
            result = slotv(context, left, right, modulo);
            if (!result.IsNotImplemented)
                return result;
        }
        if (slotw is not null)
        {
            result = slotw(context, left, right, modulo);
            if (!result.IsNotImplemented)
                return result;
        }
        return PyNotImplementedObject.NotImplemented;
    }

    // The abstract-layer fallback from the number family to the sequence
    // family (Objects/abstract.c): after the nb slots decline, PyNumber_Add
    // tries the left operand's sq_concat only; PyNumber_Multiply tries the
    // left operand's sq_repeat, else the right operand's — a left sequence
    // family takes precedence in both directions, in-place included
    private static PyResult ApplySequenceFallback(PyCallContext context, PyOperatorTypes op, PyObject left, PyObject right, PyResult result, bool inPlace)
    {
        switch (op)
        {
            case PyOperatorTypes.Add:
                {
                    var leftSequence = left.PyType.Slots.Sequence;
                    var concat = inPlace
                        ? leftSequence?.InplaceConcat ?? leftSequence?.Concat
                        : leftSequence?.Concat;
                    if (concat is not null)
                        result = concat(context, left, right);
                    break;
                }
            case PyOperatorTypes.Mult:
                {
                    // CPython: `if (mv && mv->sq_repeat) ... else if (mw &&
                    // mw->sq_repeat)` — the left side wins only by carrying an
                    // actual repeat slot, not by merely having the family struct
                    var leftSequence = left.PyType.Slots.Sequence;
                    var leftRepeat = inPlace
                        ? leftSequence?.InplaceRepeat ?? leftSequence?.Repeat
                        : leftSequence?.Repeat;
                    if (leftRepeat is not null)
                    {
                        result = leftRepeat(context, left, right);
                        break;
                    }
                    // the right operand's plain repeat runs only when the left
                    // operand has no repeat slot at all, and must not mutate it
                    // (abstract.c PyNumber_InPlaceMultiply)
                    var rightRepeat = right.PyType.Slots.Sequence?.Repeat;
                    if (rightRepeat is not null)
                        result = rightRepeat(context, right, left);
                    break;
                }
        }
        return result;
    }

    // the shared tail behind both orders: the abstract-layer sequence
    // fallback, then the binop_type_error spelling
    private static PyResult FinishBinary(PyCallContext context, PyOperatorTypes op, PyObject left, PyObject right, PyResult result, bool inPlace)
    {
        if (result.IsNotImplemented)
            result = ApplySequenceFallback(context, op, left, right, result, inPlace);
        if (result.IsNotImplemented)
            return PyResult.TypeError(OperatorTypeErrorName(op), OperatorErrorToString(op, inPlace), left.PyType.TpName, right.PyType.TpName);

        return result;
    }

    internal static PyResult InPlaceOperator(PyCallContext context, PyOperatorTypes op, PyObject left, PyObject right, PyObject? modulo = null)
    {
        if (left.PyType is PyIntObjectType && right.PyType is PyIntObjectType)
        {
            // Front-load the modulus validation for the ternary pow form so
            // NotImplemented can never escape this fast path (matches the
            // non-integer-modulus rejection inside CalculatePyIntObject).
            if (op is PyOperatorTypes.Pow && modulo is not PyNoneObject && modulo is not PyIntObject)
                return PyResult.TypeError(PySR.Runtime_Number_PowThirdArgNotInteger);
            return PyMath.CalculatePyIntObject(context, op, (PyIntObject)left, (PyIntObject)right, modulo);
        }

        var slots = left.PyType.Slots;
        if (op is not PyOperatorTypes.Pow)
        {
            var func = op switch
            {
                PyOperatorTypes.Add => slots.IAdd,
                PyOperatorTypes.Sub => slots.ISub,
                PyOperatorTypes.Mult => slots.IMul,
                PyOperatorTypes.MatMult => slots.IMatMul,
                PyOperatorTypes.TrueDiv => slots.ITrueDiv,
                PyOperatorTypes.FloorDiv => slots.IFloorDiv,
                PyOperatorTypes.Mod => slots.IMod,
                PyOperatorTypes.LShift => slots.ILShift,
                PyOperatorTypes.RShift => slots.IRShift,
                PyOperatorTypes.BitAnd => slots.IAnd,
                PyOperatorTypes.BitXor => slots.IXor,
                PyOperatorTypes.BitOr => slots.IOr,
                _ => throw new UnreachableException()
            };
            if (func is not null)
            {
                var result = func(context, left, right);
                if (!result.IsNotImplemented)
                    // error or non-NotImplemented value
                    return result;
            }
        }
        else
        {
            var func = slots.IPow;
            if (func is not null)
            {
                Debug.Assert(modulo is not null);
                var result = func(context, left, right, modulo);
                if (!result.IsNotImplemented)
                    // error or non-NotImplemented value
                    return result;
            }
        }
        // the reflective pair runs the forward slots in the original
        // operand order (binary_op1); the sequence fallback and the
        // TypeError spelling live in the shared tail
        return ReflectiveOperator(context, op, left, right, modulo, inPlace: true);
    }

    internal static PyResult ReflectiveOperator(PyCallContext context, PyOperatorTypes op, PyObject left, PyObject right, PyObject? modulo = null, bool inPlace = false)
    {
        if (left.PyType is PyIntObjectType && right.PyType is PyIntObjectType)
        {
            // Front-load the modulus validation for the ternary pow form so
            // NotImplemented can never escape this fast path (matches the
            // non-integer-modulus rejection inside CalculatePyIntObject).
            if (op is PyOperatorTypes.Pow && modulo is not PyNoneObject && modulo is not PyIntObject)
                return PyResult.TypeError(PySR.Runtime_Number_PowThirdArgNotInteger);
            return PyMath.CalculatePyIntObject(context, op, (PyIntObject)left, (PyIntObject)right, modulo);
        }

        // NOTE frozen-baseline divergence: CPython's binary_op1 decides the
        // right-first order with PyType_IsSubtype on the operand types,
        // while this semantic equality check runs __eq__ through the full
        // comparison machinery — the known type-check hijack defect,
        // carried over verbatim until the post-migration alignment pass
        var eq = PyComparer.Eq(context, left.PyType, right.PyType);
        if (eq.IsError)
            return eq;

        var rightFirst = !eq.Value.BoolValue && right.PyType.IsSubclassOf(left.PyType);

        // comparisons route through the single RichCompare slot with the
        // swapped-op mirror — always both directions, no same-type omission
        if (IsComparisonOp(op))
        {
            var compared = rightFirst
                ? EvalCompareRightFirst(context, left, right, op)
                : EvalCompare(context, left, right, op);
            return compared.IsNotImplemented
                ? PyResult.TypeError(OperatorTypeErrorName(op), OperatorErrorToString(op, inPlace: false), left.PyType.TpName, right.PyType.TpName)
                : compared;
        }

        // CPython binary_op1 over the forward slots, original operand
        // order throughout: the reflected spelling lives inside the slot,
        // identical operand types resolve to one shared slot (EvalBinary
        // Slots' allowReflected), and a proper-subclass right operand
        // tries its slot first
        var allowReflected = !eq.Value.BoolValue;
        PyResult result = op is PyOperatorTypes.Pow
            ? EvalPowSlot(context, left, right, modulo!, rightFirst, allowReflected)
            : EvalBinarySlots(context, op, left, right, rightFirst, allowReflected);
        return FinishBinary(context, op, left, right, result, inPlace);
    }

    // The divmod entry frozen as baseline: the left type's forward slot,
    // then the right type's forward slot — both in the original operand
    // order, the reflected spelling living inside the slot — then
    // TypeError; no subclass priority and no same-type omission (a known
    // divergence from CPython's PyNumber_Divmod, which shares binary_op;
    // kept verbatim until the post-migration alignment pass). The
    // identical-delegate skip mirrors binary_op1: a pair resolving to one
    // shared slot is one resolution
    internal static PyResult DivMod(PyCallContext context, PyObject left, PyObject right)
    {
        var slotv = left.PyType.Slots.DivMod;
        var slotw = right.PyType.Slots.DivMod;
        if (ReferenceEquals(slotw, slotv))
            slotw = null;

        if (slotv is not null)
        {
            var result = slotv(context, left, right);
            if (!result.IsNotImplemented)
                return result;
        }
        if (slotw is not null)
        {
            var result = slotw(context, left, right);
            if (!result.IsNotImplemented)
                return result;
        }

        return PyResult.TypeError(PySR.Runtime_Operator_UnsupportedForDivmod, left.PyType.TpName, right.PyType.TpName);
    }
}
