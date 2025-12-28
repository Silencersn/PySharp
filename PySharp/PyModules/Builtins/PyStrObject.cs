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
}

public sealed class PyStrObjectType : PyTypeObject<PyStrObjectType, PyStrObject>
{
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
    protected internal override PyResult Repr(PyCallContext context, PyStrObject self)
    {
        return PyStrObject.FromString(PyStrConverter.FromStringToLiteral(self.Value));
    }
    protected internal override PyResult Str(PyCallContext context, PyStrObject self)
    {
        return self;
    }

    protected internal override PyResult Hash(PyCallContext context, PyStrObject self)
    {
        return PyIntObject.FromInteger(self.Value.GetHashCode());
    }
    protected internal override PyResult Bool(PyCallContext context, PyStrObject self)
    {
        return PyBoolObject.FromBoolean(self.Value.Length > 0);
    }
    protected internal override PyResult Len(PyCallContext context, PyStrObject self)
    {
        return PyIntObject.FromInteger(self.PyLength);
    }
    protected internal override PyResult Iter(PyCallContext context, PyStrObject self)
    {
        return new PyStrIteratorObject(self.Value);
    }
    protected internal override PyResult GetItem(PyCallContext context, PyStrObject self, PyObject item)
    {
        if (!PySpecialMethods.TryGetIndex(context, item, out var indexObj, out var result))
            return result;
        var index = indexObj.Int32Value;
        index = Utils.MapIndex(index, self.PyLength);
        if (index < 0 || index >= self.PyLength)
            return PyResult.RaiseIndexError("string index out of range");
        return PyStrObject.FromRune(self.Value.EnumerateRunes().ElementAt(index));
    }
    protected internal override PyResult Add(PyCallContext context, PyStrObject self, PyObject other)
    {
        if (other is PyStrObject strObj)
            return PyStrObject.FromString(self.Value + strObj.Value);
        return PyResult.RaiseTypeError($"can only concatenate str (not \"{other.PyType.Name}\") to str");
    }
    protected internal override PyResult Eq(PyCallContext context, PyStrObject self, PyObject other)
    {
        if (other is PyStrObject strObj)
            return PyBoolObject.FromBoolean(self.Value == strObj.Value);
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal override PyResult Lt(PyCallContext context, PyStrObject self, PyObject other)
    {
        if (other is not PyStrObject strObj)
            return PyNotImplementedObject.NotImplemented;
        return PyBoolObject.FromBoolean(self.Value.CompareTo(strObj.Value) < 0);
    }
    protected internal override PyResult Mul(PyCallContext context, PyStrObject self, PyObject other)
    {
        if (!PySpecialMethods.TryGetIndex(context, other, out var repeatCount, out var result))
            return PyNotImplementedObject.NotImplemented;
        return PyStrObject.FromString(string.Concat(Enumerable.Repeat(self.Value, repeatCount.Int32Value)));
    }
    protected internal override PyResult RMul(PyCallContext context, PyStrObject self, PyObject other)
    {
        return Mul(context, self, other);
    }
    protected internal override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        if (!PyArgsValidator.ValidateSinglePositionalArg(args, kwargs, out var err))
            return err.Value;
        return PySpecialMethods.GetStr(context, args[0]);
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

        info.Error = ConvertError.None;
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
        var builder = new StringBuilder();

        var wrapper = '\'';
        if (str.Contains('\'') && !str.Contains('"'))
            wrapper = '"';

        builder.Append(wrapper);
        for (int i = 0; i < str.Length; i++)
        {
            var c = str[i];
            builder.Append(c switch
            {
                '\\' => "\\\\",
                '\'' => wrapper is '\'' ? "\\'" : "'",
                '\a' => "\\a",
                '\b' => "\\b",
                '\f' => "\\f",
                '\n' => "\\n",
                '\r' => "\\r",
                '\t' => "\\t",
                '\v' => "\\v",
                _ => char.IsControl(c) ? $"\\x{(int)c:x2}" : c.ToString()
            });
        }
        builder.Append(wrapper);

        return builder.ToString();
    }
}