using PySharp.Modules.Builtins;
using PySharp.Runtime.Calls;

namespace PySharp.Runtime;

public enum PyOperatorTypes
{
    Add,
    Sub,
    Mult,
    MatMult,
    TrueDiv,
    FloorDiv,
    Mod,
    Pow,
    LShift,
    RShift,
    BitAnd,
    BitOr,
    BitXor,
    Lt,
    LtE,
    Eq,
    NotEq,
    Gt,
    GtE
}

// The public operator entries. Since the slot layering rework the binary
// dispatch knowledge — forward/reflected slot pairing, the comparison
// mirror, the operand-position swap, the same-type omission and the
// sequence fallback — lives in PyOperatorProtocol (the operator-protocol
// skeleton); these entries only keep the stable call surface and the
// identity fallbacks of ==/!=.
public static class PyOperators
{
    public static PyResult Add(PyCallContext context, PyObject left, PyObject right)
    {
        return PyOperatorProtocol.ReflectiveOperator(context, PyOperatorTypes.Add, left, right);
    }
    public static PyResult Sub(PyCallContext context, PyObject left, PyObject right)
    {
        return PyOperatorProtocol.ReflectiveOperator(context, PyOperatorTypes.Sub, left, right);
    }
    public static PyResult Mult(PyCallContext context, PyObject left, PyObject right)
    {
        return PyOperatorProtocol.ReflectiveOperator(context, PyOperatorTypes.Mult, left, right);
    }
    public static PyResult MatMult(PyCallContext context, PyObject left, PyObject right)
    {
        return PyOperatorProtocol.ReflectiveOperator(context, PyOperatorTypes.MatMult, left, right);
    }
    public static PyResult TrueDiv(PyCallContext context, PyObject left, PyObject right)
    {
        return PyOperatorProtocol.ReflectiveOperator(context, PyOperatorTypes.TrueDiv, left, right);
    }
    public static PyResult FloorDiv(PyCallContext context, PyObject left, PyObject right)
    {
        return PyOperatorProtocol.ReflectiveOperator(context, PyOperatorTypes.FloorDiv, left, right);
    }
    public static PyResult Mod(PyCallContext context, PyObject left, PyObject right)
    {
        return PyOperatorProtocol.ReflectiveOperator(context, PyOperatorTypes.Mod, left, right);
    }
    public static PyResult Pow(PyCallContext context, PyObject left, PyObject right, PyObject modulo)
    {
        return PyOperatorProtocol.ReflectiveOperator(context, PyOperatorTypes.Pow, left, right, modulo);
    }
    public static PyResult LShift(PyCallContext context, PyObject left, PyObject right)
    {
        return PyOperatorProtocol.ReflectiveOperator(context, PyOperatorTypes.LShift, left, right);
    }
    public static PyResult RShift(PyCallContext context, PyObject left, PyObject right)
    {
        return PyOperatorProtocol.ReflectiveOperator(context, PyOperatorTypes.RShift, left, right);
    }
    public static PyResult BitAnd(PyCallContext context, PyObject left, PyObject right)
    {
        return PyOperatorProtocol.ReflectiveOperator(context, PyOperatorTypes.BitAnd, left, right);
    }
    public static PyResult BitXor(PyCallContext context, PyObject left, PyObject right)
    {
        return PyOperatorProtocol.ReflectiveOperator(context, PyOperatorTypes.BitXor, left, right);
    }
    public static PyResult BitOr(PyCallContext context, PyObject left, PyObject right)
    {
        return PyOperatorProtocol.ReflectiveOperator(context, PyOperatorTypes.BitOr, left, right);
    }
    public static PyResult Lt(PyCallContext context, PyObject left, PyObject right)
    {
        return PyOperatorProtocol.ReflectiveOperator(context, PyOperatorTypes.Lt, left, right);
    }
    public static PyResult LtE(PyCallContext context, PyObject left, PyObject right)
    {
        return PyOperatorProtocol.ReflectiveOperator(context, PyOperatorTypes.LtE, left, right);
    }
    public static PyResult Gt(PyCallContext context, PyObject left, PyObject right)
    {
        return PyOperatorProtocol.ReflectiveOperator(context, PyOperatorTypes.Gt, left, right);
    }
    public static PyResult GtE(PyCallContext context, PyObject left, PyObject right)
    {
        return PyOperatorProtocol.ReflectiveOperator(context, PyOperatorTypes.GtE, left, right);
    }

