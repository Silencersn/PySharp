using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;
using PySharp.PyRuntime.PyAttributes;
using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

namespace PySharp.PyModules.Builtins;

public partial class PyStrObject : PyObject
{
    public string Value { get; }
    public int PyLength
    {
        get
        {
            if (field is not -1)
                return field;
            return field = Value.EnumerateRunes().Count();
        }
    }
    public static PyStrObject Empty { get; } = new PyStrObject(string.Empty);
    public override PyTypeObject DefaultPyType => PyStrObjectType.Shared;
    private PyStrObject(string value)
    {
        Value = value;
        PyLength = -1;
    }
    internal static PyStrObject FromLiteral(ReadOnlySpan<char> literal)
    {
        if (!PyStrConverter.TryFromLiteralToString(literal, out var str, out _))
            throw new ArgumentException($"failed to parse {literal}");
        return FromString(str);
    }
    internal static PyStrObject FromLiteralContent(ReadOnlySpan<char> text)
    {
        if (!PyStrConverter.TryFromTextToString(text, out var str, out _))
            throw new ArgumentException($"failed to parse {text}");
        return FromString(str);
    }
    public static PyStrObject FromString(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new PyStrObject(value);
    }
    private static readonly ConcurrentDictionary<Rune, PyStrObject> _runeToPyStr = [];
    public static PyStrObject FromRune(Rune value)
    {
        return _runeToPyStr.GetOrAdd(value, static rune => FromString(rune.ToString()));
    }

    public Rune PyCharAt(int index)
    {
        return Rune.GetRuneAt(Value, index);
    }
}

public sealed class PyStrObjectType : PyTypeObject<PyStrObjectType, PyStrObject>
{
    public override string Module => "builtins";
    public override string Name => "str";
    public PyStrObjectType()
    {
        AppendMethodDescriptor("join", Join);
    }
    [PyFunctionArgsDef("iterable", "/")]
    internal PyResult Join(PyCallContext context, PyStrObject self, PyArguments arguments)
    {
        return self.PyJoin(context, arguments[0]);
    }
    protected override PyResult Repr(PyCallContext context, PyStrObject self)
    {
        return PyStrObject.FromString(PyStrConverter.FromStringToLiteral(self.Value));
    }
    protected override PyResult Str(PyCallContext context, PyStrObject self)
    {
        return self;
    }

    protected override PyResult Hash(PyCallContext context, PyStrObject self)
    {
        return PyIntObject.FromInteger(self.Value.GetHashCode());
    }
    protected override PyResult Bool(PyCallContext context, PyStrObject self)
    {
        return PyBoolObject.FromBoolean(self.Value.Length > 0);
    }
    protected override PyResult Len(PyCallContext context, PyStrObject self)
    {
        return PyIntObject.FromInteger(self.PyLength);
    }
    protected override PyResult Iter(PyCallContext context, PyStrObject self)
    {
        return new PyStrIteratorObject(self.Value);
    }
    protected override PyResult GetItem(PyCallContext context, PyStrObject self, PyObject item)
    {
        var result = PySpecialMethods.Index(context, item);
        if (result.IsError)
            return result;
        var index = result.Value.Int32Value;
        index = Utils.MapIndex(index, self.PyLength);
        if (index < 0 || index >= self.PyLength)
            return PyResult.RaiseIndexError("string index out of range");
        return PyStrObject.FromRune(self.Value.EnumerateRunes().ElementAt(index));
    }
    protected override PyResult Add(PyCallContext context, PyStrObject self, PyObject other)
    {
        if (other is PyStrObject strObj)
            return PyStrObject.FromString(self.Value + strObj.Value);
        return PyResult.RaiseTypeError($"can only concatenate str (not \"{other.PyType.Name}\") to str");
    }
    protected override PyResult Eq(PyCallContext context, PyStrObject self, PyObject other)
    {
        if (other is PyStrObject strObj)
            return PyBoolObject.FromBoolean(self.Value == strObj.Value);
        return PyNotImplementedObject.NotImplemented;
    }
    protected override PyResult Lt(PyCallContext context, PyStrObject self, PyObject other)
    {
        if (other is not PyStrObject strObj)
            return PyNotImplementedObject.NotImplemented;
        return PyBoolObject.FromBoolean(self.Value.CompareTo(strObj.Value) < 0);
    }
    protected override PyResult Mul(PyCallContext context, PyStrObject self, PyObject other)
    {
        var result = PySpecialMethods.Index(context, other);
        if (result.IsError)
            return result;
        return PyStrObject.FromString(string.Concat(Enumerable.Repeat(self.Value, result.Value.Int32Value)));
    }
    protected override PyResult RMul(PyCallContext context, PyStrObject self, PyObject other)
    {
        return Mul(context, self, other);
    }
    protected override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        if (!PyArgsValidator.ValidateSinglePositionalArg(args, kwargs, out var err))
            return err.Value;
        return PySpecialMethods.Str(context, args[0]);
    }
}

