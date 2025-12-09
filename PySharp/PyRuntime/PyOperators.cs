using PySharp.PyModules.Builtins;
using System.Diagnostics;
using System.Numerics;

namespace PySharp.PyRuntime;

public static class PyOperators
{
    public enum Operator
    {
        Add,
        Sub,
        Mul,
        TrueDiv,
        FloorDiv,
        Mod,
        Pow,
        LShift,
        RShift,
        And,
        Or,
        Xor,
        Lt,
        Le,
        Eq,
        Ne,
        Gt,
        Ge
    }

    private static string OperatorToString(Operator op)
    {
        return op switch
        {
            Operator.Add => "+",
            Operator.Sub => "-",
            Operator.Mul => "*",
            Operator.TrueDiv => "/",
            Operator.FloorDiv => "//",
            Operator.Mod => "%",
            Operator.Pow => "**",
            Operator.LShift => "<<",
            Operator.RShift => ">>",
            Operator.And => "&",
            Operator.Or => "|",
            Operator.Xor => "^",
            Operator.Lt => "<",
            Operator.Le => "<=",
            Operator.Eq => "==",
            Operator.Ne => "!=",
            Operator.Gt => ">",
            Operator.Ge => ">=",
            _ => throw new UnreachableException(),
        };
    }

    private static PyObject? FastOperatorForInt(Operator op, PyIntObject left, PyIntObject right, PyIntObject? modulo)
    {
        switch (op)
        {
            case Operator.Add:
                return PyIntObject.FromInteger(left.Value + right.Value);

            case Operator.Sub:
                return PyIntObject.FromInteger(left.Value - right.Value);

            case Operator.Mul:
                return PyIntObject.FromInteger(left.Value * right.Value);

            case Operator.TrueDiv:
                if (right.Value == 0)
                    return PyVirtualMachine.RaiseZeroDivisionError("division by zero");
                return PyFloatObject.FromDouble((double)left.Value / (double)right.Value);

            case Operator.FloorDiv:
                if (right.Value == 0)
                    return PyVirtualMachine.RaiseZeroDivisionError("division by zero");
                var (q, r) = BigInteger.DivRem(left.Value, right.Value);
                if (r.IsZero || BigInteger.IsPositive(q))
                    return PyIntObject.FromInteger(q);
                return PyIntObject.FromInteger(q - 1);

            case Operator.Mod:
                if (right.Value == 0)
                    return PyVirtualMachine.RaiseZeroDivisionError("integer modulo by zero");
                return PyIntObject.FromInteger(left.Value % right.Value);

            case Operator.Pow:
                Debug.Assert(modulo is not null);
                if (modulo.Value == 0)
                {
                    if (right.Value >= 0)
                        return PyIntObject.FromInteger(BigInteger.Pow(left.Value, (int)right.Value));
                    return PyFloatObject.FromDouble(Math.Pow((double)left.Value, (double)right.Value));
                }
                else
                {
                    if (right.Value >= 0)
                        return PyIntObject.FromInteger(BigInteger.ModPow(left.Value, right.Value, modulo.Value));
                    return PyFloatObject.FromDouble(Math.Pow((double)left.Value, (double)right.Value) % (double)modulo.Value);
                }

            case Operator.LShift:
                return PyIntObject.FromInteger(left.Value << right.Int32Value);

            case Operator.RShift:
                return PyIntObject.FromInteger(left.Value >> right.Int32Value);

            case Operator.And:
                return PyIntObject.FromInteger(left.Value & right.Value);

            case Operator.Or:
                return PyIntObject.FromInteger(left.Value | right.Value);

            case Operator.Xor:
                return PyIntObject.FromInteger(left.Value ^ right.Value);

            case Operator.Lt:
                return PyBoolObject.FromBoolean(left.Value < right.Value);

            case Operator.Le:
                return PyBoolObject.FromBoolean(left.Value <= right.Value);

            case Operator.Eq:
                return PyBoolObject.FromBoolean(left.Value == right.Value);

            case Operator.Ne:
                return PyBoolObject.FromBoolean(left.Value != right.Value);

            case Operator.Gt:
                return PyBoolObject.FromBoolean(left.Value > right.Value);

            case Operator.Ge:
                return PyBoolObject.FromBoolean(left.Value >= right.Value);

            default:
                throw new UnreachableException();
        }
    }

