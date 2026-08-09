using PySharp.Modules.Builtins;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace PySharp.Runtime;

internal static class PyHash
{
    // CPython constants (pyhash.h / longobject.c): _PyHASH_MODULUS = 2**61 - 1,
    // _PyHASH_BITS = 61, _PyHASH_INF = 314159, PyLong_SHIFT = 30.
    private const ulong Modulus = (1UL << 61) - 1;
    private const int Bits = 61;
    private const int LongShift = 30;
    private const ulong Inf = 314159;

    /// <summary>CPython long_hash: reduce an integer modulo 2**61 - 1 so that
    /// hash(1) == 1, hash(-1) == -2 (error sentinel), and huge ints are bounded
    /// and consistent with HashDouble for integral floats.</summary>
    public static BigInteger HashLong(BigInteger value)
    {
        // Fast path: compact values (single 30-bit digit) hash to themselves,
        // except -1 which is mapped to -2 (the error sentinel).
        if (value >= -(1 << LongShift) && value < (1 << LongShift))
        {
            if (value == -1)
                return -2;
            return value;
        }

        var negative = value.Sign < 0;
        var abs = BigInteger.Abs(value);

        // Collect 30-bit digits (low to high) then process high to low,
        // matching CPython's digit loop order.
        var digits = new List<ulong>();
        while (abs > 0)
        {
            digits.Add((ulong)(abs & ((1 << LongShift) - 1)));
            abs >>= LongShift;
        }

        ulong x = 0;
        for (int i = digits.Count - 1; i >= 0; i--)
        {
            x = ((x << LongShift) & Modulus) | (x >> (Bits - LongShift));
            x += digits[i];
            if (x >= Modulus)
                x -= Modulus;
        }

        long result = negative ? -(long)x : (long)x;
        if (result is -1)
            result = -2;
        return result;
    }

    /// <summary>CPython _Py_HashDouble: hash of a float. Matches HashLong for
    /// integral values (1.0 -> 1, -1.0 -> -2) via modular reduction; inf ->
    /// +/-314159; NaN -> object identity hash of the float instance.</summary>
    public static BigInteger HashDouble(double value, PyFloatObject instance)
    {
        if (!double.IsFinite(value))
        {
            if (double.IsPositiveInfinity(value))
                return (long)Inf;
            if (double.IsNegativeInfinity(value))
                return -(long)Inf;
            // NaN: object identity hash (CPython PyObject_GenericHash)
            return RuntimeHelpers.GetHashCode(instance);
        }

        // frexp-like decomposition: value = m * 2^e, 0.5 <= m < 1 (normal).
        long bits = BitConverter.DoubleToInt64Bits(value);
        bool negative = bits < 0;
        int expField = (int)((bits >> 52) & 0x7FF);
        long mant = bits & 0xFFFFFFFFFFFFFL;
        if (expField is not 0)
            mant |= 0x10000000000000L;
        double m = mant / 9007199254740992.0;   // mant / 2**53
        int e = expField is 0 ? -1021 : expField - 1022;
        if (negative)
            m = -m;

        int sign = 1;
        if (m < 0)
        {
            sign = -1;
            m = -m;
        }

        // Process 28 bits at a time (CPython _Py_HashDouble).
        ulong x = 0;
        while (m is not 0)
        {
            x = ((x << 28) & Modulus) | (x >> (Bits - 28));
            m *= 268435456.0;   // 2**28
            e -= 28;
            ulong y = (ulong)m;
            m -= y;
            x += y;
            if (x >= Modulus)
                x -= Modulus;
        }

        // Adjust for the exponent, reduced modulo Bits (61).
        e = e >= 0 ? e % Bits : Bits - 1 - ((-1 - e) % Bits);
        x = ((x << e) & Modulus) | (x >> (Bits - e));

        long result = sign * (long)x;
        if (result is -1)
            result = -2;
        return result;
    }
}
