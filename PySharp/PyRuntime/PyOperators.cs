using PySharp.PyModules.Builtins;
using System.Diagnostics;

namespace PySharp.PyRuntime;

public enum PyOperatorTypes
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

public static class PyOperators
{

    private static string OperatorToString(PyOperatorTypes op)
    {
        return op switch
        {
            PyOperatorTypes.Add => "+",
            PyOperatorTypes.Sub => "-",
            PyOperatorTypes.Mul => "*",
            PyOperatorTypes.TrueDiv => "/",
            PyOperatorTypes.FloorDiv => "//",
            PyOperatorTypes.Mod => "%",
            PyOperatorTypes.Pow => "**",
            PyOperatorTypes.LShift => "<<",
            PyOperatorTypes.RShift => ">>",
            PyOperatorTypes.And => "&",
            PyOperatorTypes.Or => "|",
            PyOperatorTypes.Xor => "^",
            PyOperatorTypes.Lt => "<",
            PyOperatorTypes.Le => "<=",
            PyOperatorTypes.Eq => "==",
            PyOperatorTypes.Ne => "!=",
            PyOperatorTypes.Gt => ">",
            PyOperatorTypes.Ge => ">=",
            _ => throw new UnreachableException(),
        };
    }


    private static PyObject? LeftReflectiveOperator(PyOperatorTypes op, PyObject left, PyObject right, PyObject? modulo)
    {
        PyObject? ret;
        switch (op)
        {
            case PyOperatorTypes.Add:
                ret = left.Add(right);
                if (ret is not PyNotImplementedObject)
                    return ret;
                ret = right.RAdd(left);
                break;

            case PyOperatorTypes.Sub:
                ret = left.Sub(right);
                if (ret is not PyNotImplementedObject)
                    return ret;
                ret = right.RSub(left);
                break;

            case PyOperatorTypes.Mul:
                ret = left.Mul(right);
                if (ret is not PyNotImplementedObject)
                    return ret;
                ret = right.RMul(left);
                break;

            case PyOperatorTypes.TrueDiv:
                ret = left.TrueDiv(right);
                if (ret is not PyNotImplementedObject)
                    return ret;
                ret = right.RTrueDiv(left);
                break;

            case PyOperatorTypes.FloorDiv:
                ret = left.FloorDiv(right);
                if (ret is not PyNotImplementedObject)
                    return ret;
                ret = right.RFloorDiv(left);
                break;

            case PyOperatorTypes.Mod:
                ret = left.Mod(right);
                if (ret is not PyNotImplementedObject)
                    return ret;
                ret = right.RMod(left);
                break;

            case PyOperatorTypes.Pow:
                Debug.Assert(modulo is not null);
                ret = left.Pow(right, modulo);
                if (ret is not PyNotImplementedObject)
                    return ret;
                ret = right.RPow(left, modulo);
                break;

            case PyOperatorTypes.LShift:
                ret = left.LShift(right);
                if (ret is not PyNotImplementedObject)
                    return ret;
                ret = right.RLShift(left);
                break;

            case PyOperatorTypes.RShift:
                ret = left.RShift(right);
                if (ret is not PyNotImplementedObject)
                    return ret;
                ret = right.RRShift(left);
                break;

            case PyOperatorTypes.And:
                ret = left.And(right);
                if (ret is not PyNotImplementedObject)
                    return ret;
                ret = right.RAnd(left);
                break;

            case PyOperatorTypes.Xor:
                ret = left.Xor(right);
                if (ret is not PyNotImplementedObject)
                    return ret;
                ret = right.RXor(left);
                break;

            case PyOperatorTypes.Or:
                ret = left.Or(right);
                if (ret is not PyNotImplementedObject)
                    return ret;
                ret = right.ROr(left);
                break;

            case PyOperatorTypes.Lt:
                ret = left.Lt(right);
                if (ret is not PyNotImplementedObject)
                    return ret;
                ret = right.Gt(left);
                break;

            case PyOperatorTypes.Le:
                ret = left.Le(right);
                if (ret is not PyNotImplementedObject)
                    return ret;
                ret = right.Ge(left);
                break;

            case PyOperatorTypes.Gt:
                ret = left.Gt(right);
                if (ret is not PyNotImplementedObject)
                    return ret;
                ret = right.Lt(left);
                break;

            case PyOperatorTypes.Ge:
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
    private static PyObject? RightReflectiveOperator(PyOperatorTypes op, PyObject left, PyObject right, PyObject? modulo)
    {
        PyObject? ret;
        switch (op)
        {
            case PyOperatorTypes.Add:
                ret = right.RAdd(left);
                if (ret is not PyNotImplementedObject)
                    return ret;
                ret = left.Add(right);
                break;
            case PyOperatorTypes.Sub:
                ret = right.RSub(left);
                if (ret is not PyNotImplementedObject)
                    return ret;
                ret = left.Sub(right);
                break;
            case PyOperatorTypes.Mul:
                ret = right.RMul(left);
                if (ret is not PyNotImplementedObject)
                    return ret;
                ret = left.Mul(right);
                break;
            case PyOperatorTypes.TrueDiv:
                ret = right.RTrueDiv(left);
                if (ret is not PyNotImplementedObject)
                    return ret;
                ret = left.TrueDiv(right);
                break;
            case PyOperatorTypes.FloorDiv:
                ret = right.RFloorDiv(left);
                if (ret is not PyNotImplementedObject)
                    return ret;
                ret = left.FloorDiv(right);
                break;
            case PyOperatorTypes.Mod:
                ret = right.RMod(left);
                if (ret is not PyNotImplementedObject)
                    return ret;
                ret = left.Mod(right);
                break;
            case PyOperatorTypes.Pow:
                Debug.Assert(modulo is not null);
                ret = right.RPow(left, modulo);
                if (ret is not PyNotImplementedObject)
                    return ret;
                ret = left.Pow(right, modulo);
                break;
            case PyOperatorTypes.LShift:
                ret = right.RLShift(left);
                if (ret is not PyNotImplementedObject)
                    return ret;
                ret = left.LShift(right);
                break;
            case PyOperatorTypes.RShift:
                ret = right.RRShift(left);
                if (ret is not PyNotImplementedObject)
                    return ret;
                ret = left.RShift(right);
                break;
            case PyOperatorTypes.And:
                ret = right.RAnd(left);
                if (ret is not PyNotImplementedObject)
                    return ret;
                ret = left.And(right);
                break;
            case PyOperatorTypes.Xor:
                ret = right.RXor(left);
                if (ret is not PyNotImplementedObject)
                    return ret;
                ret = left.Xor(right);
                break;
            case PyOperatorTypes.Or:
                ret = right.ROr(left);
                if (ret is not PyNotImplementedObject)
                    return ret;
                ret = left.Or(right);
                break;
            case PyOperatorTypes.Lt:
                ret = right.Gt(left);
                if (ret is not PyNotImplementedObject)
                    return ret;
                ret = left.Lt(right);
                break;
            case PyOperatorTypes.Le:
                ret = right.Ge(left);
                if (ret is not PyNotImplementedObject)
                    return ret;
                ret = left.Le(right);
                break;
            case PyOperatorTypes.Gt:
                ret = right.Lt(left);
                if (ret is not PyNotImplementedObject)
                    return ret;
                ret = left.Gt(right);
                break;
            case PyOperatorTypes.Ge:
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
    private static PyObject? ReflectiveOperator(PyOperatorTypes op, PyObject left, PyObject right, PyObject? modulo = null)
    {
        if (left is PyIntObject leftInt && right is PyIntObject rightInt)
            return PyMath.CalculatePyIntObject(op, leftInt, rightInt, modulo);

        if (left.PyType != right.PyType && right.PyType.IsSubclassOf(left.PyType))
            return RightReflectiveOperator(op, left, right, modulo);
        return LeftReflectiveOperator(op, left, right, modulo);
    }

    public static PyObject? Add(PyObject left, PyObject right)
    {
        return ReflectiveOperator(PyOperatorTypes.Add, left, right);
    }
    public static PyObject? Sub(PyObject left, PyObject right)
    {
        return ReflectiveOperator(PyOperatorTypes.Sub, left, right);
    }
    public static PyObject? Mul(PyObject left, PyObject right)
    {
        return ReflectiveOperator(PyOperatorTypes.Mul, left, right);
    }
    public static PyObject? TrueDiv(PyObject left, PyObject right)
    {
        return ReflectiveOperator(PyOperatorTypes.TrueDiv, left, right);
    }
    public static PyObject? FloorDiv(PyObject left, PyObject right)
    {
        return ReflectiveOperator(PyOperatorTypes.FloorDiv, left, right);
    }
    public static PyObject? Mod(PyObject left, PyObject right)
    {
        return ReflectiveOperator(PyOperatorTypes.Mod, left, right);
    }
    public static PyObject? Pow(PyObject left, PyObject right, PyObject modulo)
    {
        return ReflectiveOperator(PyOperatorTypes.Pow, left, right, modulo);
    }
    public static PyObject? LShift(PyObject left, PyObject right)
    {
        return ReflectiveOperator(PyOperatorTypes.LShift, left, right);
    }
    public static PyObject? RShift(PyObject left, PyObject right)
    {
        return ReflectiveOperator(PyOperatorTypes.RShift, left, right);
    }
    public static PyObject? And(PyObject left, PyObject right)
    {
        return ReflectiveOperator(PyOperatorTypes.And, left, right);
    }
    public static PyObject? Xor(PyObject left, PyObject right)
    {
        return ReflectiveOperator(PyOperatorTypes.Xor, left, right);
    }
    public static PyObject? Or(PyObject left, PyObject right)
    {
        return ReflectiveOperator(PyOperatorTypes.Or, left, right);
    }
    public static PyObject? Lt(PyObject left, PyObject right)
    {
        return ReflectiveOperator(PyOperatorTypes.Lt, left, right);
    }
    public static PyObject? Le(PyObject left, PyObject right)
    {
        return ReflectiveOperator(PyOperatorTypes.Le, left, right);
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
        return ReflectiveOperator(PyOperatorTypes.Gt, left, right);
    }
    public static PyObject? Ge(PyObject left, PyObject right)
    {
        return ReflectiveOperator(PyOperatorTypes.Ge, left, right);
    }

    private static bool AreSameObjectAtPythonLevel(PyObject left, PyObject right)
    {
        if (ReferenceEquals(left, right))
            return true;

        if (left._pyId is not null && left._pyId == right._pyId)
            // because of backing objects of CustomObject
            // different PyObject at c# level may be the same PyObject at python level
            return true;

        return false;
    }

    public static PyBoolObject Is(PyObject left, PyObject right)
    {
        return PyBoolObject.FromBoolean(AreSameObjectAtPythonLevel(left, right));
    }
    public static PyBoolObject IsNot(PyObject left, PyObject right)
    {
        return PyBoolObject.FromBoolean(!AreSameObjectAtPythonLevel(left, right));
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