    private static PyObject? LeftReflectiveOperator(Operator op, PyObject left, PyObject right, PyObject? modulo)
    {
        PyObject? ret;
        switch (op)
        {
            case Operator.Add:
                ret = left.Add(right);
                if (ret is not PyNotImplementedObject)
                    return ret;
                ret = right.RAdd(left);
                break;

            case Operator.Sub:
                ret = left.Sub(right);
                if (ret is not PyNotImplementedObject)
                    return ret;
                ret = right.RSub(left);
                break;

            case Operator.Mul:
                ret = left.Mul(right);
                if (ret is not PyNotImplementedObject)
                    return ret;
                ret = right.RMul(left);
                break;

            case Operator.TrueDiv:
                ret = left.TrueDiv(right);
                if (ret is not PyNotImplementedObject)
                    return ret;
                ret = right.RTrueDiv(left);
                break;

            case Operator.FloorDiv:
                ret = left.FloorDiv(right);
                if (ret is not PyNotImplementedObject)
                    return ret;
                ret = right.RFloorDiv(left);
                break;

            case Operator.Mod:
                ret = left.Mod(right);
                if (ret is not PyNotImplementedObject)
                    return ret;
                ret = right.RMod(left);
                break;

            case Operator.Pow:
                Debug.Assert(modulo is not null);
                ret = left.Pow(right, modulo);
                if (ret is not PyNotImplementedObject)
                    return ret;
                ret = right.RPow(left, modulo);
                break;

            case Operator.LShift:
                ret = left.LShift(right);
                if (ret is not PyNotImplementedObject)
                    return ret;
                ret = right.RLShift(left);
                break;

            case Operator.RShift:
                ret = left.RShift(right);
                if (ret is not PyNotImplementedObject)
                    return ret;
                ret = right.RRShift(left);
                break;

            case Operator.And:
                ret = left.And(right);
                if (ret is not PyNotImplementedObject)
                    return ret;
                ret = right.RAnd(left);
                break;

            case Operator.Xor:
                ret = left.Xor(right);
                if (ret is not PyNotImplementedObject)
                    return ret;
                ret = right.RXor(left);
                break;

            case Operator.Or:
                ret = left.Or(right);
                if (ret is not PyNotImplementedObject)
                    return ret;
                ret = right.ROr(left);
                break;

            case Operator.Lt:
                ret = left.Lt(right);
                if (ret is not PyNotImplementedObject)
                    return ret;
                ret = right.Gt(left);
                break;

            case Operator.Le:
                ret = left.Le(right);
                if (ret is not PyNotImplementedObject)
                    return ret;
                ret = right.Ge(left);
                break;

            case Operator.Gt:
                ret = left.Gt(right);
                if (ret is not PyNotImplementedObject)
                    return ret;
                ret = right.Lt(left);
                break;

            case Operator.Ge:
                ret = left.Ge(right);
                if (ret is not PyNotImplementedObject)
                    return ret;
                ret = right.Le(left);
                break;

            default:
                return PyVirtualMachine.RaiseTypeError($"Operator '{OperatorToString(op)}' is not supported.");
        }

        if (ret is PyNotImplementedObject)
            return PyVirtualMachine.RaiseTypeError($"'{OperatorToString(op)}' not supported between instances of '{left.PyType.Name}' and '{right.PyType.Name}'");

        return ret;
    }
    private static PyObject? RightReflectiveOperator(Operator op, PyObject left, PyObject right, PyObject? modulo)
    {
        PyObject? ret;
        switch (op)
        {
            case Operator.Add:
                ret = right.RAdd(left);
                if (ret is not PyNotImplementedObject)
                    return ret;
                ret = left.Add(right);
                break;
            case Operator.Sub:
                ret = right.RSub(left);
                if (ret is not PyNotImplementedObject)
                    return ret;
                ret = left.Sub(right);
                break;
            case Operator.Mul:
                ret = right.RMul(left);
                if (ret is not PyNotImplementedObject)
                    return ret;
                ret = left.Mul(right);
                break;
            case Operator.TrueDiv:
                ret = right.RTrueDiv(left);
                if (ret is not PyNotImplementedObject)
                    return ret;
                ret = left.TrueDiv(right);
                break;
            case Operator.FloorDiv:
                ret = right.RFloorDiv(left);
                if (ret is not PyNotImplementedObject)
                    return ret;
                ret = left.FloorDiv(right);
                break;
            case Operator.Mod:
                ret = right.RMod(left);
                if (ret is not PyNotImplementedObject)
                    return ret;
                ret = left.Mod(right);
                break;
            case Operator.Pow:
                Debug.Assert(modulo is not null);
                ret = right.RPow(left, modulo);
                if (ret is not PyNotImplementedObject)
                    return ret;
                ret = left.Pow(right, modulo);
                break;
            case Operator.LShift:
                ret = right.RLShift(left);
                if (ret is not PyNotImplementedObject)
                    return ret;
                ret = left.LShift(right);
                break;
            case Operator.RShift:
                ret = right.RRShift(left);
                if (ret is not PyNotImplementedObject)
                    return ret;
                ret = left.RShift(right);
                break;
            case Operator.And:
                ret = right.RAnd(left);
                if (ret is not PyNotImplementedObject)
                    return ret;
                ret = left.And(right);
                break;
            case Operator.Xor:
                ret = right.RXor(left);
                if (ret is not PyNotImplementedObject)
                    return ret;
                ret = left.Xor(right);
                break;
            case Operator.Or:
                ret = right.ROr(left);
                if (ret is not PyNotImplementedObject)
                    return ret;
                ret = left.Or(right);
                break;
            case Operator.Lt:
                ret = right.Gt(left);
                if (ret is not PyNotImplementedObject)
                    return ret;
                ret = left.Lt(right);
                break;
            case Operator.Le:
                ret = right.Ge(left);
                if (ret is not PyNotImplementedObject)
                    return ret;
                ret = left.Le(right);
                break;
            case Operator.Gt:
                ret = right.Lt(left);
                if (ret is not PyNotImplementedObject)
                    return ret;
                ret = left.Gt(right);
                break;
            case Operator.Ge:
                ret = right.Le(left);
                if (ret is not PyNotImplementedObject)
                    return ret;
                ret = left.Ge(right);
                break;
            default:
                return PyVirtualMachine.RaiseTypeError($"Operator '{OperatorToString(op)}' is not supported.");
        }

        if (ret is PyNotImplementedObject)
            return PyVirtualMachine.RaiseTypeError($"'{OperatorToString(op)}' not supported between instances of '{right.PyType.Name}' and '{left.PyType.Name}'");

        return ret;
    }
    private static PyObject? ReflectiveOperator(Operator op, PyObject left, PyObject right, PyObject? modulo = null)
    {
        if (left is PyIntObject leftInt && right is PyIntObject rightInt && modulo is null or PyIntObject)
            return FastOperatorForInt(op, leftInt, rightInt, modulo as PyIntObject);

        if (left.PyType.IsSubclass(right.PyType) && left.PyType != right.PyType)
            return RightReflectiveOperator(op, left, right, modulo);
        return LeftReflectiveOperator(op, left, right, modulo);
    }

