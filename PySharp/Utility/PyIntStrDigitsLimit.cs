using System.Diagnostics.CodeAnalysis;
using System.Numerics;

namespace PySharp.Utility;

// CVE-2020-10735 guard: limits decimal int<->str conversions like CPython's
// sys.set_int_max_str_digits() (default 4300 digits, 0 disables). Power-of-two
// bases are never limited.
internal static class PyIntStrDigitsLimit
{
    public const int DefaultMaxStrDigits = 4300;
    public const int MinMaxStrDigits = 640;

    private static int _maxStrDigits = DefaultMaxStrDigits;

    public static int MaxStrDigits => _maxStrDigits;

    // False when the value is not a valid limit (CPython
    // _PySys_SetIntMaxStrDigits).
    public static bool TrySetMaxStrDigits(int value)
    {
        if (value is not 0 && value < MinMaxStrDigits)
            return false;

        _maxStrDigits = value;
        return true;
    }

    // Parse direction: only counts above the 640 threshold are checked
    // (CPython long_from_string_base).
    public static bool IsOverLimit(int digitCount)
    {
        return digitCount > MinMaxStrDigits && _maxStrDigits > 0 && digitCount > _maxStrDigits;
    }

    // Format direction: converts to decimal unless the value certainly has
    // more digits than the limit. A bit-length estimate decides far from the
    // boundary, the exact digit count decides near it (CPython
    // long_to_decimal_string).
    public static bool TryToDecimalString(BigInteger value, [NotNullWhen(true)] out string? text)
    {
        text = null;

        var max = _maxStrDigits;
        if (max <= 0)
        {
            text = value.ToString();
            return true;
        }

        var bits = value.GetBitLength();
        if (bits > 0)
        {
            // A b-bit magnitude has at least (b-1)*log10(2)+1 and at most
            // b*log10(2)+1 decimal digits; both margins stay safe.
            var lowerBound = (bits - 1) * 0.3010299956639812;
            if (lowerBound > max + 1.0)
                return false;
            if (lowerBound < max - 2.0)
            {
                text = value.ToString();
                return true;
            }
        }

        var candidate = value.ToString();
        var digitCount = candidate.Length - (candidate[0] is '-' ? 1 : 0);
        if (digitCount > max)
            return false;

        text = candidate;
        return true;
    }
}
