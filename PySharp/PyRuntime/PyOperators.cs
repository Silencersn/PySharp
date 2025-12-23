using PySharp.PyModules.Builtins;
using PySharp.PyRuntime.Calls;
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


    private static PyResult LeftFirstReflectiveOperator(PyCallContext context, PyOperatorTypes op, PyObject left, PyObject right, PyObject? modulo)
    {
        PyResult result;
        var leftType = left.PyType;
        var rightType = right.PyType;
        switch (op)
        {
            case PyOperatorTypes.Add:
                result = leftType.Add(context, left, right);
                if (!result.IsNotImplemented)
                    return result; // result or error
                result = rightType.RAdd(context, right, left);
                break;

            case PyOperatorTypes.Sub:
                result = leftType.Sub(context, left, right);
                if (!result.IsNotImplemented)
                    return result;
                result = rightType.RSub(context, right, left);
                break;

            case PyOperatorTypes.Mul:
                result = leftType.Mul(context, left, right);
                if (!result.IsNotImplemented)
                    return result;
                result = rightType.RMul(context, right, left);
                break;

            case PyOperatorTypes.TrueDiv:
                result = leftType.TrueDiv(context, left, right);
                if (!result.IsNotImplemented)
                    return result;
                result = rightType.RTrueDiv(context, right, left);
                break;

            case PyOperatorTypes.FloorDiv:
                result = leftType.FloorDiv(context, left, right);
                if (!result.IsNotImplemented)
                    return result;
                result = rightType.RFloorDiv(context, right, left);
                break;

            case PyOperatorTypes.Mod:
                result = leftType.Mod(context, left, right);
                if (!result.IsNotImplemented)
                    return result;
                result = rightType.RMod(context, right, left);
                break;

            case PyOperatorTypes.Pow:
                Debug.Assert(modulo is not null);
                result = leftType.Pow(context, left, right, modulo);
                if (!result.IsNotImplemented)
                    return result;
                result = rightType.RPow(context, right, left, modulo);
                break;

            case PyOperatorTypes.LShift:
                result = leftType.LShift(context, left, right);
                if (!result.IsNotImplemented)
                    return result;
                result = rightType.RLShift(context, right, left);
                break;

            case PyOperatorTypes.RShift:
                result = leftType.RShift(context, left, right);
                if (!result.IsNotImplemented)
                    return result;
                result = rightType.RRShift(context, right, left);
                break;

            case PyOperatorTypes.And:
                result = leftType.And(context, left, right);
                if (!result.IsNotImplemented)
                    return result;
                result = rightType.RAnd(context, right, left);
                break;

            case PyOperatorTypes.Xor:
                result = leftType.Xor(context, left, right);
                if (!result.IsNotImplemented)
                    return result;
                result = rightType.RXor(context, right, left);
                break;

            case PyOperatorTypes.Or:
                result = leftType.Or(context, left, right);
                if (!result.IsNotImplemented)
                    return result;
                result = rightType.ROr(context, right, left);
                break;

            case PyOperatorTypes.Lt:
                result = leftType.Lt(context, left, right);
                if (!result.IsNotImplemented)
                    return result;
                result = rightType.Gt(context, right, left);
                break;

            case PyOperatorTypes.Le:
                result = leftType.Le(context, left, right);
                if (!result.IsNotImplemented)
                    return result;
                result = rightType.Ge(context, right, left);
                break;

            case PyOperatorTypes.Gt:
                result = leftType.Gt(context, left, right);
                if (!result.IsNotImplemented)
                    return result;
                result = rightType.Lt(context, right, left);
                break;

            case PyOperatorTypes.Ge:
                result = leftType.Ge(context, left, right);
                if (!result.IsNotImplemented)
                    return result;
                result = rightType.Le(context, right, left);
                break;

            default:
                return PyResult.RaiseTypeError($"Operator '{OperatorToString(op)}' is not supported.");
        }

        if (result.IsNotImplemented)
            return PyResult.RaiseTypeError($"'{OperatorToString(op)}' not supported between instances of '{left.PyType.Name}' and '{right.PyType.Name}'");

        return result;
    }

    private static PyResult RightFirstReflectiveOperator(PyCallContext context, PyOperatorTypes op, PyObject left, PyObject right, PyObject? modulo)
    {
        PyResult result;
        var leftType = left.PyType;
        var rightType = right.PyType;
        switch (op)
        {
            case PyOperatorTypes.Add:
                result = rightType.RAdd(context, right, left);
                if (!result.IsNotImplemented)
                    return result;
                result = leftType.Add(context, left, right);
                break;
            case PyOperatorTypes.Sub:
                result = rightType.RSub(context, right, left);
                if (!result.IsNotImplemented)
                    return result;
                result = leftType.Sub(context, left, right);
                break;
            case PyOperatorTypes.Mul:
                result = rightType.RMul(context, right, left);
                if (!result.IsNotImplemented)
                    return result;
                result = leftType.Mul(context, left, right);
                break;
            case PyOperatorTypes.TrueDiv:
                result = rightType.RTrueDiv(context, right, left);
                if (!result.IsNotImplemented)
                    return result;
                result = leftType.TrueDiv(context, left, right);
                break;
            case PyOperatorTypes.FloorDiv:
                result = rightType.RFloorDiv(context, right, left);
                if (!result.IsNotImplemented)
                    return result;
                result = leftType.FloorDiv(context, left, right);
                break;
            case PyOperatorTypes.Mod:
                result = rightType.RMod(context, right, left);
                if (!result.IsNotImplemented)
                    return result;
                result = leftType.Mod(context, left, right);
                break;
            case PyOperatorTypes.Pow:
                Debug.Assert(modulo is not null);
                result = rightType.RPow(context, right, left, modulo);
                if (!result.IsNotImplemented)
                    return result;
                result = leftType.Pow(context, left, right, modulo);
                break;
            case PyOperatorTypes.LShift:
                result = rightType.RLShift(context, right, left);
                if (!result.IsNotImplemented)
                    return result;
                result = leftType.LShift(context, left, right);
                break;
            case PyOperatorTypes.RShift:
                result = rightType.RRShift(context, right, left);
                if (!result.IsNotImplemented)
                    return result;
                result = leftType.RShift(context, left, right);
                break;
            case PyOperatorTypes.And:
                result = rightType.RAnd(context, right, left);
                if (!result.IsNotImplemented)
                    return result;
                result = leftType.And(context, left, right);
                break;
            case PyOperatorTypes.Xor:
                result = rightType.RXor(context, right, left);
                if (!result.IsNotImplemented)
                    return result;
                result = leftType.Xor(context, left, right);
                break;
            case PyOperatorTypes.Or:
                result = rightType.ROr(context, right, left);
                if (!result.IsNotImplemented)
                    return result;
                result = leftType.Or(context, left, right);
                break;
            case PyOperatorTypes.Lt:
                result = rightType.Gt(context, right, left);
                if (!result.IsNotImplemented)
                    return result;
                result = leftType.Lt(context, left, right);
                break;
            case PyOperatorTypes.Le:
                result = rightType.Ge(context, right, left);
                if (!result.IsNotImplemented)
                    return result;
                result = leftType.Le(context, left, right);
                break;
            case PyOperatorTypes.Gt:
                result = rightType.Lt(context, right, left);
                if (!result.IsNotImplemented)
                    return result;
                result = leftType.Gt(context, left, right);
                break;
            case PyOperatorTypes.Ge:
                result = rightType.Le(context, right, left);
                if (!result.IsNotImplemented)
                    return result;
                result = leftType.Ge(context, left, right);
                break;
            default:
                return PyResult.RaiseTypeError($"Operator '{OperatorToString(op)}' is not supported.");
        }

        if (result.IsNotImplemented)
            return PyResult.RaiseTypeError($"'{OperatorToString(op)}' not supported between instances of '{left.PyType.Name}' and '{right.PyType.Name}'");

        return result;
    }

    private static PyResult ReflectiveOperator(PyCallContext context, PyOperatorTypes op, PyObject left, PyObject right, PyObject? modulo = null)
    {
        if (left is PyIntObject leftInt && right is PyIntObject rightInt)
            return PyMath.CalculatePyIntObject(op, leftInt, rightInt, modulo);

        if (left.PyType != right.PyType && right.PyType.IsSubclassOf(left.PyType))
            return RightFirstReflectiveOperator(context, op, left, right, modulo);
        return LeftFirstReflectiveOperator(context, op, left, right, modulo);
    }

    public static PyResult Add(PyCallContext context, PyObject left, PyObject right)
    {
        return ReflectiveOperator(context, PyOperatorTypes.Add, left, right);
    }
    public static PyResult Sub(PyCallContext context, PyObject left, PyObject right)
    {
        return ReflectiveOperator(context, PyOperatorTypes.Sub, left, right);
    }
    public static PyResult Mul(PyCallContext context, PyObject left, PyObject right)
    {
        return ReflectiveOperator(context, PyOperatorTypes.Mul, left, right);
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
    public static PyResult And(PyCallContext context, PyObject left, PyObject right)
    {
        return ReflectiveOperator(context, PyOperatorTypes.And, left, right);
    }
    public static PyResult Xor(PyCallContext context, PyObject left, PyObject right)
    {
        return ReflectiveOperator(context, PyOperatorTypes.Xor, left, right);
    }
    public static PyResult Or(PyCallContext context, PyObject left, PyObject right)
    {
        return ReflectiveOperator(context, PyOperatorTypes.Or, left, right);
    }
    public static PyResult Lt(PyCallContext context, PyObject left, PyObject right)
    {
        return ReflectiveOperator(context, PyOperatorTypes.Lt, left, right);
    }
    public static PyResult Le(PyCallContext context, PyObject left, PyObject right)
    {
        return ReflectiveOperator(context, PyOperatorTypes.Le, left, right);
    }
    public static PyResult Gt(PyCallContext context, PyObject left, PyObject right)
    {
        return ReflectiveOperator(context, PyOperatorTypes.Gt, left, right);
    }
    public static PyResult Ge(PyCallContext context, PyObject left, PyObject right)
    {
        return ReflectiveOperator(context, PyOperatorTypes.Ge, left, right);
    }

    public static PyResult Eq(PyCallContext context, PyObject left, PyObject right)
    {
        var ret = left.Eq(context, right);
        if (!ret.IsNotImplemented)
            return ret;

        ret = right.Eq(context, left);
        if (ret.IsNotImplemented)
            return Is(left, right);

        return ret;
    }
    public static PyResult Ne(PyCallContext context, PyObject left, PyObject right)
    {
        var ret = left.Ne(context, right);
        if (!ret.IsNotImplemented)
            return ret;

        ret = right.Ne(context, left);
        if (!ret.IsNotImplemented)
            return ret;

        var eq = Eq(context, left, right);
        if (eq.IsError)
            return eq;

        Debug.Assert(!eq.IsNotImplemented);

        var boolRet = eq.Value.Bool(context);
        if (boolRet.IsError)
            return boolRet;

        if (!PySpecialMethods.TryGetBool(boolRet.Value, out var b))
            return PyResult.CaptureExceptionFromPVM();

        return PyBoolObject.FromBoolean(!b.BoolValue);
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

    public static PyResult GetAttr(PyCallContext context, PyObject target, string name)
    {
        var attr = target.GetAttribute(context, name);
        if (!attr.IsAttributeError)
            return attr;
        return target.GetAttr(context, name);
    }
    public static PyResult SetAttr(PyCallContext context, PyObject target, string name, PyObject value)
    {
        return target.SetAttr(context, name, value);
    }
    public static PyResult DelAttr(PyCallContext context, PyObject target, string name)
    {
        return target.DelAttr(context, name);
    }
}
