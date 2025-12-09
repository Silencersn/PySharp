using PySharp.PyModules.Builtins;
using System.Diagnostics;
using System.Numerics;
using System.Reflection;

namespace PySharp.PyRuntime;

public static class PyOperators
{
    private static PyObject? FastOperatorForInt(string op, PyIntObject left, PyIntObject right, PyIntObject? modulo)
    {
        switch (op)
        {
            case "+":
                return PyIntObject.FromInteger(left.Value + right.Value);

            case "-":
                return PyIntObject.FromInteger(left.Value - right.Value);

            case "*":
                return PyIntObject.FromInteger(left.Value * right.Value);

            case "/":
                if (right.Value == 0)
                    return PyVirtualMachine.RaiseZeroDivisionError("division by zero");
                return PyFloatObject.FromDouble((double)left.Value / (double)right.Value);

            case "//":
                if (right.Value == 0)
                    return PyVirtualMachine.RaiseZeroDivisionError("division by zero");
                var (q, r) = BigInteger.DivRem(left.Value, right.Value);
                if (r.IsZero || BigInteger.IsPositive(q))
                    return PyIntObject.FromInteger(q);
                return PyIntObject.FromInteger(q - 1);

            case "%":
                if (right.Value == 0)
                    return PyVirtualMachine.RaiseZeroDivisionError("integer modulo by zero");
                return PyIntObject.FromInteger(left.Value % right.Value);

            case "**":
                Debug.Assert(modulo is not null);
                if (modulo.Value == 0)
                {
                    if (right.Value >= 0)
                        return PyIntObject.FromInteger(BigInteger.Pow(left.Value, (int)right.Value));
                    return PyFloatObject.FromDouble(System.Math.Pow((double)left.Value, (double)right.Value));
                }
                else
                {
                    if (right.Value >= 0)
                        return PyIntObject.FromInteger(BigInteger.ModPow(left.Value, right.Value, modulo.Value));
                    return PyFloatObject.FromDouble(System.Math.Pow((double)left.Value, (double)right.Value) % (double)modulo.Value);
                }

            case "<<":
                return PyIntObject.FromInteger(left.Value << right.Int32Value);

            case ">>":
                return PyIntObject.FromInteger(left.Value >> right.Int32Value);

            case "&":
                return PyIntObject.FromInteger(left.Value & right.Value);

            case "|":
                return PyIntObject.FromInteger(left.Value | right.Value);

            case "^":
                return PyIntObject.FromInteger(left.Value ^ right.Value);

            case "<":
                return PyBoolObject.FromBoolean(left.Value < right.Value);

            case "<=":
                return PyBoolObject.FromBoolean(left.Value <= right.Value);

            case "==":
                return PyBoolObject.FromBoolean(left.Value == right.Value);

            case "!=" :
                return PyBoolObject.FromBoolean(left.Value != right.Value);

            case ">":
                return PyBoolObject.FromBoolean(left.Value > right.Value);

            case ">=":
                return PyBoolObject.FromBoolean(left.Value >= right.Value);

            default:
                throw new UnreachableException();
        }
    }

