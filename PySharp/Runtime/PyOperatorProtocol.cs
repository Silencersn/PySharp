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

    // The adaptation view proper: one accessor pair per binary operator.
    // The comparison rows carry CPython's _Py_SwappedOp mirror as table
    // data — the right-side view of < is the left-side view of >. Eq and
    // NotEq never dispatch through ReflectiveOperator (they go through
    // their own two-sided entries), so they have no row here.
    private static (Func<PyTypeObject, PyBinaryFunction?> Forward, Func<PyTypeObject, PyBinaryFunction?> Reflected) GetBinarySlotAccessors(PyOperatorTypes op)
    {
        return op switch
        {
            PyOperatorTypes.Add => (static t => t.Slots.Add, static t => t.Slots.RAdd),
            PyOperatorTypes.Sub => (static t => t.Slots.Sub, static t => t.Slots.RSub),
            PyOperatorTypes.Mult => (static t => t.Slots.Mul, static t => t.Slots.RMul),
            PyOperatorTypes.MatMult => (static t => t.Slots.MatMul, static t => t.Slots.RMatMul),
            PyOperatorTypes.TrueDiv => (static t => t.Slots.TrueDiv, static t => t.Slots.RTrueDiv),
            PyOperatorTypes.FloorDiv => (static t => t.Slots.FloorDiv, static t => t.Slots.RFloorDiv),
            PyOperatorTypes.Mod => (static t => t.Slots.Mod, static t => t.Slots.RMod),
            PyOperatorTypes.LShift => (static t => t.Slots.LShift, static t => t.Slots.RLShift),
            PyOperatorTypes.RShift => (static t => t.Slots.RShift, static t => t.Slots.RRShift),
            PyOperatorTypes.BitAnd => (static t => t.Slots.And, static t => t.Slots.RAnd),
            PyOperatorTypes.BitXor => (static t => t.Slots.Xor, static t => t.Slots.RXor),
            PyOperatorTypes.BitOr => (static t => t.Slots.Or, static t => t.Slots.ROr),
            PyOperatorTypes.Lt => (static t => t.Slots.Lt, static t => t.Slots.Gt),
            PyOperatorTypes.LtE => (static t => t.Slots.Le, static t => t.Slots.Ge),
            PyOperatorTypes.Gt => (static t => t.Slots.Gt, static t => t.Slots.Lt),
            PyOperatorTypes.GtE => (static t => t.Slots.Ge, static t => t.Slots.Le),
            _ => throw new UnreachableException(),
        };
    }

    internal static PyResult EvalReflectiveOperator(PyCallContext context, PyObject self, PyObject other, PyBinaryFunction? selfFunc, PyBinaryFunction? otherFunc)
    {
        if (selfFunc is not null)
        {
            var result = selfFunc(context, self, other);
            if (!result.IsNotImplemented)
                // error or non-NotImplemented value
                return result;
        }
        if (otherFunc is not null)
        {
            var result = otherFunc(context, other, self);
            if (!result.IsNotImplemented)
                // error or non-NotImplemented value
                return result;
        }
        return PyNotImplementedObject.NotImplemented;
    }

    private static PyResult EvalReflectiveOperator(PyCallContext context, PyObject self, PyObject other, PyObject third, PyTernaryFunction? selfFunc, PyTernaryFunction? otherFunc)
    {
        if (selfFunc is not null)
        {
            var result = selfFunc(context, self, other, third);
            if (!result.IsNotImplemented)
                // error or non-NotImplemented value
                return result;
        }
        if (otherFunc is not null)
        {
            var result = otherFunc(context, other, self, third);
            if (!result.IsNotImplemented)
                // error or non-NotImplemented value
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

    private static PyResult EvalLeftFirst(PyCallContext context, PyOperatorTypes op, PyObject left, PyObject right, PyObject? modulo, bool allowReflected, bool inPlace = false)
    {
        PyResult result;
        var leftType = left.PyType;
        var rightType = right.PyType;
        if (op is PyOperatorTypes.Pow)
        {
            Debug.Assert(modulo is not null);
            result = EvalReflectiveOperator(context, left, right, modulo, leftType.Slots.Pow, allowReflected ? rightType.Slots.RPow : null);
        }
        else
        {
            var (forward, reflected) = GetBinarySlotAccessors(op);
            result = EvalReflectiveOperator(context, left, right, forward(leftType), allowReflected ? reflected(rightType) : null);
        }

        if (result.IsNotImplemented)
            result = ApplySequenceFallback(context, op, left, right, result, inPlace);
        if (result.IsNotImplemented)
            return PyResult.TypeError(OperatorTypeErrorName(op), OperatorErrorToString(op, inPlace), left.PyType.TpName, right.PyType.TpName);

        return result;
    }

    private static PyResult EvalRightFirst(PyCallContext context, PyOperatorTypes op, PyObject left, PyObject right, PyObject? modulo, bool inPlace = false)
    {
        PyResult result;
        var leftType = left.PyType;
        var rightType = right.PyType;
        if (op is PyOperatorTypes.Pow)
        {
            Debug.Assert(modulo is not null);
            result = EvalReflectiveOperator(context, right, left, modulo, rightType.Slots.RPow, leftType.Slots.Pow);
        }
        else
        {
            var (forward, reflected) = GetBinarySlotAccessors(op);
            // the operand positions swap as a whole: the right type's
            // reflected slot runs as (right, left) and the left type's
            // forward slot as (left, right), both via EvalReflective
            // Operator's argument order
            result = EvalReflectiveOperator(context, right, left, reflected(rightType), forward(leftType));
        }

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
        var reflective = ReflectiveOperator(context, op, left, right, modulo, inPlace: true);
        if (reflective.IsNotImplemented)
            reflective = ApplySequenceFallback(context, op, left, right, reflective, inPlace: true);
        return reflective;
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

        if (!eq.Value.BoolValue && right.PyType.IsSubclassOf(left.PyType))
            return EvalRightFirst(context, op, left, right, modulo, inPlace);

        // CPython binary_op1: identical operand types resolve to a single
        // shared slot, so for arithmetic ops only the forward variant is
        // tried and the reflected method never runs; comparisons keep both
        // directions.
        var allowReflected = !eq.Value.BoolValue || IsComparisonOp(op);
        return EvalLeftFirst(context, op, left, right, modulo, allowReflected, inPlace);
    }

    // The divmod entry frozen as baseline: the left type's forward slot,
    // then the right type's reflected slot with swapped operands, then
    // TypeError — no subclass priority and no same-type omission (a known
    // divergence from CPython's PyNumber_Divmod, which shares binary_op;
    // kept verbatim until the post-migration alignment pass)
    internal static PyResult DivMod(PyCallContext context, PyObject left, PyObject right)
    {
        var func = left.PyType.Slots.DivMod;
        if (func is not null)
        {
            var result = func(context, left, right);
            if (!result.IsNotImplemented)
                return result;
        }

        func = right.PyType.Slots.RDivMod;
        if (func is not null)
        {
            var result = func(context, right, left);
            if (!result.IsNotImplemented)
                return result;
        }

        return PyResult.TypeError(PySR.Runtime_Operator_UnsupportedForDivmod, left.PyType.TpName, right.PyType.TpName);
    }
}
