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
            ToBinString(ref chars, value, out charsWritten);
        else if (numBase is 8)
            ToOctString(chars, value, out charsWritten);
        else if (numBase is 16)
            ToHexString(ref chars, value, out charsWritten);
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

        var charCount = GetMaxCharCount(value, numBase);
        char[]? arrayToReturn = null;
        Span<char> chars = charCount <= 512
            ? stackalloc char[charCount]
            : PoolHelper.Rent(charCount, out arrayToReturn);

        int charsWritten;
        if (numBase is 16)
        {
            // "x" is exact (no leading zero padding); .NET's "b" format pads,
            // so binary digits are generated manually below.
            var ok = value.TryFormat(chars, out charsWritten, "x");
            Debug.Assert(ok);
        }
        else
        {
            charsWritten = ToDigitsInBase(chars, value, numBase);
        }

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

        var digits = new List<char>();
        while (value > 0)
        {
            value = BigInteger.DivRem(value, numBase, out var rem);
            digits.Add((char)('0' + (int)rem));
        }

        int index = 0;
        for (int i = digits.Count - 1; i >= 0; i--)
            chars[index++] = digits[i];
        return index;
    }

    private static int GetMaxCharCount(BigInteger value, int numBase)
    {
        Debug.Assert(numBase is 2 or 8 or 16);

        var byteCount = BigInteger.Abs(value).GetByteCount(isUnsigned: true);
        return numBase switch
        {
            2 => byteCount * 8,
            8 => (byteCount * 8 + 2) / 3,
            16 => byteCount * 2,
            _ => throw new UnreachableException()
        } + 1 /* sign */ + 2 /* prefix */;
    }

    private static void ToBinOrHexString(ref Span<char> chars, BigInteger value, out int charsWritten, char binOrHex)
    {
        Debug.Assert(binOrHex is 'b' or 'x');

        if (value == 0)
        {
            // Keep the single digit: "0x0" / "0b0" (mirrors ToOctString).
            chars[0] = '0';
            chars[1] = binOrHex;
            chars[2] = '0';
            charsWritten = 3;
            return;
        }

        var offset = value < 0 ? 3 : 2; // prefix
        var formatted = BigInteger.Abs(value).TryFormat(chars[offset..], out charsWritten, [binOrHex]);
        Debug.Assert(formatted);

        if (chars[offset] is '0')
        {
            // the first char may be a '0' to prevent being considered negative
            chars = chars[1..];
            charsWritten--;
        }

        if (value < 0)
        {
            chars[0] = '-';
            chars[1] = '0';
            chars[2] = binOrHex;
        }
        else
        {
            chars[0] = '0';
            chars[1] = binOrHex;
        }
        charsWritten += offset;
    }

    private static void ToBinString(ref Span<char> chars, BigInteger value, out int charsWritten)
    {
        ToBinOrHexString(ref chars, value, out charsWritten, 'b');
    }

    private static void ToOctString(Span<char> chars, BigInteger value, out int charsWritten)
    {
        // TODO: temp fix

        if (value == 0)
        {
            chars[0] = '0';
            chars[1] = 'o';
            chars[2] = '0';
            charsWritten = 3;
            return;
        }

        bool isNegative = value < 0;
        BigInteger absValue = isNegative ? -value : value;

        List<char> digits = new List<char>();
        while (absValue > 0)
        {
            BigInteger remainder = absValue % 8;
            absValue /= 8;
            digits.Add((char)('0' + (int)remainder));
        }

        int index = 0;
        if (isNegative)
        {
            chars[index++] = '-';
            chars[index++] = '0';
            chars[index++] = 'o';
        }
        else
        {
            chars[index++] = '0';
            chars[index++] = 'o';
        }

        for (int i = digits.Count - 1; i >= 0; i--)
            chars[index++] = digits[i];

        charsWritten = index;



        // THIS IS WRONG

        //bool isNegative = value < 0;
        //value = BigInteger.Abs(value);

        //var count = value.GetByteCount(true);

        //// bytesLength should be 1 + 3*n (the 1 is padding, the 3 is the value to convert)
        //var bytesLength = count;
        //var offset = 1;
        //if (bytesLength % 3 is not 0)
        //    offset += 3 - bytesLength % 3;
        //bytesLength += offset;


        //byte[]? arrayToReturn = null;
        //Span<byte> bytes = bytesLength <= 1024
        //    ? stackalloc byte[bytesLength]
        //    : PoolHelper.Rent(bytesLength, out arrayToReturn);

        //var written = value.TryWriteBytes(bytes[offset..], out var bytesWritten, isUnsigned: true, isBigEndian: true);
        //Debug.Assert(written);
        //Debug.Assert(count == bytesWritten);

        //if (isNegative)
        //{
        //    chars[0] = '-';
        //    chars[1] = '0';
        //    chars[2] = 'o';
        //    charsWritten = 3;
        //}
        //else
        //{
        //    chars[0] = '0';
        //    chars[1] = 'o';
        //    charsWritten = 2;
        //}

        //for (int i = 1; i < bytes.Length; i += 3)
        //{
        //    Debug.Assert(i + 3 <= bytes.Length);

        //    // the first byte is for padding
        //    // it should be zero to avoid affecting re-interpretation
        //    var batch = bytes.Slice(i - 1, 4);
        //    batch[0] = 0;
        //    uint batchValue = BinaryPrimitives.ReadUInt32BigEndian(batch);

        //    if (i is 1)
        //    {
        //        // HERE IS WRONG

        //        // ignore leading zero if it is first batch
        //        int startIndex = charsWritten;
        //        do
        //        {
        //            char c = (char)('0' + (batchValue & 0b111));
        //            if (c is not '0' || charsWritten != startIndex)
        //                chars[charsWritten++] = c;
        //            batchValue >>= 3;
        //        } while (batchValue is not 0);
        //        chars[startIndex..charsWritten].Reverse();
        //    }
        //    else
        //    {
        //        var index = charsWritten + 8;
        //        for (int j = 0; j < 8; j++)
        //        {
        //            char c = (char)('0' + (batchValue & 0b111));
        //            chars[--index] = c;
        //            batchValue >>= 3;
        //        }
        //        charsWritten += 8;
        //    }
        //    Console.WriteLine("TEST:" + chars.ToString());
        //}

        //PoolHelper.ReturnIfNonNull(arrayToReturn);
    }

    private static void ToHexString(ref Span<char> chars, BigInteger value, out int charsWritten)
    {
        ToBinOrHexString(ref chars, value, out charsWritten, 'x');
    }
}