    public static PyObject? Add(PyObject left, PyObject right)
    {
        return ReflectiveOperator(Operator.Add, left, right);
    }
    public static PyObject? Sub(PyObject left, PyObject right)
    {
        return ReflectiveOperator(Operator.Sub, left, right);
    }
    public static PyObject? Mul(PyObject left, PyObject right)
    {
        return ReflectiveOperator(Operator.Mul, left, right);
    }
    public static PyObject? TrueDiv(PyObject left, PyObject right)
    {
        return ReflectiveOperator(Operator.TrueDiv, left, right);
    }
    public static PyObject? FloorDiv(PyObject left, PyObject right)
    {
        return ReflectiveOperator(Operator.FloorDiv, left, right);
    }
    public static PyObject? Mod(PyObject left, PyObject right)
    {
        return ReflectiveOperator(Operator.Mod, left, right);
    }
    public static PyObject? Pow(PyObject left, PyObject right, PyObject modulo)
    {
        return ReflectiveOperator(Operator.Pow, left, right, modulo);
    }
    public static PyObject? LShift(PyObject left, PyObject right)
    {
        return ReflectiveOperator(Operator.LShift, left, right);
    }
    public static PyObject? RShift(PyObject left, PyObject right)
    {
        return ReflectiveOperator(Operator.RShift, left, right);
    }
    public static PyObject? And(PyObject left, PyObject right)
    {
        return ReflectiveOperator(Operator.And, left, right);
    }
    public static PyObject? Xor(PyObject left, PyObject right)
    {
        return ReflectiveOperator(Operator.Xor, left, right);
    }
    public static PyObject? Or(PyObject left, PyObject right)
    {
        return ReflectiveOperator(Operator.Or, left, right);
    }
    public static PyObject? Lt(PyObject left, PyObject right)
    {
        return ReflectiveOperator(Operator.Lt, left, right);
    }
    public static PyObject? Le(PyObject left, PyObject right)
    {
        return ReflectiveOperator(Operator.Le, left, right);
    }
    public static PyObject? Eq(PyObject left, PyObject right)
    {
        var ret = left.Eq(right);
        if (ret is not PyNotImplementedObject)
            return ret;

        ret = right.Eq(left);
        if (ret is PyNotImplementedObject)
            return Is(left, right);

        return ret;
    }
    public static PyObject? Ne(PyObject left, PyObject right)
    {
        var ret = left.Ne(right);
        if (ret is not PyNotImplementedObject)
            return ret;

        ret = right.Ne(left);
        if (ret is not PyNotImplementedObject)
            return ret;

        var eq = Eq(left, right);
        if (eq is null)
            return null;

        Debug.Assert(eq is not PyNotImplementedObject);

        var boolRet = eq.Bool();
        if (boolRet is null)
            return null;

        if (!PySpecialMethods.TryGetBool(boolRet, out var b))
            return null;

        return PyBoolObject.FromBoolean(!b.BoolValue);
    }
    public static PyObject? Gt(PyObject left, PyObject right)
    {
        return ReflectiveOperator(Operator.Gt, left, right);
    }
    public static PyObject? Ge(PyObject left, PyObject right)
    {
        return ReflectiveOperator(Operator.Ge, left, right);
    }

    public static PyBoolObject Is(PyObject left, PyObject right)
    {
        return PyBoolObject.FromBoolean(ReferenceEquals(left, right));
    }
    public static PyBoolObject IsNot(PyObject left, PyObject right)
    {
        return PyBoolObject.FromBoolean(!ReferenceEquals(left, right));
    }

    public static PyObject? GetAttr(PyObject target, string name)
    {
        var attr = target.GetAttribute(name);
        if (attr is not null || !PyVirtualMachine.IsExceptionOfTypeRaised(PyStandardExceptionTypes.AttributeError))
            return attr;

        return target.GetAttr(name);
    }
    public static PyObject? SetAttr(PyObject target, string name, PyObject value)
    {
        return target.SetAttr(name, value);
    }
    public static PyObject? DelAttr(PyObject target, string name)
    {
        return target.DelAttr(name);
    }
}
