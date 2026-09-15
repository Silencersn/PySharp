using PySharp.Modules.Builtins;
using PySharp.Runtime.Calls;
using PySharp.Resources;
using PySharp.Utility;
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
                return PyResult.TypeError(PySR.Runtime_Operator_UnsupportedOperand, "@", "int", "int");

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
                // same sign correction as Mod below: a non-zero remainder
                // with the divisor's opposite sign means the truncated
                // quotient is one above the floored one (-1 // 2**32 == -1)
                if (!r.IsZero && left.Value.Sign != right.Value.Sign)
                    q -= 1;
                return PyIntObject.FromInteger(q);

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
                        return PyIntObject.FromInteger(BigInteger.Pow(left.Value, right.Int32Value));
                    // CPython long_pow converts a negative exponent to float,
                    // where 0.0 ** negative raises instead of returning inf.
                    if (left.Value.IsZero)
                        return PyResult.ZeroDivisionError(PySR.Runtime_Number_ZeroToNegativePower);
                    if (!left.Value.TryToDoubleRounded(out var baseValue) || !right.Value.TryToDoubleRounded(out var exponentValue))
                        return PyResult.OverflowError(PySR.Runtime_Number_IntTooLargeForFloat);
                    return PyFloatObject.FromDouble(Math.Pow(baseValue, exponentValue));
                }
                else
                {
                    if (modulo is not PyIntObject moduloObj)
                        return PyNotImplementedObject.NotImplemented;

                    var modulus = moduloObj.Value;
                    if (modulus.IsZero)
                        return PyResult.ValueError(PySR.Runtime_Number_PowWithZeroModulo);

                    // CPython long_pow: a negative modulus is applied at the very
                    // end (result -= |mod|); all computation uses |mod|.
                    var negativeOutput = modulus.Sign < 0;
                    if (negativeOutput)
                        modulus = BigInteger.Abs(modulus);

                    if (modulus.IsOne)
                        return PyIntObject.Zero;   // pow(x, y, 1) == 0

                    BigInteger result;
                    if (right.Value < 0)
                    {
                        // pow(base, -exp, mod): compute the modular inverse of base
                        // (CPython 3.8+); base must be coprime with mod or ValueError.
                        var inv = TryModInverse(left.Value, modulus);
                        if (inv is null)
                            return PyResult.ValueError("base is not invertible for the given modulus");
                        result = BigInteger.ModPow(inv.Value, -right.Value, modulus);
                    }
                    else
                    {
                        result = BigInteger.ModPow(left.Value, right.Value, modulus);
                    }

                    // Normalize to [0, modulus): .NET ModPow returns C# remainder
                    // semantics for a negative base (sign follows the base's power),
                    // e.g. ModPow(-2, 3, 5) == -3, while CPython returns 2.
                    if (result.Sign < 0)
                        result += modulus;

                    if (negativeOutput && !result.IsZero)
                        result -= modulus;
                    return PyIntObject.FromInteger(result);
                }

            case PyOperatorTypes.LShift:
                if (right.Value < 0)
                    return PyResult.ValueError("negative shift count");
                if (left.Value.IsZero)
                    return PyIntObject.Zero;
                if (right.Value > long.MaxValue)
                    return PyResult.OverflowError("too many digits in integer");
                try
                {
                    return PyIntObject.FromInteger(ShiftLeftByInt64(left.Value, (long)right.Value));
                }
                catch (Exception e) when (e is OverflowException or OutOfMemoryException)
                {
                    // The result cannot be represented at all; with the digit
                    // limit active any decimal conversion of it would report
                    // that limit, without it there is only out of memory.
                    if (PyIntStrDigitsLimit.MaxStrDigits > 0)
                        return PyResult.ValueError(PySR.Runtime_Number_Int_ExceedsMaxStrDigitsResult, PyIntStrDigitsLimit.MaxStrDigits);
                    return PyResult.MemoryError(null);
                }

            case PyOperatorTypes.RShift:
                if (right.Value < 0)
                    return PyResult.ValueError("negative shift count");
                if (left.Value.IsZero)
                    return PyIntObject.Zero;
                if (right.Value > long.MaxValue)
                    return left.Value < 0 ? PyIntObject.MinusOne : PyIntObject.Zero;
                return PyIntObject.FromInteger(ShiftRightByInt64(left.Value, (long)right.Value));

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

    // BigInteger shifts take an int count; split counts beyond int.MaxValue
    // into chunks (CPython takes the shift amount as int64).
    private static BigInteger ShiftLeftByInt64(BigInteger value, long count)
    {
        while (count > int.MaxValue)
        {
            value <<= int.MaxValue;
            count -= int.MaxValue;
        }
        return value << (int)count;
    }

    private static BigInteger ShiftRightByInt64(BigInteger value, long count)
    {
        // A shift reaching past the magnitude saturates: 0 for non-negative
        // values, -1 for negative ones (floor semantics).
        if (count >= BigInteger.Abs(value).GetBitLength())
            return value < 0 ? -1 : 0;

        while (count > int.MaxValue)
        {
            value >>= int.MaxValue;
            count -= int.MaxValue;
        }
        return value >> (int)count;
    }

    // Extended Euclidean modular inverse of a modulo m (m > 0), or null when
    // a and m are not coprime (mirrors CPython's long_invmod).
    private static BigInteger? TryModInverse(BigInteger a, BigInteger m)
    {
        if (m.IsOne)
            return BigInteger.Zero;

        a %= m;
        if (a.Sign < 0)
            a += m;

        if (BigInteger.GreatestCommonDivisor(a, m) != BigInteger.One)
            return null;

        var m0 = m;
        BigInteger y = 0, x = 1;
        while (a > 1)
        {
            var q = a / m;
            (a, m) = (m, a % m);
            (y, x) = (x - q * y, y);
        }
        return x.Sign < 0 ? x + m0 : x;
    }
}
