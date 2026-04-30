using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;
using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

namespace PySharp.Modules.Builtins;

public partial class PyStrObject : PyObject
{
    private const int CharPoolSize = 256;
    private static readonly PyStrObject[] _charPool;

    static PyStrObject()
    {
        _charPool = new PyStrObject[CharPoolSize];
        for (int i = 0; i < CharPoolSize; i++)
            _charPool[i] = new PyStrObject(((char)i).ToString());
    }

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
        if (value.Length is 0)
            return Empty;
        if (value.Length is 1 && value[0] < CharPoolSize)
            return _charPool[value[0]];
        return new PyStrObject(value);
    }
    public static PyStrObject FromRune(Rune value)
    {
        if (value.Value < CharPoolSize)
            return _charPool[value.Value];
        return new PyStrObject(value.ToString());
    }

    internal string Repr()
    {
        return PyStrConverter.FromStringToLiteral(Value);
    }

    internal Rune PyCharAt(int index)
    {
        foreach (var rune in Value.EnumerateRunes())
        {
            if (index-- is 0)
                return rune;
        }
        throw new UnreachableException();
    }
}

[PyType("str")]
public sealed partial class PyStrObjectType : PyTypeObject<PyStrObject>
{
    [PyMethod("join")]
    [PyFunctionParameters("iterable", "/")]
    private static PyResult Join(PyCallContext context, PyStrObject self, PyArguments arguments)
    {
        return self.PyJoin(context, arguments[0]);
    }

    [PyMethod("upper")]
    [AIGenerated]
    [PyFunctionParameters]
    private static PyResult Upper(PyCallContext context, PyStrObject self, PyArguments arguments)
    {
        return PyStrObject.FromString(self.Value.ToUpperInvariant());
    }

    [PyMethod("lower")]
    [AIGenerated]
    [PyFunctionParameters]
    private static PyResult Lower(PyCallContext context, PyStrObject self, PyArguments arguments)
    {
        return PyStrObject.FromString(self.Value.ToLowerInvariant());
    }

    [PyMethod("strip")]
    [AIGenerated]
    [PyFunctionParameters("chars=None", "/")]
    private static PyResult Strip(PyCallContext context, PyStrObject self, PyArguments arguments)
    {
        if (arguments[0] is PyNoneObject)
            return PyStrObject.FromString(self.Value.Trim());
        if (arguments[0] is PyStrObject charsStr)
            return PyStrObject.FromString(self.Value.Trim(charsStr.Value.ToCharArray()));
        return PyResult.TypeError($"strip arg must be None or str");
    }

    [PyMethod("lstrip")]
    [AIGenerated]
    [PyFunctionParameters("chars=None", "/")]
    private static PyResult LStrip(PyCallContext context, PyStrObject self, PyArguments arguments)
    {
        if (arguments[0] is PyNoneObject)
            return PyStrObject.FromString(self.Value.TrimStart());
        if (arguments[0] is PyStrObject charsStr)
            return PyStrObject.FromString(self.Value.TrimStart(charsStr.Value.ToCharArray()));
        return PyResult.TypeError($"lstrip arg must be None or str");
    }

    [PyMethod("rstrip")]
    [AIGenerated]
    [PyFunctionParameters("chars=None", "/")]
    private static PyResult RStrip(PyCallContext context, PyStrObject self, PyArguments arguments)
    {
        if (arguments[0] is PyNoneObject)
            return PyStrObject.FromString(self.Value.TrimEnd());
        if (arguments[0] is PyStrObject charsStr)
            return PyStrObject.FromString(self.Value.TrimEnd(charsStr.Value.ToCharArray()));
        return PyResult.TypeError($"rstrip arg must be None or str");
    }

    [PyMethod("startswith")]
    [AIGenerated]
    [PyFunctionParameters("prefix", "/")]
    private static PyResult StartsWith(PyCallContext context, PyStrObject self, PyArguments arguments)
    {
        if (arguments[0] is PyStrObject prefixStr)
            return PyBoolObject.FromBoolean(self.Value.StartsWith(prefixStr.Value));
        return PyResult.TypeError($"startswith first arg must be str");
    }

