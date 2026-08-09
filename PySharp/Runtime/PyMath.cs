using PySharp.Modules.Builtins;
using PySharp.Runtime.Calls;
using System.Diagnostics;
using System.Numerics;

namespace PySharp.Runtime;

internal static class PyMath
{
    public static PyResult CalculatePyIntObject(PyOperatorTypes op, PyIntObject left, PyIntObject right, PyObject? modulo = null)
    {
        switch (op)
        {
            case PyOperatorTypes.Add:
                return PyIntObject.FromInteger(left.Value + right.Value);

            case PyOperatorTypes.Sub:
                return PyIntObject.FromInteger(left.Value - right.Value);

            case PyOperatorTypes.Mult:
                return PyIntObject.FromInteger(left.Value * right.Value);

            case PyOperatorTypes.MatMult:
                return PyResult.TypeError(PySR.Runtime_Operator_UnsupportedBetween, "@", "int", "int");

            case PyOperatorTypes.TrueDiv:
                {
                    if (right.Value.IsZero)
                        return PyResult.ZeroDivisionError();
                    var (tdQ, tdR) = BigInteger.DivRem(left.Value, right.Value);
                    if (tdR.IsZero)
                    {
                        // exact quotient: avoid inf/inf -> NaN and detect float overflow
                        var dq = (double)tdQ;
                        if (double.IsInfinity(dq))
                            return PyResult.OverflowError("integer division result too large for a float");
                        return PyFloatObject.FromDouble(dq);
                    }
                    // exact overflow check: |left/right| >= 2^1024  <=>  |left| >= |right| << 1024
                    if (BigInteger.Abs(left.Value) >= BigInteger.Abs(right.Value) << 1024)
                        return PyResult.OverflowError("integer division result too large for a float");
                    // result = q + r/right (0 < |r| < |right|) without intermediate (double) overflow
                    var dQ = (double)tdQ;
                    if (double.IsInfinity(dQ))
                        dQ = dQ > 0 ? double.MaxValue : -double.MaxValue;   // q near 2^1024: still finite in CPython
                    var scaledR = BigInteger.Abs(tdR) * (BigInteger.One << 53) / BigInteger.Abs(right.Value);
                    var frac = (double)scaledR / 9007199254740992.0;         // 2^53: r/right as a 53-bit fraction
                    if ((tdR < 0) != (right.Value < 0))
                        frac = -frac;
                    return PyFloatObject.FromDouble(dQ + frac);
                }

            case PyOperatorTypes.FloorDiv:
                if (right.Value.IsZero)
                    return PyResult.ZeroDivisionError();
                var (q, r) = BigInteger.DivRem(left.Value, right.Value);
                if (r.IsZero || BigInteger.IsPositive(q))
                    return PyIntObject.FromInteger(q);
                return PyIntObject.FromInteger(q - 1);

            case PyOperatorTypes.Mod:
                if (right.Value.IsZero)
                    return PyResult.ZeroDivisionError();

                if (left.Value.IsZero)
                    return PyIntObject.Zero;

                var mod = left.Value % right.Value;
                if (!mod.IsZero && left.Value.Sign != right.Value.Sign)
                    mod += right.Value;
                return PyIntObject.FromInteger(mod);

            case PyOperatorTypes.Pow:
                Debug.Assert(modulo is not null);
                if (modulo is PyNoneObject)
                {
                    if (right.Value >= 0)
                        return PyIntObject.FromInteger(BigInteger.Pow(left.Value, (int)right.Value));
                    return PyFloatObject.FromDouble(Math.Pow((double)left.Value, (double)right.Value));
                }
                else
                {
                    if (modulo is not PyIntObject moduloObj)
                        return PyNotImplementedObject.NotImplemented;

                    if (moduloObj.Value.IsZero)
                        return PyResult.ValueError(PySR.Runtime_Number_PowWithZeroModulo);

                    if (right.Value >= 0)
                        return PyIntObject.FromInteger(BigInteger.ModPow(left.Value, right.Value, moduloObj.Value));

                    return PyFloatObject.FromDouble(Math.Pow((double)left.Value, (double)right.Value) % (double)moduloObj.Value);
                }

            case PyOperatorTypes.LShift:
                if (right.Value < 0)
                    return PyResult.ValueError("negative shift count");
                if (!right.IsInt32)
                    return PyResult.OverflowError("too many digits in integer");
                return PyIntObject.FromInteger(left.Value << right.Int32Value);

            case PyOperatorTypes.RShift:
                if (right.Value < 0)
                    return PyResult.ValueError("negative shift count");
                if (!right.IsInt32)
                    return PyResult.OverflowError("too many digits in integer");
                return PyIntObject.FromInteger(left.Value >> right.Int32Value);

            case PyOperatorTypes.BitAnd:
                return PyIntObject.FromInteger(left.Value & right.Value);

            case PyOperatorTypes.BitOr:
                return PyIntObject.FromInteger(left.Value | right.Value);

            case PyOperatorTypes.BitXor:
                return PyIntObject.FromInteger(left.Value ^ right.Value);

            case PyOperatorTypes.Lt:
                return PyBoolObject.FromBoolean(left.Value < right.Value);

            case PyOperatorTypes.LtE:
                return PyBoolObject.FromBoolean(left.Value <= right.Value);

            case PyOperatorTypes.Eq:
                return PyBoolObject.FromBoolean(left.Value == right.Value);

            case PyOperatorTypes.NotEq:
                return PyBoolObject.FromBoolean(left.Value != right.Value);

            case PyOperatorTypes.Gt:
                return PyBoolObject.FromBoolean(left.Value > right.Value);

            case PyOperatorTypes.GtE:
                return PyBoolObject.FromBoolean(left.Value >= right.Value);

            default:
                throw new UnreachableException();
        }
    }

}
