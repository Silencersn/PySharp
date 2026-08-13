using PySharp.Utility;
using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

namespace PySharp.Modules.Builtins;

internal static class PyStrConverter
{
    public struct ConvertErrorInfo
    {
        public ConvertError Error;
        public char Char;
        public int Position;
        public int Length;
    }

    public enum ConvertError : byte
    {
        None = 0,
        DestinationNotEnough,
        EndsWithEscape,
        LowerXSequence,
        LowerUSequence,
        UpperUSequence,
        SurrogatesNotAllowed,
        IllegalUnicodeCharacter,
        InvalidEscapeSequence,
        InvalidOctalEscapeSequence,

        WrongFormat,
    }

    private delegate bool InternalTryFromTo<T>(ReadOnlySpan<char> text, Span<T> destination, out int itemsWritten, out ConvertErrorInfo info) where T : unmanaged;

    private static bool InternalTryFromTextToStringOrBytes<T>(ReadOnlySpan<char> text, Span<T> destination, out int itemsWritten, out ConvertErrorInfo info) where T : unmanaged
    {
        Debug.Assert(typeof(T) == typeof(char) || typeof(T) == typeof(byte));

        info = default;
        var textLength = text.Length;
        var destLength = destination.Length;
        itemsWritten = 0;
        Span<char> cache = stackalloc char[2];

        for (int i = 0; i < textLength; i++)
        {
            switch (text[i])
            {
                case '\\':
                    if (++i >= textLength)
                    {
                        info.Error = ConvertError.EndsWithEscape;
                        info.Position = i - 1;
                        info.Length = 1;
                        return false;
                    }

                    char charToWrite;
                    bool hasSecond = false;
                    char charToWrite2 = default;

                    switch (text[i])
                    {
                        case '\\' or '\'' or '\"':
                            charToWrite = text[i];
                            break;

                        case 'a':
                            charToWrite = '\a';
                            break;

                        case 'b':
                            charToWrite = '\b';
                            break;

                        case 'f':
                            charToWrite = '\f';
                            break;

                        case 'n':
                            charToWrite = '\n';
                            break;

                        case 'r':
                            charToWrite = '\r';
                            break;

                        case 't':
                            charToWrite = '\t';
                            break;

                        case 'v':
                            charToWrite = '\v';
                            break;

                        case '0' or '1' or '2' or '3' or '4' or '5' or '6' or '7':
                            int num = text[i] - '0';
                            if ((i + 1 < textLength) && char.IsBetween(text[i + 1], '0', '7'))
                            {
                                num *= 8;
                                num += text[++i] - '0';

                                if ((i + 1 < textLength) && char.IsBetween(text[i + 1], '0', '7'))
                                {
                                    num *= 8;
                                    num += text[++i] - '0';
                                }
                            }
                            if (num > 0xFF)
                            {
                                // CPython: octal escape > 0o377 keeps its value but emits a SyntaxWarning
                                info.Error = ConvertError.InvalidOctalEscapeSequence;
                                info.Char = (char)num;
                            }
                            charToWrite = (char)num;
                            break;

                        case 'x':
                            if (i + 2 >= textLength)
                            {
                                info.Error = ConvertError.LowerXSequence;
                                info.Position = i - 1;
                                info.Length = 2;
                                IncreaseUntilNonHexDight(ref info, text[(i + 1)..]);
                                return false;
                            }

                            var xSeq = text.Slice(i + 1, 2);
                            if (!AllAsciiHexDigit(xSeq))
                            {
                                info.Error = ConvertError.LowerXSequence;
                                info.Position = i - 1;
                                IncreaseUntilNonHexDight(ref info, xSeq);
                                return false;
                            }

                            charToWrite = (char)byte.Parse(xSeq, NumberStyles.HexNumber);
                            i += 2;
                            break;

                        case 'u':
                            if (typeof(T) == typeof(byte))
                                goto default;

                            if (i + 4 >= textLength)
                            {
                                info.Error = ConvertError.LowerUSequence;
                                info.Position = i - 1;
                                info.Length = 2;
                                IncreaseUntilNonHexDight(ref info, text[(i + 1)..]);
                                return false;
                            }

                            var uSeq4 = text.Slice(i + 1, 4);
                            if (!AllAsciiHexDigit(uSeq4))
                            {
                                info.Error = ConvertError.LowerUSequence;
                                info.Position = i - 1;
                                info.Length = 2;
                                IncreaseUntilNonHexDight(ref info, uSeq4);
                                return false;
                            }
                            // CPython allows lone surrogates in string literals (e.g. '\ud800', len 1);
                            // only encode('utf-8') etc. raises UnicodeEncodeError at runtime.
                            charToWrite = (char)ushort.Parse(uSeq4, NumberStyles.HexNumber);
                            i += 4;
                            break;

                        case 'U':
                            if (typeof(T) == typeof(byte))
                                goto default;

                            if (i + 8 >= textLength)
                            {
                                info.Error = ConvertError.UpperUSequence;
                                info.Position = i - 1;
                                info.Length = 2;
                                IncreaseUntilNonHexDight(ref info, text[(i + 1)..]);
                                return false;
                            }

                            var uSeq8 = text.Slice(i + 1, 8);
                            if (!AllAsciiHexDigit(uSeq8))
                            {
                                info.Error = ConvertError.UpperUSequence;
                                info.Position = i - 1;
                                info.Length = 2;
                                IncreaseUntilNonHexDight(ref info, uSeq8);
                                return false;
                            }
                            var value = uint.Parse(uSeq8, NumberStyles.HexNumber);

                            if (value is >= 0xD800 and <= 0xDFFF)
                            {
                                // CPython allows lone surrogates via \U escape (e.g. '\U0000d800', len 1)
                                charToWrite = (char)value;
                            }
                            else if (!Rune.TryCreate(value, out var rune))
                            {
                                info.Error = ConvertError.IllegalUnicodeCharacter;
                                info.Position = i - 1;
                                info.Length = 10;
                                return false;
                            }
                            else if (rune.Utf16SequenceLength is 2)
                            {
                                hasSecond = true;
                                rune.EncodeToUtf16(cache);
                                charToWrite = cache[0];
                                charToWrite2 = cache[1];
                            }
                            else
                            {
                                Debug.Assert(rune.Utf16SequenceLength is 1);
                                // Rune values are never surrogates, so no surrogate check is needed here
                                charToWrite = (char)rune.Value;
                            }
                            i += 8;
                            break;

                        //case 'N':
                        //    throw new NotSupportedException();

                        default:
                            info.Error = ConvertError.InvalidEscapeSequence;
                            info.Char = text[i];
                            charToWrite = '\\';
                            hasSecond = true;
                            charToWrite2 = text[i];
                            break;
                    }

                    if (!TryWrite(destination, ref itemsWritten, ref info, charToWrite))
                    {
                        info.Error = ConvertError.DestinationNotEnough;
                        return false;
                    }
                    if (hasSecond && !TryWrite(destination, ref itemsWritten, ref info, charToWrite2))
                    {
                        info.Error = ConvertError.DestinationNotEnough;
                        return false;
                    }
                    break;

                default:
                    // A bare non-ASCII character cannot be represented in a bytes
                    // literal (CPython would UTF-8-encode it; PySharp keeps the
                    // existing error behavior for that case).
                    if (typeof(T) == typeof(byte) && text[i] > 0xFF)
                    {
                        info.Error = ConvertError.IllegalUnicodeCharacter;
                        info.Position = i;
                        info.Length = 1;
                        return false;
                    }

                    if (!TryWrite(destination, ref itemsWritten, ref info, text[i]))
                    {
                        info.Error = ConvertError.DestinationNotEnough;
                        return false;
                    }
                    break;
            }
        }

        return true;

        static bool AllAsciiHexDigit(ReadOnlySpan<char> chars)
        {
            foreach (var c in chars)
            {
                if (!char.IsAsciiHexDigit(c))
                    return false;
            }
            return true;
        }

        static void IncreaseUntilNonHexDight(ref ConvertErrorInfo info, ReadOnlySpan<char> chars)
        {
            foreach (var c in chars)
            {
                if (char.IsAsciiHexDigit(c))
                    info.Length++;
                else
                    break;
            }
        }

        static bool TryWrite(Span<T> destination, ref int written, ref ConvertErrorInfo info, char value)
        {
            if (written >= destination.Length)
            {
                info.Error = ConvertError.DestinationNotEnough;
                return false;
            }

            if (typeof(T) == typeof(char))
                destination.Cast<T, char>()[written++] = value;
            else
                destination.Cast<T, byte>()[written++] = (byte)value;
            return true;
        }
    }

