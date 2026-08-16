using System.Diagnostics;
using System.Globalization;
using System.Numerics;

namespace PySharp.Utility;

internal static class BigIntegerHelper
{
    public static bool TryParse(ReadOnlySpan<char> s, int numBase, out BigInteger result)
    {
        Debug.Assert(numBase is 0 or (>= 2 and <= 36));

        result = default;

        s = s.Trim();
        if (s.IsEmpty)
            return false;

        bool negative = false;
        if (s[0] is '+' or '-')
        {
            negative = s[0] is '-';
            s = s[1..];
        }
        if (s.IsEmpty)
            return false;

        if (!TryConvertCharToInt(s[0], out _))
            return false;

        if (numBase is 0)
        {
            if (s.StartsWith("0x") || s.StartsWith("0X"))
                numBase = 16;
            else if (s.StartsWith("0b") || s.StartsWith("0B"))
                numBase = 2;
            else if (s.StartsWith("0o") || s.StartsWith("0O"))
                numBase = 8;
            else
                numBase = 10;

            if (numBase is not 10)
            {
                s = s[2..];
                if (!ValidateAfterRemovingPrefix(s))
                    return false;
            }
        }
        else if (numBase is 16)
        {
            if (s.StartsWith("0x") || s.StartsWith("0X"))
            {
                s = s[2..];
                if (!ValidateAfterRemovingPrefix(s))
                    return false;
            }
        }
        else if (numBase is 2)
        {
            if (s.StartsWith("0b") || s.StartsWith("0B"))
            {
                s = s[2..];
                if (!ValidateAfterRemovingPrefix(s))
                    return false;
            }
        }
        else if (numBase is 8)
        {
            if (s.StartsWith("0o") || s.StartsWith("0O"))
            {
                s = s[2..];
                if (!ValidateAfterRemovingPrefix(s))
                    return false;
            }
        }

        bool containsUnderline = s.Contains('_');
        if (containsUnderline)
        {
            if (s[^1] is '_')
                return false;

            if (s.Contains("__", StringComparison.Ordinal))
                return false;
        }

        if (numBase is 10 && !containsUnderline)
        {
            if (!TryParseBase10(s, out result))
                return false;
        }
        else
        {
            if (!TryParseBaseN(s, numBase, out result))
                return false;
        }

        result = negative ? -result : result;
        return true;

        static bool ValidateAfterRemovingPrefix(ReadOnlySpan<char> s)
        {
            if (s.IsEmpty)
                return false;

            if (s[0] is '_')
            {
                s = s[1..];
                if (s.IsEmpty)
                    return false;

                if (s[0] is '_')
                    return false;
            }

            return true;
        }
    }

    private static bool TryParseBase10(ReadOnlySpan<char> s, out BigInteger result)
    {
        return BigInteger.TryParse(s, NumberStyles.None, provider: null, out result);
    }

    private static bool TryParseBaseN(ReadOnlySpan<char> s, int numBase, out BigInteger result)
    {
        result = 0;
        foreach (var c in s)
        {
            if (c is '_')
                continue;

            if (!TryConvertCharToInt(c, out var value))
                return false;

            if (value >= numBase)
                return false;

            result *= numBase;
            result += value;
        }

        return true;
    }

    private static bool TryConvertCharToInt(char c, out int value)
    {
        if (c >= CharToNumberLookup.Length)
        {
            value = 0;
            return false;
        }

        value = CharToNumberLookup[c];
        return value is not 0xFF;
    }

    private static ReadOnlySpan<byte> CharToNumberLookup =>
    [
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 15
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 31
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 47
        0   , 1,    2,    3,    4,    5,    6,    7,    8,    9,    0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 63
        0xFF, 10,   11,   12,   13,   14,   15,   16,   17,   18,   19,   20,   21,   22,   23,   24,   // 79
        25  , 26  , 27  , 28  , 29  , 30  , 31  , 32  , 33  , 34  , 35  , 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 95
        0xFF, 10,   11,   12,   13,   14,   15,   16,   17,   18,   19,   20,   21,   22,   23,   24,   // 111
        25  , 26  , 27  , 28  , 29  , 30  , 31  , 32  , 33  , 34  , 35  , 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 127
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 143
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 159
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 175
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 191
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 207
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 223
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 239
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF  // 255
    ];


    public static string ToString(BigInteger value, int numBase)
    {
        Debug.Assert(numBase is 2 or 8 or 16);

        var charCount = GetMaxCharCount(value, numBase);
        char[]? arrayToReturn = null;
        Span<char> chars = charCount <= 512
            ? stackalloc char[charCount]
            : PoolHelper.Rent(charCount, out arrayToReturn);

        int charsWritten;
        if (numBase is 2)
            ToPowerOfTwoString(chars, value, out charsWritten, 'b', 1);
        else if (numBase is 8)
            ToPowerOfTwoString(chars, value, out charsWritten, 'o', 3);
        else if (numBase is 16)
            ToPowerOfTwoString(chars, value, out charsWritten, 'x', 4);
        else
            throw new UnreachableException();
        var str = chars[..charsWritten].ToString();

        PoolHelper.ReturnIfNonNull(arrayToReturn);

        return str;
    }