    private static PyObject? LeftReflectiveOperator(string op, PyObject left, PyObject right, PyObject? modulo)
    {
        PyObject? ret;
        switch (op)
        {
            case "+":
                ret = left.Add(right);
                if (ret is not PyNotImplementedObject)
                    return ret;
                ret = right.RAdd(left);
                break;

            case "-":
                ret = left.Sub(right);
                if (ret is not PyNotImplementedObject)
                    return ret;
                ret = right.RSub(left);
                break;

            case "*":
                ret = left.Mul(right);
                if (ret is not PyNotImplementedObject)
                    return ret;
                ret = right.RMul(left);
                break;

            case "/":
                ret = left.TrueDiv(right);
                if (ret is not PyNotImplementedObject)
                    return ret;
                ret = right.RTrueDiv(left);
                break;

            case "//":
                ret = left.FloorDiv(right);
                if (ret is not PyNotImplementedObject)
                    return ret;
                ret = right.RFloorDiv(left);
                break;

            case "%":
                ret = left.Mod(right);
                if (ret is not PyNotImplementedObject)
                    return ret;
                ret = right.RMod(left);
                break;

            case "**":
                Debug.Assert(modulo is not null);
                ret = left.Pow(right, modulo);
                if (ret is not PyNotImplementedObject)
                    return ret;
                ret = right.RPow(left, modulo);
                break;

            case "<<":
                ret = left.LShift(right);
                if (ret is not PyNotImplementedObject)
                    return ret;
                ret = right.RLShift(left);
                break;

            case ">>":
                ret = left.RShift(right);
                if (ret is not PyNotImplementedObject)
                    return ret;
                ret = right.RRShift(left);
                break;

            case "&":
                ret = left.And(right);
                if (ret is not PyNotImplementedObject)
                    return ret;
                ret = right.RAnd(left);
                break;

            case "^":
                ret = left.Xor(right);
                if (ret is not PyNotImplementedObject)
                    return ret;
                ret = right.RXor(left);
                break;

            case "|":
                ret = left.Or(right);
                if (ret is not PyNotImplementedObject)
                    return ret;
                ret = right.ROr(left);
                break;

            case "<":
                ret = left.Lt(right);
                if (ret is not PyNotImplementedObject)
                    return ret;
                ret = right.Gt(left);
                break;

            case "<=":
                ret = left.Le(right);
                if (ret is not PyNotImplementedObject)
                    return ret;
                ret = right.Ge(left);
                break;

            case ">":
                ret = left.Gt(right);
                if (ret is not PyNotImplementedObject)
                    return ret;
                ret = right.Lt(left);
                break;

            case ">=":
                ret = left.Ge(right);
                if (ret is not PyNotImplementedObject)
                    return ret;
                ret = right.Le(left);
                break;

            default:
                return PyVirtualMachine.RaiseTypeError($"Operator '{op}' is not supported.");
        }

        if (ret is PyNotImplementedObject)
            return PyVirtualMachine.RaiseTypeError($"'{op}' not supported between instances of '{left.PyType.Name}' and '{right.PyType.Name}'");

        return ret;
    }
    private static PyObject? RightReflectiveOperator(string op, PyObject left, PyObject right, PyObject? modulo)
    {
        PyObject? ret;
        switch (op)
        {
            case "+":
                ret = right.RAdd(left);
                if (ret is not PyNotImplementedObject)
                    return ret;
                ret = left.Add(right);
                break;

            case "-":
                ret = right.RSub(left);
                if (ret is not PyNotImplementedObject)
                    return ret;
                ret = left.Sub(right);
                break;

            case "*":
                ret = right.RMul(left);
                if (ret is not PyNotImplementedObject)
                    return ret;
                ret = left.Mul(right);
                break;

            case "/":
                ret = right.RTrueDiv(left);
                if (ret is not PyNotImplementedObject)
                    return ret;
                ret = left.TrueDiv(right);
                break;

            case "//":
                ret = right.RFloorDiv(left);
                if (ret is not PyNotImplementedObject)
                    return ret;
                ret = left.FloorDiv(right);
                break;

            case "%":
                ret = right.RMod(left);
                if (ret is not PyNotImplementedObject)
                    return ret;
                ret = left.Mod(right);
                break;

            case "**":
                Debug.Assert(modulo is not null);
                ret = right.RPow(left, modulo);
                if (ret is not PyNotImplementedObject)
                    return ret;
                ret = left.Pow(right, modulo);
                break;

            case "<<":
                ret = right.RLShift(left);
                if (ret is not PyNotImplementedObject)
                    return ret;
                ret = left.LShift(right);
                break;

            case ">>":
                ret = right.RRShift(left);
                if (ret is not PyNotImplementedObject)
                    return ret;
                ret = left.RShift(right);
                break;

            case "&":
                ret = right.RAnd(left);
                if (ret is not PyNotImplementedObject)
                    return ret;
                ret = left.And(right);
                break;

            case "^":
                ret = right.RXor(left);
                if (ret is not PyNotImplementedObject)
                    return ret;
                ret = left.Xor(right);
                break;

            case "|":
                ret = right.ROr(left);
                if (ret is not PyNotImplementedObject)
                    return ret;
                ret = left.Or(right);
                break;

            case "<":
                ret = right.Gt(left);
                if (ret is not PyNotImplementedObject)
                    return ret;
                ret = left.Lt(right);
                break;

            case "<=":
                ret = right.Ge(left);
                if (ret is not PyNotImplementedObject)
                    return ret;
                ret = left.Le(right);
                break;

            case ">":
                ret = right.Lt(left);
                if (ret is not PyNotImplementedObject)
                    return ret;
                ret = left.Gt(right);
                break;

            case ">=":
                ret = right.Le(left);
                if (ret is not PyNotImplementedObject)
                    return ret;
                ret = left.Ge(right);
                break;

            default:
                return PyVirtualMachine.RaiseTypeError($"Operator '{op}' is not supported.");
        }

        if (ret is PyNotImplementedObject)
            return PyVirtualMachine.RaiseTypeError($"'{op}' not supported between instances of '{right.PyType.Name}' and '{left.PyType.Name}'");

        return ret;
    }
    private static PyObject? ReflectiveOperator(string op, PyObject left, PyObject right, PyObject? modulo = null)
    {
        if (left is PyIntObject leftInt && right is PyIntObject rightInt && modulo is null or PyIntObject)
            return FastOperatorForInt(op, leftInt, rightInt, modulo as PyIntObject);

        if (left.PyType.IsSubclass(right.PyType) && left.PyType != right.PyType)
            return RightReflectiveOperator(op, left, right, modulo);
        return LeftReflectiveOperator(op, left, right, modulo);
    }