    private static bool InternalTryFromLiteralToStringOrBytes<T>(ReadOnlySpan<char> literal, Span<T> destination, out int itemsWritten, out ConvertErrorInfo info) where T : unmanaged
    {
        Debug.Assert(typeof(T) == typeof(char) || typeof(T) == typeof(byte));

        itemsWritten = 0;
        info = default;
        info.Error = ConvertError.WrongFormat;

        if (literal.Length < 2)
            return false;

        var wrapper = literal[^1];
        if (wrapper is not ('\'' or '\"'))
            return false;

        var startIndex = literal.IndexOf(wrapper);
        Debug.Assert(startIndex is not -1);
        if (startIndex == literal.Length - 1)
            return false;

        var prefix = literal[..startIndex];
        bool isRaw;
        if (prefix.Length is 0)
        {
            isRaw = false;
        }
        else if (prefix.Length is 1)
        {
            if (prefix[0] is 'r' or 'R')
                isRaw = true;
            else if (prefix[0] is 'u' or 'U' or 'b' or 'B')
                isRaw = false;
            else
            {
                return false;
            }
        }
        else if (prefix.Length is 2)
        {
            if (prefix.ContainsAny('r', 'R') && prefix.ContainsAny('b', 'B'))
                isRaw = true;
            else
            {
                return false;
            }
        }
        else
        {
            return false;
        }

        ReadOnlySpan<char> text;
        ReadOnlySpan<char> triple = [wrapper, wrapper, wrapper];
        if (literal.EndsWith(triple))
        {
            if (!literal[startIndex..].StartsWith(triple))
                return false;
            text = literal[(startIndex + 3)..^3];
        }
        else
        {
            text = literal[(startIndex + 1)..^1];
        }

        info.Error = ConvertError.None;

        if (isRaw)
        {
            if (text.Length > destination.Length)
            {
                info.Error = ConvertError.DestinationNotEnough;
                return false;
            }

            if (typeof(T) == typeof(char))
            {
                text.CopyTo(destination.Cast<T, char>());
            }
            else if (typeof(T) == typeof(byte))
            {
                for (int i = 0; i < text.Length; i++)
                {
                    var c = text[i];
                    if (c > 0xFF)
                    {
                        info.Error = ConvertError.IllegalUnicodeCharacter;
                        info.Position = startIndex + 1 + i;
                        info.Length = 1;
                        return false;
                    }
                    destination[itemsWritten++] = Unsafe.As<char, T>(ref c);
                }
            }
            else
            {
                throw new UnreachableException();
            }

            itemsWritten = text.Length;
            return true;
        }

        return InternalTryFromTextToStringOrBytes(text, destination, out itemsWritten, out info);
    }