    [PyMethod("endswith")]
    [AIGenerated]
    [PyFunctionParameters("suffix", "/")]
    private static PyResult EndsWith(PyCallContext context, PyStrObject self, PyArguments arguments)
    {
        if (arguments[0] is PyStrObject suffixStr)
            return PyBoolObject.FromBoolean(self.Value.EndsWith(suffixStr.Value));
        return PyResult.TypeError($"endswith first arg must be str");
    }

    [PyMethod("replace")]
    [AIGenerated]
    [PyFunctionParameters("old", "new", "/", "count=-1")]
    private static PyResult Replace(PyCallContext context, PyStrObject self, PyArguments arguments)
    {
        if (arguments[0] is not PyStrObject oldStr || arguments[1] is not PyStrObject newStr)
            return PyResult.TypeError($"replace args must be str");

        if (arguments[2] is not PyIntObject countArg)
            return PyResult.TypeError($"replace count must be int");

        int count = countArg.Int32Value;
        if (count < 0)
            return PyStrObject.FromString(self.Value.Replace(oldStr.Value, newStr.Value));

        if (string.IsNullOrEmpty(oldStr.Value))
        {
            var sb = new StringBuilder();
            sb.Append(newStr.Value);
            int charsAppended = 0;
            for (int i = 0; i < self.Value.Length && count > 0; i++, count--)
            {
                sb.Append(self.Value[i]);
                sb.Append(newStr.Value);
                charsAppended++;
            }
            if (count == 0)
            {
                sb.Append(self.Value.AsSpan(charsAppended));
            }
            return PyStrObject.FromString(sb.ToString());
        }

        var resObj = self.Value;
        int startIndex = 0;
        while (count > 0)
        {
            int idx = resObj.IndexOf(oldStr.Value, startIndex);
            if (idx == -1) break;
            resObj = resObj.Remove(idx, oldStr.Value.Length).Insert(idx, newStr.Value);
            startIndex = idx + newStr.Value.Length;
            count--;
        }
        return PyStrObject.FromString(resObj);
    }

    [PyMethod("split")]
    [AIGenerated]
    [PyFunctionParameters("sep=None", "maxsplit=-1")]
    private static PyResult Split(PyCallContext context, PyStrObject self, PyArguments arguments)
    {
        var sepObj = arguments[0];
        var maxsplitObj = arguments[1];

        int maxsplit = -1;
        if (maxsplitObj is PyIntObject maxsplitInt) maxsplit = maxsplitInt.Int32Value;
        
        string[] parts;
        if (sepObj is PyNoneObject)
        {
            if (maxsplit < 0) parts = self.Value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            else parts = self.Value.Split((char[]?)null, maxsplit + 1, StringSplitOptions.RemoveEmptyEntries);
        }
        else if (sepObj is PyStrObject sepStr)
        {
            if (string.IsNullOrEmpty(sepStr.Value))
                return PyResult.ValueError("empty separator");

            if (maxsplit < 0) parts = self.Value.Split([sepStr.Value], StringSplitOptions.None);
            else parts = self.Value.Split([sepStr.Value], maxsplit + 1, StringSplitOptions.None);
        }
        else
        {
            return PyResult.TypeError("must be str or None");
        }

        var list = new List<PyObject>(parts.Length);
        foreach (var p in parts)
            list.Add(PyStrObject.FromString(p));
        
        return PyListObject.CreateList(list);
    }

    [PyMethod("find")]
    [AIGenerated]
    [PyFunctionParameters("sub", "/")] // simplification, skip start/end support for now
    private static PyResult Find(PyCallContext context, PyStrObject self, PyArguments arguments)
    {
        if (arguments[0] is PyStrObject subStr)
            return PyIntObject.FromInteger(self.Value.IndexOf(subStr.Value));
        return PyResult.TypeError($"find arg must be str");
    }