public static class PyStrConverter
{
    public struct ConvertErrorInfo
    {
        public ConvertError Error;
        public char Char;
        public int Position;
        public int Length;
    }

    public const int ExtraInfoOffset = 16;

    public enum ConvertError : uint
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

        WrongFormat,
    }

    public static bool TryFromTextToString(ReadOnlySpan<char> text, Span<char> destination, out int charsWritten, out ConvertErrorInfo info)
    {
        info = default;
        var textLength = text.Length;
        var destLength = destination.Length;
        charsWritten = 0;
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

                                if (char.IsBetween(text[i - 1], '0', '3') && (i + 1 < textLength) && char.IsBetween(text[i + 1], '0', '7'))
                                {
                                    num *= 8;
                                    num += text[++i] - '0';
                                }
                            }
                            Debug.Assert(num >= byte.MinValue && num <= byte.MaxValue);
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
                            charToWrite = (char)ushort.Parse(uSeq4, NumberStyles.HexNumber);
                            if (char.IsSurrogate(charToWrite))
                            {
                                info.Error = ConvertError.SurrogatesNotAllowed;
                                info.Char = charToWrite;
                                info.Position = i - 1;
                                return false;
                            }
                            i += 4;
                            break;

                        case 'U':
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

                            if (!Rune.TryCreate(value, out var rune))
                            {
                                info.Error = ConvertError.IllegalUnicodeCharacter;
                                info.Position = i - 1;
                                info.Length = 10;
                                return false;
                            }

                            if (rune.Utf16SequenceLength is 2)
                            {
                                hasSecond = true;
                                rune.EncodeToUtf16(cache);
                                charToWrite = cache[0];
                                charToWrite2 = cache[1];
                            }
                            else
                            {
                                Debug.Assert(rune.Utf16SequenceLength is 1);
                                charToWrite = (char)rune.Value;

                                if (char.IsSurrogate(charToWrite))
                                {
                                    info.Error = ConvertError.SurrogatesNotAllowed;
                                    info.Char = charToWrite;
                                    info.Position = i - 1;
                                    return false;
                                }
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

                    if (charsWritten >= destination.Length)
                    {
                        info.Error = ConvertError.DestinationNotEnough;
                        return false;
                    }
                    destination[charsWritten++] = charToWrite;
                    if (hasSecond)
                    {
                        if (charsWritten >= destination.Length)
                        {
                            info.Error = ConvertError.DestinationNotEnough;
                            return false;
                        }
                        destination[charsWritten++] = charToWrite2;
                    }
                    break;

                default:
                    if (charsWritten >= destination.Length)
                    {
                        info.Error = ConvertError.DestinationNotEnough;
                        return false;
                    }
                    destination[charsWritten++] = text[i];
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
    }

    public static bool TryFromTextToString(ReadOnlySpan<char> text, [NotNullWhen(true)] out string? str, out ConvertErrorInfo info)
    {
        const int MaxStackLimit = 1024;
        char[]? rentedArray = null;

        Span<char> chars = text.Length <= MaxStackLimit ? stackalloc char[text.Length] : (rentedArray = ArrayPool<char>.Shared.Rent(text.Length));
        if (!TryFromTextToString(text, chars, out var charsWritten, out info))
        {
            Debug.Assert(info.Error is not ConvertError.DestinationNotEnough);
            str = null;
            if (rentedArray is not null)
                ArrayPool<char>.Shared.Return(rentedArray);
            return false;
        }

        str = chars[..charsWritten].ToString();
        if (rentedArray is not null)
            ArrayPool<char>.Shared.Return(rentedArray);
        return true;
    }

    public static bool TryFromLiteralToString(ReadOnlySpan<char> literal, Span<char> destination, out int charsWritten, out ConvertErrorInfo info)
    {
        charsWritten = 0;
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
            {
                isRaw = true;
            }
            else if (prefix[0] is 'u' or 'U')
            {
                isRaw = false;
            }
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

            text.CopyTo(destination);
            charsWritten = text.Length;
            return true;
        }

        return TryFromTextToString(text, destination, out charsWritten, out info);
    }

    public static bool TryFromLiteralToString(ReadOnlySpan<char> literal, [NotNullWhen(true)] out string? str, out ConvertErrorInfo info)
    {
        const int MaxStackLimit = 1024;
        char[]? rentedArray = null;

        Span<char> chars = literal.Length <= MaxStackLimit ? stackalloc char[literal.Length] : (rentedArray = ArrayPool<char>.Shared.Rent(literal.Length));
        if (!TryFromLiteralToString(literal, chars, out var charsWritten, out info))
        {
            Debug.Assert(info.Error is not ConvertError.DestinationNotEnough);
            str = null;
            if (rentedArray is not null)
                ArrayPool<char>.Shared.Return(rentedArray);
            return false;
        }

        str = chars[..charsWritten].ToString();
        if (rentedArray is not null)
            ArrayPool<char>.Shared.Return(rentedArray);
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
}