    private static bool InternalTryToStringOrBytes<T>(InternalTryFromTo<T> tryFromTo, ReadOnlySpan<char> text, [NotNullWhen(true)] out object? obj, out ConvertErrorInfo info) where T : unmanaged
    {
        Debug.Assert(typeof(T) == typeof(char) || typeof(T) == typeof(byte));

        const int MaxStackLimit = 1024;
        T[]? rentedArray = null;

        Span<T> span = text.Length <= MaxStackLimit ? stackalloc T[text.Length] : (rentedArray = ArrayPool<T>.Shared.Rent(text.Length));
        if (!tryFromTo(text, span, out var itemsWritten, out info))
        {
            Debug.Assert(info.Error is not ConvertError.DestinationNotEnough);
            obj = null;
            if (rentedArray is not null)
                ArrayPool<T>.Shared.Return(rentedArray);
            return false;
        }

        span = span[..itemsWritten];
        if (typeof(T) == typeof(char))
            obj = span.ToString();
        else
            obj = span.ToArray();

        if (rentedArray is not null)
            ArrayPool<T>.Shared.Return(rentedArray);
        return true;
    }

    public static bool TryFromTextToString(ReadOnlySpan<char> text, [NotNullWhen(true)] out string? str, out ConvertErrorInfo info)
    {
        str = null;
        if (!InternalTryToStringOrBytes<char>(InternalTryFromTextToStringOrBytes, text, out var obj, out info))
            return false;
        str = (string)obj;
        return true;
    }

    public static bool TryFromLiteralToString(ReadOnlySpan<char> literal, [NotNullWhen(true)] out string? str, out ConvertErrorInfo info)
    {
        str = null;
        if (!InternalTryToStringOrBytes<char>(InternalTryFromLiteralToStringOrBytes, literal, out var obj, out info))
            return false;
        str = (string)obj;
        return true;
    }

