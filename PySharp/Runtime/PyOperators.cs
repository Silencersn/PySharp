using PySharp.Modules.Builtins;
using PySharp.Runtime.Calls;
using System.Diagnostics;

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

public static class PyOperators
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


    private static PyResult EvalReflectiveOperator(PyCallContext context, PyObject self, PyObject other, PyBinaryFunction? selfFunc, PyBinaryFunction? otherFunc)
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

    private static PyResult EvalLeftFirstReflectiveOperator(PyCallContext context, PyOperatorTypes op, PyObject left, PyObject right, PyObject? modulo)
    {
        PyResult result;
        var leftType = left.PyType;
        var rightType = right.PyType;
        switch (op)
        {
            case PyOperatorTypes.Add:
                result = EvalReflectiveOperator(context, left, right, leftType.Slots.Add, rightType.Slots.RAdd);
                break;
            case PyOperatorTypes.Sub:
                result = EvalReflectiveOperator(context, left, right, leftType.Slots.Sub, rightType.Slots.RSub);
                break;
            case PyOperatorTypes.Mult:
                result = EvalReflectiveOperator(context, left, right, leftType.Slots.Mul, rightType.Slots.RMul);
                break;
            case PyOperatorTypes.MatMult:
                result = EvalReflectiveOperator(context, left, right, leftType.Slots.MatMul, rightType.Slots.RMatMul);
                break;
            case PyOperatorTypes.TrueDiv:
                result = EvalReflectiveOperator(context, left, right, leftType.Slots.TrueDiv, rightType.Slots.RTrueDiv);
                break;
            case PyOperatorTypes.FloorDiv:
                result = EvalReflectiveOperator(context, left, right, leftType.Slots.FloorDiv, rightType.Slots.RFloorDiv);
                break;
            case PyOperatorTypes.Mod:
                result = EvalReflectiveOperator(context, left, right, leftType.Slots.Mod, rightType.Slots.RMod);
                break;
            case PyOperatorTypes.Pow:
                Debug.Assert(modulo is not null);
                result = EvalReflectiveOperator(context, left, right, modulo, leftType.Slots.Pow, rightType.Slots.RPow);
                break;
            case PyOperatorTypes.LShift:
                result = EvalReflectiveOperator(context, left, right, leftType.Slots.LShift, rightType.Slots.RLShift);
                break;
            case PyOperatorTypes.RShift:
                result = EvalReflectiveOperator(context, left, right, leftType.Slots.RShift, rightType.Slots.RRShift);
                break;
            case PyOperatorTypes.BitAnd:
                result = EvalReflectiveOperator(context, left, right, leftType.Slots.And, rightType.Slots.RAnd);
                break;
            case PyOperatorTypes.BitXor:
                result = EvalReflectiveOperator(context, left, right, leftType.Slots.Xor, rightType.Slots.RXor);
                break;
            case PyOperatorTypes.BitOr:
                result = EvalReflectiveOperator(context, left, right, leftType.Slots.Or, rightType.Slots.ROr);
                break;
            case PyOperatorTypes.Lt:
                result = EvalReflectiveOperator(context, left, right, leftType.Slots.Lt, rightType.Slots.Gt);
                break;
            case PyOperatorTypes.LtE:
                result = EvalReflectiveOperator(context, left, right, leftType.Slots.Le, rightType.Slots.Ge);
                break;
            case PyOperatorTypes.Gt:
                result = EvalReflectiveOperator(context, left, right, leftType.Slots.Gt, rightType.Slots.Lt);
                break;
            case PyOperatorTypes.GtE:
                result = EvalReflectiveOperator(context, left, right, leftType.Slots.Ge, rightType.Slots.Le);
                break;
            default:
                return PyResult.RaisePySharpException($"Operator '{OperatorToString(op)}' is not supported.");
        }

        if (result.IsNotImplemented)
            return PyResult.TypeError(PySR.Runtime_Operator_UnsupportedBetween, OperatorToString(op), left.PyType.FullName, right.PyType.FullName);

        return result;
    }

    private static PyResult EvalRightFirstReflectiveOperator(PyCallContext context, PyOperatorTypes op, PyObject left, PyObject right, PyObject? modulo)
    {
        PyResult result;
        var leftType = left.PyType;
        var rightType = right.PyType;
        switch (op)
        {
            case PyOperatorTypes.Add:
                result = EvalReflectiveOperator(context, right, left, rightType.Slots.RAdd, leftType.Slots.Add);
                break;
            case PyOperatorTypes.Sub:
                result = EvalReflectiveOperator(context, right, left, rightType.Slots.RSub, leftType.Slots.Sub);
                break;
            case PyOperatorTypes.Mult:
                result = EvalReflectiveOperator(context, right, left, rightType.Slots.RMul, leftType.Slots.Mul);
                break;
            case PyOperatorTypes.MatMult:
                result = EvalReflectiveOperator(context, right, left, rightType.Slots.RMatMul, leftType.Slots.MatMul);
                break;
            case PyOperatorTypes.TrueDiv:
                result = EvalReflectiveOperator(context, right, left, rightType.Slots.RTrueDiv, leftType.Slots.TrueDiv);
                break;
            case PyOperatorTypes.FloorDiv:
                result = EvalReflectiveOperator(context, right, left, rightType.Slots.RFloorDiv, leftType.Slots.FloorDiv);
                break;
            case PyOperatorTypes.Mod:
                result = EvalReflectiveOperator(context, right, left, rightType.Slots.RMod, leftType.Slots.Mod);
                break;
            case PyOperatorTypes.Pow:
                Debug.Assert(modulo is not null);
                result = EvalReflectiveOperator(context, right, left, modulo, rightType.Slots.RPow, leftType.Slots.Pow);
                break;
            case PyOperatorTypes.LShift:
                result = EvalReflectiveOperator(context, right, left, rightType.Slots.RLShift, leftType.Slots.LShift);
                break;
            case PyOperatorTypes.RShift:
                result = EvalReflectiveOperator(context, right, left, rightType.Slots.RRShift, leftType.Slots.RShift);
                break;
            case PyOperatorTypes.BitAnd:
                result = EvalReflectiveOperator(context, right, left, rightType.Slots.RAnd, leftType.Slots.And);
                break;
            case PyOperatorTypes.BitXor:
                result = EvalReflectiveOperator(context, right, left, rightType.Slots.RXor, leftType.Slots.Xor);
                break;
            case PyOperatorTypes.BitOr:
                result = EvalReflectiveOperator(context, right, left, rightType.Slots.ROr, leftType.Slots.Or);
                break;
            case PyOperatorTypes.Lt:
                result = EvalReflectiveOperator(context, right, left, rightType.Slots.Gt, leftType.Slots.Lt);
                break;
            case PyOperatorTypes.LtE:
                result = EvalReflectiveOperator(context, right, left, rightType.Slots.Ge, leftType.Slots.Le);
                break;
            case PyOperatorTypes.Gt:
                result = EvalReflectiveOperator(context, right, left, rightType.Slots.Lt, leftType.Slots.Gt);
                break;
            case PyOperatorTypes.GtE:
                result = EvalReflectiveOperator(context, right, left, rightType.Slots.Le, leftType.Slots.Ge);
                break;
            default:
                return PyResult.RaisePySharpException($"Operator '{OperatorToString(op)}' is not supported.");
        }

        if (result.IsNotImplemented)
            return PyResult.TypeError(PySR.Runtime_Operator_UnsupportedBetween, OperatorToString(op), left.PyType.FullName, right.PyType.FullName);

        return result;
    }

    private static PyResult InPlaceOperator(PyCallContext context, PyOperatorTypes op, PyObject left, PyObject right, PyObject? modulo = null)
    {
        if (left.PyType is PyIntObjectType && right.PyType is PyIntObjectType)
            return PyMath.CalculatePyIntObject(op, (PyIntObject)left, (PyIntObject)right, modulo);

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
        return ReflectiveOperator(context, op, left, right, modulo);
    }
    private static PyResult ReflectiveOperator(PyCallContext context, PyOperatorTypes op, PyObject left, PyObject right, PyObject? modulo = null)
    {
        if (left.PyType is PyIntObjectType && right.PyType is PyIntObjectType)
            return PyMath.CalculatePyIntObject(op, (PyIntObject)left, (PyIntObject)right, modulo);

        if (!context.Comparer.Equals(left.PyType, right.PyType) && right.PyType.IsSubclassOf(left.PyType))
            return EvalRightFirstReflectiveOperator(context, op, left, right, modulo);
        return EvalLeftFirstReflectiveOperator(context, op, left, right, modulo);
    }

    public static PyResult Add(PyCallContext context, PyObject left, PyObject right)
    {
        return ReflectiveOperator(context, PyOperatorTypes.Add, left, right);
    }
    public static PyResult Sub(PyCallContext context, PyObject left, PyObject right)
    {
        return ReflectiveOperator(context, PyOperatorTypes.Sub, left, right);
    }
    public static PyResult Mult(PyCallContext context, PyObject left, PyObject right)
    {
        return ReflectiveOperator(context, PyOperatorTypes.Mult, left, right);
    }
    public static PyResult MatMult(PyCallContext context, PyObject left, PyObject right)
    {
        return ReflectiveOperator(context, PyOperatorTypes.MatMult, left, right);
    }
    public static PyResult TrueDiv(PyCallContext context, PyObject left, PyObject right)
    {
        return ReflectiveOperator(context, PyOperatorTypes.TrueDiv, left, right);
    }
    public static PyResult FloorDiv(PyCallContext context, PyObject left, PyObject right)
    {
        return ReflectiveOperator(context, PyOperatorTypes.FloorDiv, left, right);
    }
    public static PyResult Mod(PyCallContext context, PyObject left, PyObject right)
    {
        return ReflectiveOperator(context, PyOperatorTypes.Mod, left, right);
    }
    public static PyResult Pow(PyCallContext context, PyObject left, PyObject right, PyObject modulo)
    {
        return ReflectiveOperator(context, PyOperatorTypes.Pow, left, right, modulo);
    }
    public static PyResult LShift(PyCallContext context, PyObject left, PyObject right)
    {
        return ReflectiveOperator(context, PyOperatorTypes.LShift, left, right);
    }
    public static PyResult RShift(PyCallContext context, PyObject left, PyObject right)
    {
        return ReflectiveOperator(context, PyOperatorTypes.RShift, left, right);
    }
    public static PyResult BitAnd(PyCallContext context, PyObject left, PyObject right)
    {
        return ReflectiveOperator(context, PyOperatorTypes.BitAnd, left, right);
    }
    public static PyResult BitXor(PyCallContext context, PyObject left, PyObject right)
    {
        return ReflectiveOperator(context, PyOperatorTypes.BitXor, left, right);
    }
    public static PyResult BitOr(PyCallContext context, PyObject left, PyObject right)
    {
        return ReflectiveOperator(context, PyOperatorTypes.BitOr, left, right);
    }
    public static PyResult Lt(PyCallContext context, PyObject left, PyObject right)
    {
        return ReflectiveOperator(context, PyOperatorTypes.Lt, left, right);
    }
    public static PyResult LtE(PyCallContext context, PyObject left, PyObject right)
    {
        return ReflectiveOperator(context, PyOperatorTypes.LtE, left, right);
    }
    public static PyResult Gt(PyCallContext context, PyObject left, PyObject right)
    {
        return ReflectiveOperator(context, PyOperatorTypes.Gt, left, right);
    }
    public static PyResult GtE(PyCallContext context, PyObject left, PyObject right)
    {
        return ReflectiveOperator(context, PyOperatorTypes.GtE, left, right);
    }

    public static PyResult InPlaceAdd(PyCallContext context, PyObject left, PyObject right)
    {
        return InPlaceOperator(context, PyOperatorTypes.Add, left, right);
    }
    public static PyResult InPlaceSub(PyCallContext context, PyObject left, PyObject right)
    {
        return InPlaceOperator(context, PyOperatorTypes.Sub, left, right);
    }
    public static PyResult InPlaceMult(PyCallContext context, PyObject left, PyObject right)
    {
        return InPlaceOperator(context, PyOperatorTypes.Mult, left, right);
    }
    public static PyResult InPlaceMatMult(PyCallContext context, PyObject left, PyObject right)
    {
        return InPlaceOperator(context, PyOperatorTypes.MatMult, left, right);
    }
    public static PyResult InPlaceTrueDiv(PyCallContext context, PyObject left, PyObject right)
    {
        return InPlaceOperator(context, PyOperatorTypes.TrueDiv, left, right);
    }
    public static PyResult InPlaceFloorDiv(PyCallContext context, PyObject left, PyObject right)
    {
        return InPlaceOperator(context, PyOperatorTypes.FloorDiv, left, right);
    }
    public static PyResult InPlaceMod(PyCallContext context, PyObject left, PyObject right)
    {
        return InPlaceOperator(context, PyOperatorTypes.Mod, left, right);
    }
    public static PyResult InPlacePow(PyCallContext context, PyObject left, PyObject right, PyObject modulo)
    {
        return InPlaceOperator(context, PyOperatorTypes.Pow, left, right, modulo);
    }
    public static PyResult InPlaceLShift(PyCallContext context, PyObject left, PyObject right)
    {
        return InPlaceOperator(context, PyOperatorTypes.LShift, left, right);
    }
    public static PyResult InPlaceRShift(PyCallContext context, PyObject left, PyObject right)
    {
        return InPlaceOperator(context, PyOperatorTypes.RShift, left, right);
    }
    public static PyResult InPlaceBitAnd(PyCallContext context, PyObject left, PyObject right)
    {
        return InPlaceOperator(context, PyOperatorTypes.BitAnd, left, right);
    }
    public static PyResult InPlaceBitXor(PyCallContext context, PyObject left, PyObject right)
    {
        return InPlaceOperator(context, PyOperatorTypes.BitXor, left, right);
    }
    public static PyResult InPlaceBitOr(PyCallContext context, PyObject left, PyObject right)
    {
        return InPlaceOperator(context, PyOperatorTypes.BitOr, left, right);
    }

    public static PyResult Eq(PyCallContext context, PyObject left, PyObject right)
    {
        var result = EvalReflectiveOperator(context, left, right, left.PyType.Slots.Eq, right.PyType.Slots.Eq);
        if (!result.IsNotImplemented)
            // error or non-NotImplemented value
            return result;

        return Is(left, right);
    }
    public static PyResult NotEq(PyCallContext context, PyObject left, PyObject right)
    {
        var neResult = EvalReflectiveOperator(context, left, right, left.PyType.Slots.Ne, right.PyType.Slots.Ne);
        if (!neResult.IsNotImplemented)
            // error or non-NotImplemented value
            return neResult;

        var eq = Eq(context, left, right);
        if (eq.IsError)
            return eq;

        Debug.Assert(!eq.IsNotImplemented);

        var result = PySpecialMethods.Bool(context, eq.Value);
        if (result.IsError)
            return result;

        return PyBoolObject.FromBoolean(!result.Value.BoolValue);
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
        if (func is not null)
        {
            var result = func(context, value);
            if (!result.IsNotImplemented)
                return result;
        }

        return PyResult.TypeError(PySR.Runtime_Operator_UnsupportedForUnary, op, value.PyType.FullName);
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