    public static PyObject? Add(PyObject left, PyObject right)
    {
        return ReflectiveOperator("+", left, right);
    }
    public static PyObject? Sub(PyObject left, PyObject right)
    {
        return ReflectiveOperator("-", left, right);
    }
    public static PyObject? Mul(PyObject left, PyObject right)
    {
        return ReflectiveOperator("*", left, right);
    }
    public static PyObject? TrueDiv(PyObject left, PyObject right)
    {
        return ReflectiveOperator("/", left, right);
    }
    public static PyObject? FloorDiv(PyObject left, PyObject right)
    {
        return ReflectiveOperator("//", left, right);
    }
    public static PyObject? Mod(PyObject left, PyObject right)
    {
        return ReflectiveOperator("%", left, right);
    }
    public static PyObject? Pow(PyObject left, PyObject right, PyObject modulo)
    {
        return ReflectiveOperator("**", left, right, modulo);
    }
    public static PyObject? LShift(PyObject left, PyObject right)
    {
        return ReflectiveOperator("<<", left, right);
    }
    public static PyObject? RShift(PyObject left, PyObject right)
    {
        return ReflectiveOperator(">>", left, right);
    }
    public static PyObject? And(PyObject left, PyObject right)
    {
        return ReflectiveOperator("&", left, right);
    }
    public static PyObject? Xor(PyObject left, PyObject right)
    {
        return ReflectiveOperator("^", left, right);
    }
    public static PyObject? Or(PyObject left, PyObject right)
    {
        return ReflectiveOperator("|", left, right);
    }
    public static PyObject? Lt(PyObject left, PyObject right)
    {
        return ReflectiveOperator("<", left, right);
    }
    public static PyObject? Le(PyObject left, PyObject right)
    {
        return ReflectiveOperator("<=", left, right);
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
        return ReflectiveOperator(">", left, right);
    }
    public static PyObject? Ge(PyObject left, PyObject right)
    {
        return ReflectiveOperator(">=", left, right);
    }

    public static PyBoolObject Is(PyObject left, PyObject right)
    {
        return PyBoolObject.FromBoolean(left.PyId == right.PyId);
    }
    public static PyBoolObject IsNot(PyObject left, PyObject right)
    {
        return PyBoolObject.FromBoolean(left.PyId != right.PyId);
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
