using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;

namespace PySharp.Utility;

// CVE-2020-10735 guard: limits decimal int<->str conversions like CPython's
// sys.set_int_max_str_digits() (default 4300 digits, 0 disables). Power-of-two
// bases are never limited. The limit value itself is per-environment state
// (PyEnvironment.IntStrDigits, like CPython's interpreter-scoped
// long_state.max_str_digits); this type only carries the constants and the
// pure checks over a caller-supplied limit.
internal static class PyIntStrDigitsLimit
{
    public const int DefaultMaxStrDigits = 4300;
    public const int MinMaxStrDigits = 640;

    // False when the value is not a valid limit (CPython
    // _PySys_SetIntMaxStrDigits).
    public static bool IsValidLimit(int value)
    {
        return value is 0 || value >= MinMaxStrDigits;
    }

    // Parse direction: only counts above the 640 threshold are checked
    // (CPython long_from_string_base).
    public static bool IsOverLimit(int digitCount, int maxStrDigits)
    {
        return digitCount > MinMaxStrDigits && maxStrDigits > 0 && digitCount > maxStrDigits;
    }

    // Format direction: converts to decimal unless the value certainly has
    // more digits than the limit. A bit-length estimate decides far from the
    // boundary, the exact digit count decides near it (CPython
    // long_to_decimal_string).
    public static bool TryToDecimalString(BigInteger value, int maxStrDigits, [NotNullWhen(true)] out string? text)
    {
        text = null;

        if (maxStrDigits <= 0)
        {
            text = value.ToString(CultureInfo.InvariantCulture);
            return true;
        }

        var bits = value.GetBitLength();
        if (bits > 0)
        {
            // A b-bit magnitude has at least (b-1)*log10(2)+1 and at most
            // b*log10(2)+1 decimal digits; both margins stay safe.
            var lowerBound = (bits - 1) * 0.3010299956639812;
            if (lowerBound > maxStrDigits + 1.0)
                return false;
            if (lowerBound < maxStrDigits - 2.0)
            {
                text = value.ToString(CultureInfo.InvariantCulture);
                return true;
            }
        }

        var candidate = value.ToString(CultureInfo.InvariantCulture);
        var digitCount = candidate.Length - (candidate[0] is '-' ? 1 : 0);
        if (digitCount > maxStrDigits)
            return false;

        text = candidate;
        return true;
    }
}