    /// <summary>Formats a non-negative BigInteger in base 2/8/16 WITHOUT the
    /// "0b"/"0o"/"0x" prefix (used by int.__format__).</summary>
    public static string ToStringDigits(BigInteger value, int numBase)
    {
        Debug.Assert(numBase is 2 or 8 or 16);
        Debug.Assert(value >= 0);

        var charCount = GetMaxDigitsCount(value, numBase);
        char[]? arrayToReturn = null;
        Span<char> chars = charCount <= 512
            ? stackalloc char[charCount]
            : PoolHelper.Rent(charCount, out arrayToReturn);

        int charsWritten = ToDigitsInBase(chars, value, numBase);

        var str = chars[..charsWritten].ToString();
        PoolHelper.ReturnIfNonNull(arrayToReturn);
        return str;
    }

    private static int ToDigitsInBase(Span<char> chars, BigInteger value, int numBase)
    {
        if (value.IsZero)
        {
            chars[0] = '0';
            return 1;
        }

        Debug.Assert(numBase is 2 or 8 or 16);
        var shift = numBase switch
        {
            2 => 1,
            8 => 3,
            16 => 4,
            _ => throw new UnreachableException()
        };

        var byteCount = value.GetByteCount(isUnsigned: true);
        byte[]? arrayToReturn = null;
        Span<byte> bytes = byteCount <= 1024
            ? stackalloc byte[byteCount]
            : PoolHelper.Rent(byteCount, out arrayToReturn);

        var written = value.TryWriteBytes(bytes, out var bytesWritten, isUnsigned: true, isBigEndian: true);
        Debug.Assert(written);
        Debug.Assert(bytesWritten == byteCount);

        int index = ToPowerOfTwoDigits(chars, bytes[..bytesWritten], shift);

        PoolHelper.ReturnIfNonNull(arrayToReturn);
        return index;
    }

    /// <summary>Writes the minimal digits of a positive magnitude (big-endian
    /// bytes with no leading zero byte) in a power-of-two base (2/8/16) to
    /// <paramref name="chars"/> by extracting bits (O(n)), avoiding the O(n^2)
    /// repeated BigInteger division. Returns the number of digits written.</summary>
    private static int ToPowerOfTwoDigits(Span<char> chars, ReadOnlySpan<byte> bytes, int shift)
    {
        Debug.Assert(shift is 1 or 3 or 4);
        var mask = (1 << shift) - 1;

        int index = 0;
        bool firstGroup = true;
        int offset = 0;
        int groupBytes = bytes.Length % 3;
        if (groupBytes is 0)
            groupBytes = 3;

        // Process the magnitude 3 bytes (24 bits) at a time. The leading group
        // may be 1-2 bytes; its leading zero digits are skipped so the result
        // is minimal, while later groups always emit every digit.
        while (offset < bytes.Length)
        {
            int gb = firstGroup ? groupBytes : 3;
            int groupBits = gb * 8;
            uint group = 0;
            for (int i = 0; i < gb; i++)
                group = (group << 8) | bytes[offset + i];
            offset += gb;

            // The top digit of a group is narrower than `shift` bits when the
            // group's bit count is not a multiple of `shift`.
            int topBits = groupBits % shift;
            if (topBits is 0)
                topBits = shift;
            int remaining = groupBits;

            while (remaining > 0)
            {
                int take = remaining == groupBits ? topBits : shift;
                int digit = (int)((group >> (remaining - take)) & mask);
                remaining -= take;
                if (firstGroup && index is 0 && digit is 0)
                    continue; // strip leading zeros of the very first digit
                chars[index++] = digit < 10 ? (char)('0' + digit) : (char)('a' + digit - 10);
            }
            firstGroup = false;
        }

        return index;
    }

    private static int GetMaxDigitsCount(BigInteger value, int numBase)
    {
        Debug.Assert(numBase is 2 or 8 or 16);

        var byteCount = BigInteger.Abs(value).GetByteCount(isUnsigned: true);
        var digits = numBase switch
        {
            2 => byteCount * 8,
            8 => (byteCount * 8 + 2) / 3,
            16 => byteCount * 2,
            _ => throw new UnreachableException()
        };
        // A single digit is always needed (value 0 formats as "0").
        return Math.Max(1, digits);
    }

    private static int GetMaxCharCount(BigInteger value, int numBase)
    {
        return GetMaxDigitsCount(value, numBase) + 1 /* sign */ + 2 /* prefix */;
    }

    private static void ToPowerOfTwoString(Span<char> chars, BigInteger value, out int charsWritten, char prefixChar, int shift)
    {
        Debug.Assert(prefixChar is 'b' or 'o' or 'x');

        if (value == 0)
        {
            // Keep the single digit: "0x0" / "0b0" / "0o0".
            chars[0] = '0';
            chars[1] = prefixChar;
            chars[2] = '0';
            charsWritten = 3;
            return;
        }

        bool isNegative = value < 0;
        BigInteger absValue = isNegative ? -value : value;

        int index = 0;
        if (isNegative)
        {
            chars[index++] = '-';
            chars[index++] = '0';
            chars[index++] = prefixChar;
        }
        else
        {
            chars[index++] = '0';
            chars[index++] = prefixChar;
        }

        var byteCount = absValue.GetByteCount(isUnsigned: true);
        byte[]? arrayToReturn = null;
        Span<byte> bytes = byteCount <= 1024
            ? stackalloc byte[byteCount]
            : PoolHelper.Rent(byteCount, out arrayToReturn);

        var written = absValue.TryWriteBytes(bytes, out var bytesWritten, isUnsigned: true, isBigEndian: true);
        Debug.Assert(written);
        Debug.Assert(bytesWritten == byteCount);

        index += ToPowerOfTwoDigits(chars[index..], bytes[..bytesWritten], shift);

        PoolHelper.ReturnIfNonNull(arrayToReturn);
        charsWritten = index;
    }
}