    public static PyResult InPlaceAdd(PyCallContext context, PyObject left, PyObject right)
    {
        return PyOperatorProtocol.InPlaceOperator(context, PyOperatorTypes.Add, left, right);
    }
    public static PyResult InPlaceSub(PyCallContext context, PyObject left, PyObject right)
    {
        return PyOperatorProtocol.InPlaceOperator(context, PyOperatorTypes.Sub, left, right);
    }
    public static PyResult InPlaceMult(PyCallContext context, PyObject left, PyObject right)
    {
        return PyOperatorProtocol.InPlaceOperator(context, PyOperatorTypes.Mult, left, right);
    }
    public static PyResult InPlaceMatMult(PyCallContext context, PyObject left, PyObject right)
    {
        return PyOperatorProtocol.InPlaceOperator(context, PyOperatorTypes.MatMult, left, right);
    }
    public static PyResult InPlaceTrueDiv(PyCallContext context, PyObject left, PyObject right)
    {
        return PyOperatorProtocol.InPlaceOperator(context, PyOperatorTypes.TrueDiv, left, right);
    }
    public static PyResult InPlaceFloorDiv(PyCallContext context, PyObject left, PyObject right)
    {
        return PyOperatorProtocol.InPlaceOperator(context, PyOperatorTypes.FloorDiv, left, right);
    }
    public static PyResult InPlaceMod(PyCallContext context, PyObject left, PyObject right)
    {
        return PyOperatorProtocol.InPlaceOperator(context, PyOperatorTypes.Mod, left, right);
    }
    public static PyResult InPlacePow(PyCallContext context, PyObject left, PyObject right, PyObject modulo)
    {
        return PyOperatorProtocol.InPlaceOperator(context, PyOperatorTypes.Pow, left, right, modulo);
    }
    public static PyResult InPlaceLShift(PyCallContext context, PyObject left, PyObject right)
    {
        return PyOperatorProtocol.InPlaceOperator(context, PyOperatorTypes.LShift, left, right);
    }
    public static PyResult InPlaceRShift(PyCallContext context, PyObject left, PyObject right)
    {
        return PyOperatorProtocol.InPlaceOperator(context, PyOperatorTypes.RShift, left, right);
    }
    public static PyResult InPlaceBitAnd(PyCallContext context, PyObject left, PyObject right)
    {
        return PyOperatorProtocol.InPlaceOperator(context, PyOperatorTypes.BitAnd, left, right);
    }
    public static PyResult InPlaceBitXor(PyCallContext context, PyObject left, PyObject right)
    {
        return PyOperatorProtocol.InPlaceOperator(context, PyOperatorTypes.BitXor, left, right);
    }
    public static PyResult InPlaceBitOr(PyCallContext context, PyObject left, PyObject right)
    {
        return PyOperatorProtocol.InPlaceOperator(context, PyOperatorTypes.BitOr, left, right);
    }

    public static PyResult Eq(PyCallContext context, PyObject left, PyObject right)
    {
        // both sides spell the op as itself (_Py_SwappedOp[Py_EQ] is Py_EQ)
        var result = PyOperatorProtocol.EvalCompare(context, left, right, PyOperatorTypes.Eq);
        if (!result.IsNotImplemented)
            // error or non-NotImplemented value
            return result;

        return Is(left, right);
    }
    public static PyResult NotEq(PyCallContext context, PyObject left, PyObject right)
    {
        var neResult = PyOperatorProtocol.EvalCompare(context, left, right, PyOperatorTypes.NotEq);
        if (!neResult.IsNotImplemented)
            // error or non-NotImplemented value
            return neResult;

        // CPython do_richcompare: once both sides' __ne__ return
        // NotImplemented, Py_NE falls back to the identity check — __eq__
        // is not consulted (its inversion lives in object.__ne__, which
        // the slot dispatch reaches whenever no __ne__ is defined)
        return IsNot(left, right);
    }