    [PyMethod("rfind")]
    [AIGenerated]
    [PyFunctionParameters("sub", "/")]
    private static PyResult RFind(PyCallContext context, PyStrObject self, PyArguments arguments)
    {
        if (arguments[0] is PyStrObject subStr)
            return PyIntObject.FromInteger(self.Value.LastIndexOf(subStr.Value));
        return PyResult.TypeError($"rfind arg must be str");
    }

    [PyMethod("capitalize")]
    [AIGenerated]
    [PyFunctionParameters]
    private static PyResult Capitalize(PyCallContext context, PyStrObject self, PyArguments arguments)
    {
        if (self.Value.Length == 0) return self;
        var first = char.ToUpperInvariant(self.Value[0]);
        if (self.Value.Length == 1) return PyStrObject.FromString(first.ToString());
        return PyStrObject.FromString(first + self.Value[1..].ToLowerInvariant());
    }

    [PyMethod("casefold")]
    [AIGenerated]
    [PyFunctionParameters]
    private static PyResult Casefold(PyCallContext context, PyStrObject self, PyArguments arguments)
    {
        return PyStrObject.FromString(self.Value.ToLowerInvariant()); // C# doesn't have a direct casefold, toLowerInvariant works mostly identical for standard cases.
    }

    [PyMethod("center")]
    [AIGenerated]
    [PyFunctionParameters("width", "fillchar=' '", "/")]
    private static PyResult Center(PyCallContext context, PyStrObject self, PyArguments arguments)
    {
        if (arguments[0] is not PyIntObject widthObj)
            return PyResult.TypeError("width must be int");
        
        string fillchar = " ";
        if (arguments[1] is PyStrObject fillStr)
        {
            if (fillStr.Value.Length != 1) return PyResult.TypeError("fillchar must be a string of length 1");
            fillchar = fillStr.Value;
        }
        else if (arguments[1] is not PyNoneObject)
        {
            return PyResult.TypeError("fillchar must be a character");
        }

        int width = widthObj.Int32Value;
        if (width <= self.Value.Length) return self;

        int padLeft = (width - self.Value.Length) / 2;
        int padRight = width - self.Value.Length - padLeft;

        var sb = new StringBuilder(width);
        sb.Append(fillchar[0], padLeft);
        sb.Append(self.Value);
        sb.Append(fillchar[0], padRight);
        return PyStrObject.FromString(sb.ToString());
    }

    [PyMethod("count")]
    [AIGenerated]
    [PyFunctionParameters("sub", "/")]
    private static PyResult Count(PyCallContext context, PyStrObject self, PyArguments arguments)
    {
        if (arguments[0] is not PyStrObject subStr) return PyResult.TypeError("count arg must be str");
        if (string.IsNullOrEmpty(subStr.Value)) return PyIntObject.FromInteger(self.Value.Length + 1);

        int count = 0;
        int index = 0;
        while ((index = self.Value.IndexOf(subStr.Value, index)) != -1)
        {
            count++;
            index += subStr.Value.Length;
        }
        return PyIntObject.FromInteger(count);
    }

    [PyMethod("isalnum")]
    [AIGenerated]
    [PyFunctionParameters]
    private static PyResult IsAlnum(PyCallContext context, PyStrObject self, PyArguments arguments)
    {
        if (self.Value.Length == 0) return PyBoolObject.False;
        foreach (char c in self.Value)
            if (!char.IsLetterOrDigit(c)) return PyBoolObject.False;
        return PyBoolObject.True;
    }

    [PyMethod("isalpha")]
    [AIGenerated]
    [PyFunctionParameters]
    private static PyResult IsAlpha(PyCallContext context, PyStrObject self, PyArguments arguments)
    {
        if (self.Value.Length == 0) return PyBoolObject.False;
        foreach (char c in self.Value)
            if (!char.IsLetter(c)) return PyBoolObject.False;
        return PyBoolObject.True;
    }

    [PyMethod("isdigit")]
    [AIGenerated]
    [PyFunctionParameters]
    private static PyResult IsDigit(PyCallContext context, PyStrObject self, PyArguments arguments)
    {
        if (self.Value.Length == 0) return PyBoolObject.False;
        foreach (char c in self.Value)
            if (!char.IsDigit(c)) return PyBoolObject.False;
        return PyBoolObject.True;
    }

