using PySharp.PyModules.Builtins;
using System.Diagnostics;

namespace PySharp.PyRuntime;

public static class PyOperators
{
    private static PyObject? ReflectiveOperator(string operatorDisplay, PyObject left, PyObject right, Func<PyObject?> leftOp, Func<PyObject?> rightOp)
    {
        if (left.PyType.IsSubclass(right.PyType) && left.PyType != right.PyType)
            (leftOp, rightOp) = (rightOp, leftOp);

        var ret = leftOp();
        if (ret is not PyNotImplementedObject) // null or any value other than NotImplemented
            return ret;

        ret = rightOp();
        if (ret is PyNotImplementedObject)
            return PyVirtualMachine.RaiseTypeError($"'{operatorDisplay}' not supported between instances of '{left.PyType.Name}' and '{right.PyType.Name}'");

        return ret;
    }

    public static PyObject? Add(PyObject left, PyObject right)
    {
        return ReflectiveOperator("+", left, right, () => left.Add(right), () => right.RAdd(left));
    }
    public static PyObject? Sub(PyObject left, PyObject right)
    {
        return ReflectiveOperator("-", left, right, () => left.Sub(right), () => right.RSub(left));
    }
    public static PyObject? Mul(PyObject left, PyObject right)
    {
        return ReflectiveOperator("*", left, right, () => left.Mul(right), () => right.RMul(left));
    }
    public static PyObject? TrueDiv(PyObject left, PyObject right)
    {
        return ReflectiveOperator("/", left, right, () => left.TrueDiv(right), () => right.RTrueDiv(left));
    }
    public static PyObject? FloorDiv(PyObject left, PyObject right)
    {
        return ReflectiveOperator("//", left, right, () => left.FloorDiv(right), () => right.RFloorDiv(left));
    }
    public static PyObject? Mod(PyObject left, PyObject right)
    {
        return ReflectiveOperator("%", left, right, () => left.Mod(right), () => right.RMod(left));
    }
    public static PyObject? Pow(PyObject left, PyObject right, PyObject modulo)
    {
        return ReflectiveOperator("**", left, right, () => left.Pow(right, modulo), () => right.RPow(left, modulo));
    }
    public static PyObject? LShift(PyObject left, PyObject right)
    {
        return ReflectiveOperator("<<", left, right, () => left.LShift(right), () => right.RLShift(left));
    }
    public static PyObject? RShift(PyObject left, PyObject right)
    {
        return ReflectiveOperator(">>", left, right, () => left.RShift(right), () => right.RRShift(left));
    }
    public static PyObject? And(PyObject left, PyObject right)
    {
        return ReflectiveOperator("&", left, right, () => left.And(right), () => right.RAnd(left));
    }
    public static PyObject? Xor(PyObject left, PyObject right)
    {
        return ReflectiveOperator("^", left, right, () => left.Xor(right), () => right.RXor(left));
    }
    public static PyObject? Or(PyObject left, PyObject right)
    {
        return ReflectiveOperator("|", left, right, () => left.Or(right), () => right.ROr(left));
    }

    public static PyObject? Lt(PyObject left, PyObject right)
    {
        return ReflectiveOperator("<", left, right, () => left.Lt(right), () => right.Gt(left));
    }
    public static PyObject? Le(PyObject left, PyObject right)
    {
        return ReflectiveOperator("<=", left, right, () => left.Le(right), () => right.Ge(left));
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
        return ReflectiveOperator(">", left, right, () => left.Gt(right), () => right.Lt(left));
    }
    public static PyObject? Ge(PyObject left, PyObject right)
    {
        return ReflectiveOperator(">=", left, right, () => left.Ge(right), () => right.Le(left));
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