    public static bool TryFromLiteralToBytes(ReadOnlySpan<char> literal, [NotNullWhen(true)] out byte[]? bytes, out ConvertErrorInfo info)
    {
        bytes = null;
        if (!InternalTryToStringOrBytes<byte>(InternalTryFromLiteralToStringOrBytes, literal, out var obj, out info))
            return false;
        bytes = (byte[])obj;
        return true;
    }

    public static string FromStringToLiteral(ReadOnlySpan<char> str)
    {
        return UnicodeRepr(str);
    }

    private static void ScanUnicodeForRepr(ReadOnlySpan<char> str, out int osize, out char quote)
    {
        int squote = 0;
        int dquote = 0;
        osize = 0;

        var unicode = str.EnumerateRunes();
        foreach (var rune in unicode)
        {
            int ch = rune.Value;
            int incr = 1;
            switch (ch)
            {
                case '\'':
                    squote++;
                    break;

                case '"':
                    dquote++;
                    break;

                case '\\' or '\t' or '\r' or '\n':
                    incr = 2;
                    break;

                default:
                    if (ch < ' ' || ch is 0x7F)
                        incr = 4; // \xHH
                    else if (ch < 0x7F)
                        incr = 1;
                    else if (ch < 0x100)
                        incr = 4; // \xHH
                    else if (ch < 0x10000)
                        incr = 6; // \uHHHH
                    else
                        incr = 10; // \uHHHHHHHH
                    break;
            }

            osize += incr;
        }

        quote = '\'';
        if (squote > 0)
        {
            if (dquote > 0)
                // both squote and dquote present
                // use squote, and escape them
                osize += squote;
            else
                quote = '"';
        }

        // quotes
        osize += 2;
    }
    private static string UnicodeRepr(ReadOnlySpan<char> str)
    {
        ScanUnicodeForRepr(str, out var osize, out var quote);

        var builder = new StringBuilder(osize);
        builder.Append(quote);

        var unicode = str.EnumerateRunes();
        foreach (var rune in unicode)
        {
            int ch = rune.Value;
            switch (ch)
            {
                case '\'':
                    if (quote is '\'')
                        builder.Append("\\'");
                    else
                        builder.Append('\'');
                    break;

                case '"':
                    // if str contains dquote, quote must be squote
                    builder.Append('"');
                    break;

                case '\\':
                    builder.Append("\\\\");
                    break;

                case '\t':
                    builder.Append("\\t");
                    break;

                case '\r':
                    builder.Append("\\r");
                    break;

                case '\n':
                    builder.Append("\\n");
                    break;

                default:
                    if (ch < ' ' || ch is 0x7F)
                        builder.AppendFormat("\\x{0:x2}", ch);
                    else if (ch < 0x7F)
                        builder.Append((char)ch);
                    else if (IsPrintable(rune))
                        builder.Append(rune.ToString());
                    else if (ch < 0x100)
                        builder.AppendFormat("\\x{0:x2}", ch);
                    else if (ch < 0x10000)
                        builder.AppendFormat("\\u{0:x4}", ch);
                    else
                        builder.AppendFormat("\\U{0:x8}", ch);
                    break;
            }
        }

        builder.Append(quote);

        return builder.ToString();
    }

    private static bool IsPrintable(Rune rune)
    {
        var c = rune.Value;

        if (0x1F < c && c < 0x7F)
            return true;

        if (c <= 0xA0 || c is 0xAD)
            return false;

        if (c <= 0xFF)
            return true;

        return Rune.GetUnicodeCategory(rune) is not
            (
                UnicodeCategory.Control or
                UnicodeCategory.Format or
                UnicodeCategory.Surrogate or
                UnicodeCategory.OtherNotAssigned or
                UnicodeCategory.LineSeparator or
                UnicodeCategory.ParagraphSeparator or
                UnicodeCategory.SpaceSeparator
            );
    }

    public static string FromSourceToLiteral(ReadOnlySpan<char> str, bool isRaw, StringBuilder builder)
    {
        builder.Clear();
        builder.Append(str);

        // all the \r\n or \r should be \n
        builder.Replace("\r\n", "\n");
        builder.Replace('\r', '\n');

        if (!isRaw)
            // explicit line joining
            builder.Replace("\\\n", string.Empty);

        return builder.ToString();
    }
}