    public static PyBoolObject Is(PyObject left, PyObject right)
    {
        return PyBoolObject.FromBoolean(ReferenceEquals(left, right));
    }
    public static PyBoolObject IsNot(PyObject left, PyObject right)
    {
        return PyBoolObject.FromBoolean(!ReferenceEquals(left, right));
    }

    public static PyResult<PyBoolObject> In(PyCallContext context, PyObject left, PyObject right)
    {
        var contains = PySpecialMethods.Contains(context, right, left);
        if (contains.IsError)
            return contains.ExceptionResult;
        return PySpecialMethods.Bool(context, contains.Value);
    }
    public static PyResult<PyBoolObject> NotIn(PyCallContext context, PyObject left, PyObject right)
    {
        var result = In(context, left, right);
        if (result.IsError)
            return result;

        return Not(context, result.Value);
    }

    public static PyResult GetAttr(PyCallContext context, PyObject target, string name)
    {
        return GetAttr(context, target, context.PyEnvironment.InternPool.Intern(name));
    }
    public static PyResult SetAttr(PyCallContext context, PyObject target, string name, PyObject value)
    {
        return SetAttr(context, target, context.PyEnvironment.InternPool.Intern(name), value);
    }
    public static PyResult DelAttr(PyCallContext context, PyObject target, string name)
    {
        return DelAttr(context, target, context.PyEnvironment.InternPool.Intern(name));
    }
    public static PyResult GetAttr(PyCallContext context, PyObject target, PyObject name)
    {
        var getAttributeFunc = target.PyType.Slots.GetAttribute ?? PyTypeObject.DefaultGetAttribute;
        var attr = getAttributeFunc(context, target, name);
        if (!attr.IsAttributeError)
            return attr;

        var getAttrFunc = target.PyType.Slots.GetAttr;
        if (getAttrFunc is not null)
            return getAttrFunc(context, target, name);

        return attr;
    }
    public static PyResult SetAttr(PyCallContext context, PyObject target, PyObject name, PyObject value)
    {
        var func = target.PyType.Slots.SetAttr ?? PyTypeObject.DefaultSetAttr;
        return func(context, target, name, value);
    }
    public static PyResult DelAttr(PyCallContext context, PyObject target, PyObject name)
    {
        var func = target.PyType.Slots.DelAttr ?? PyTypeObject.DefaultDelAttr;
        return func(context, target, name);
    }

    public static PyResult<PyBoolObject> Not(PyCallContext context, PyObject value)
    {
        var result = PySpecialMethods.Bool(context, value);
        if (result.IsError)
            return result;
        return PyBoolObject.FromBoolean(!result.Value.BoolValue);
    }

    private static PyResult EvalUnaryOperator(PyCallContext context, PyObject value, PyUnaryFunction? func, char op)
    {
        // CPython's unary slot wrappers pass the hook result through
        // unchecked — a hook returning NotImplemented yields the singleton
        // itself (unlike binary slots); only a missing slot raises here
        if (func is not null)
            return func(context, value);

        return PyResult.TypeError(PySR.Runtime_Operator_UnsupportedForUnary, op, value.PyType.TpName);
    }

    public static PyResult Invert(PyCallContext context, PyObject value)
    {
        return EvalUnaryOperator(context, value, value.PyType.Slots.Invert, '~');
    }

    public static PyResult UAdd(PyCallContext context, PyObject value)
    {
        return EvalUnaryOperator(context, value, value.PyType.Slots.Pos, '+');
    }

    public static PyResult USub(PyCallContext context, PyObject value)
    {
        return EvalUnaryOperator(context, value, value.PyType.Slots.Neg, '-');
    }
}