    [PyMethod("islower")]
    [AIGenerated]
    [PyFunctionParameters]
    private static PyResult IsLower(PyCallContext context, PyStrObject self, PyArguments arguments)
    {
        if (self.Value.Length == 0) return PyBoolObject.False;
        bool hasCased = false;
        foreach (char c in self.Value)
        {
            if (char.IsUpper(c)) return PyBoolObject.False;
            if (char.IsLower(c)) hasCased = true;
        }
        return PyBoolObject.FromBoolean(hasCased);
    }

    [PyMethod("isupper")]
    [AIGenerated]
    [PyFunctionParameters]
    private static PyResult IsUpper(PyCallContext context, PyStrObject self, PyArguments arguments)
    {
        if (self.Value.Length == 0) return PyBoolObject.False;
        bool hasCased = false;
        foreach (char c in self.Value)
        {
            if (char.IsLower(c)) return PyBoolObject.False;
            if (char.IsUpper(c)) hasCased = true;
        }
        return PyBoolObject.FromBoolean(hasCased);
    }

    [PyMethod("title")]
    [AIGenerated]
    [PyFunctionParameters]
    private static PyResult Title(PyCallContext context, PyStrObject self, PyArguments arguments)
    {
        if (self.Value.Length == 0) return self;
        var sb = new StringBuilder(self.Value.Length);
        bool newWord = true;
        foreach (char c in self.Value)
        {
            if (char.IsLetter(c))
            {
                sb.Append(newWord ? char.ToUpperInvariant(c) : char.ToLowerInvariant(c));
                newWord = false;
            }
            else
            {
                sb.Append(c);
                newWord = true;
            }
        }
        return PyStrObject.FromString(sb.ToString());
    }

    [PyMethod("swapcase")]
    [AIGenerated]
    [PyFunctionParameters]
    private static PyResult Swapcase(PyCallContext context, PyStrObject self, PyArguments arguments)
    {
        var sb = new StringBuilder(self.Value.Length);
        foreach (char c in self.Value)
        {
            if (char.IsUpper(c)) sb.Append(char.ToLowerInvariant(c));
            else if (char.IsLower(c)) sb.Append(char.ToUpperInvariant(c));
            else sb.Append(c);
        }
        return PyStrObject.FromString(sb.ToString());
    }

    [PyMethod("zfill")]
    [AIGenerated]
    [PyFunctionParameters("width", "/")]
    private static PyResult Zfill(PyCallContext context, PyStrObject self, PyArguments arguments)
    {
        if (arguments[0] is not PyIntObject widthObj) return PyResult.TypeError("width must be int");
        int width = widthObj.Int32Value;
        if (width <= self.Value.Length) return self;

        if (self.Value.Length > 0 && (self.Value[0] == '+' || self.Value[0] == '-'))
        {
            return PyStrObject.FromString(self.Value[0] + self.Value[1..].PadLeft(width - 1, '0'));
        }
        
        return PyStrObject.FromString(self.Value.PadLeft(width, '0'));
    }

    protected override PyResult Repr(PyCallContext context, PyStrObject self)
    {
        return PyStrObject.FromString(self.Repr());
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
            return PyResult.IndexError(PySR.Runtime_String_IndexOutOfRange);
        return PyStrObject.FromRune(self.PyCharAt(index));
    }
    protected override PyResult Add(PyCallContext context, PyStrObject self, PyObject other)
    {
        if (other is PyStrObject strObj)
            return PyStrObject.FromString(self.Value + strObj.Value);
        return PyResult.TypeError(PySR.Runtime_String_AddNonStr, other.PyType.FullName);
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
            else if (prefix[0] is 'u' or 'U' or 'b' or 'B')
            {
                isRaw = false;
            }
            else
            {
                return false;
            }
        }
        else if (prefix.Length is 2)
        {
            if (prefix.ContainsAny('r', 'R') && prefix.ContainsAny('b', 'B'))
            {
                isRaw = true;
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
