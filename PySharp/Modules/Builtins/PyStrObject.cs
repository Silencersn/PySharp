using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;
using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;
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

    /// <summary>Convert code-point (Rune) index to char index in the string.</summary>
    internal int RuneIndexToCharIndex(int runeIndex)
    {
        if (runeIndex <= 0)
            return 0;
        int runeCount = 0;
        for (int i = 0; i < Value.Length; i++)
        {
            if (runeCount == runeIndex)
                return i;
            if (char.IsHighSurrogate(Value[i]))
                i++;
            runeCount++;
        }
        return Value.Length;
    }

    /// <summary>Convert char index back to code-point (Rune) index.</summary>
    internal static int CharIndexToRuneIndex(string value, int charIndex)
    {
        int runeCount = 0;
        for (int i = 0; i < value.Length && i < charIndex; i++)
        {
            if (char.IsHighSurrogate(value[i]))
                i++;
            runeCount++;
        }
        return runeCount;
    }

    /// <summary>Return substring limited by rune (code-point) range [startRune, endRune).</summary>
    internal string SubstringByRuneRange(int startRune, int endRune)
    {
        int startChar = RuneIndexToCharIndex(startRune);
        int endChar = RuneIndexToCharIndex(endRune);
        return Value[startChar..endChar];
    }

    /// <summary>Get the first Rune of the string, or default (null character) if empty.</summary>
    internal Rune FirstRune()
    {
        foreach (var rune in Value.EnumerateRunes())
            return rune;
        return default;
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
    [PyFunctionParameters("prefix", "/", "start=0", "end=2147483647")]
    private static PyResult StartsWith(PyCallContext context, PyStrObject self, PyArguments arguments)
    {
        if (arguments[0] is PyStrObject prefixStr)
        {
            int start = 0, end = int.MaxValue;
            if (arguments[1] is PyIntObject startObj)
                start = startObj.Int32Value;
            if (arguments[2] is PyIntObject endObj)
                end = endObj.Int32Value;
            start = Utils.MapIndex(start, self.PyLength);
            if (start < 0)
                start = 0;
            if (end > self.PyLength)
                end = self.PyLength;
            if (start >= end)
                return PyBoolObject.False;
            var sliced = self.SubstringByRuneRange(start, end);
            return PyBoolObject.FromBoolean(sliced.StartsWith(prefixStr.Value));
        }
        return PyResult.TypeError($"startswith first arg must be str");
    }

    [PyMethod("endswith")]
    [AIGenerated]
    [PyFunctionParameters("suffix", "/", "start=0", "end=2147483647")]
    private static PyResult EndsWith(PyCallContext context, PyStrObject self, PyArguments arguments)
    {
        if (arguments[0] is PyStrObject suffixStr)
        {
            int start = 0, end = int.MaxValue;
            if (arguments[1] is PyIntObject startObj)
                start = startObj.Int32Value;
            if (arguments[2] is PyIntObject endObj)
                end = endObj.Int32Value;
            start = Utils.MapIndex(start, self.PyLength);
            if (start < 0)
                start = 0;
            if (end > self.PyLength)
                end = self.PyLength;
            if (start >= end)
                return PyBoolObject.False;
            var sliced = self.SubstringByRuneRange(start, end);
            return PyBoolObject.FromBoolean(sliced.EndsWith(suffixStr.Value));
        }
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
            if (count is 0)
                sb.Append(self.Value.AsSpan(charsAppended));
            return PyStrObject.FromString(sb.ToString());
        }

        var resObj = self.Value;
        int startIndex = 0;
        while (count > 0)
        {
            int idx = resObj.IndexOf(oldStr.Value, startIndex);
            if (idx is -1)
                break;
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
        if (maxsplitObj is PyIntObject maxsplitInt)
            maxsplit = maxsplitInt.Int32Value;

        string[] parts;
        if (sepObj is PyNoneObject)
        {
            if (maxsplit < 0)
                parts = self.Value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            else
                parts = self.Value.Split((char[]?)null, maxsplit + 1, StringSplitOptions.RemoveEmptyEntries);
        }
        else if (sepObj is PyStrObject sepStr)
        {
            if (string.IsNullOrEmpty(sepStr.Value))
                return PyResult.ValueError("empty separator");

            if (maxsplit < 0)
                parts = self.Value.Split([sepStr.Value], StringSplitOptions.None);
            else
                parts = self.Value.Split([sepStr.Value], maxsplit + 1, StringSplitOptions.None);
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

    [PyMethod("rsplit")]
    [AIGenerated]
    [PyFunctionParameters("sep=None", "maxsplit=-1")]
    private static PyResult RSplit(PyCallContext context, PyStrObject self, PyArguments arguments)
    {
        var sepObj = arguments[0];
        var maxsplitObj = arguments[1];

        int maxsplit = -1;
        if (maxsplitObj is PyIntObject maxsplitInt)
            maxsplit = maxsplitInt.Int32Value;

        string[] parts;
        if (sepObj is PyNoneObject)
        {
            if (maxsplit < 0)
            {
                parts = self.Value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            }
            else
            {
                // rsplit with sep=None: scan from right counting whitespace-separated tokens
                var resultList = new List<string>();
                int end = self.Value.Length;
                int count = 0;
                // Skip trailing whitespace
                while (end > 0 && char.IsWhiteSpace(self.Value[end - 1]))
                    end--;
                while (count < maxsplit && end > 0)
                {
                    // Find end of current token
                    int tokenEnd = end;
                    while (end > 0 && !char.IsWhiteSpace(self.Value[end - 1]))
                        end--;
                    resultList.Add(self.Value[end..tokenEnd]);
                    count++;
                    // Skip whitespace between tokens
                    while (end > 0 && char.IsWhiteSpace(self.Value[end - 1]))
                        end--;
                }
                // Remaining part (may include leading whitespace preserved)
                resultList.Add(self.Value[..end]);
                resultList.Reverse();
                parts = [.. resultList];
            }
        }
        else if (sepObj is PyStrObject sepStr)
        {
            if (string.IsNullOrEmpty(sepStr.Value))
                return PyResult.ValueError("empty separator");

            if (maxsplit < 0)
            {
                parts = self.Value.Split([sepStr.Value], StringSplitOptions.None);
            }
            else
            {
                var resultList = new List<string>();
                string remaining = self.Value;
                int count = 0;
                while (count < maxsplit)
                {
                    int idx = remaining.LastIndexOf(sepStr.Value);
                    if (idx < 0)
                            break;
                    resultList.Add(remaining[(idx + sepStr.Value.Length)..]);
                    remaining = remaining[..idx];
                    count++;
                }
                resultList.Add(remaining);
                resultList.Reverse();
                parts = [.. resultList];
            }
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

    private static int ClampRuneStart(int start, int length)
    {
        start = Utils.MapIndex(start, length);
        return start < 0 ? 0 : start > length ? length : start;
    }
    private static int ClampRuneEnd(int end, int length)
    {
        if (end < 0)
            end += length;
        return end < 0 ? 0 : end > length ? length : end;
    }

    [PyMethod("find")]
    [AIGenerated]
    [PyFunctionParameters("sub", "/", "start=0", "end=2147483647")]
    private static PyResult Find(PyCallContext context, PyStrObject self, PyArguments arguments)
    {
        if (arguments[0] is PyStrObject subStr)
        {
            int start = 0, end = int.MaxValue;
            if (arguments[1] is PyIntObject startObj)
                start = startObj.Int32Value;
            if (arguments[2] is PyIntObject endObj)
                end = endObj.Int32Value;
            start = ClampRuneStart(start, self.PyLength);
            end = ClampRuneEnd(end, self.PyLength);
            if (start >= end)
                return PyIntObject.MinusOne;
            var sliced = self.SubstringByRuneRange(start, end);
            int charIdx = sliced.IndexOf(subStr.Value);
            if (charIdx < 0)
                return PyIntObject.MinusOne;
            int charStart = self.RuneIndexToCharIndex(start);
            int resultRuneIdx = PyStrObject.CharIndexToRuneIndex(self.Value, charStart + charIdx);
            return PyIntObject.FromInteger(resultRuneIdx);
        }
        return PyResult.TypeError($"find arg must be str");
    }

    [PyMethod("rfind")]
    [AIGenerated]
    [PyFunctionParameters("sub", "/", "start=0", "end=2147483647")]
    private static PyResult RFind(PyCallContext context, PyStrObject self, PyArguments arguments)
    {
        if (arguments[0] is PyStrObject subStr)
        {
            int start = 0, end = int.MaxValue;
            if (arguments[1] is PyIntObject startObj)
                start = startObj.Int32Value;
            if (arguments[2] is PyIntObject endObj)
                end = endObj.Int32Value;
            start = ClampRuneStart(start, self.PyLength);
            end = ClampRuneEnd(end, self.PyLength);
            if (start >= end)
                return PyIntObject.MinusOne;
            var sliced = self.SubstringByRuneRange(start, end);
            int charIdx = sliced.LastIndexOf(subStr.Value);
            if (charIdx < 0)
                return PyIntObject.MinusOne;
            int charStart = self.RuneIndexToCharIndex(start);
            int resultRuneIdx = PyStrObject.CharIndexToRuneIndex(self.Value, charStart + charIdx);
            return PyIntObject.FromInteger(resultRuneIdx);
        }
        return PyResult.TypeError($"rfind arg must be str");
    }

    [PyMethod("index")]
    [AIGenerated]
    [PyFunctionParameters("sub", "/", "start=0", "end=2147483647")]
    private static PyResult Index(PyCallContext context, PyStrObject self, PyArguments arguments)
    {
        var result = Find(context, self, arguments);
        if (result.IsError)
            return result;
        if (result.Value is not PyIntObject intVal)
            return PyResult.ValueError("substring not found");
        if (intVal.Int32Value < 0)
            return PyResult.ValueError("substring not found");
        return intVal;
    }

    [PyMethod("rindex")]
    [AIGenerated]
    [PyFunctionParameters("sub", "/", "start=0", "end=2147483647")]
    private static PyResult RIndex(PyCallContext context, PyStrObject self, PyArguments arguments)
    {
        var result = RFind(context, self, arguments);
        if (result.IsError)
            return result;
        if (result.Value is not PyIntObject intVal)
            return PyResult.ValueError("substring not found");
        if (intVal.Int32Value < 0)
            return PyResult.ValueError("substring not found");
        return intVal;
    }

    [PyMethod("capitalize")]
    [AIGenerated]
    [PyFunctionParameters]
    private static PyResult Capitalize(PyCallContext context, PyStrObject self, PyArguments arguments)
    {
        if (self.PyLength is 0)
            return self;
        var first = self.FirstRune();
        var sb = new StringBuilder();
        sb.Append(Rune.ToUpperInvariant(first).ToString());
        // Rest of the string in lower case
        bool firstDone = false;
        foreach (var rune in self.Value.EnumerateRunes())
        {
            if (!firstDone) { firstDone = true; continue; }
            sb.Append(Rune.ToLowerInvariant(rune).ToString());
        }
        return PyStrObject.FromString(sb.ToString());
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
            if (fillStr.PyLength is not 1)
                return PyResult.TypeError("fillchar must be a string of length 1");
            fillchar = fillStr.Value;
        }
        else if (arguments[1] is not PyNoneObject)
        {
            return PyResult.TypeError("fillchar must be a character");
        }

        int width = widthObj.Int32Value;
        if (width <= self.PyLength)
            return self;

        int padLeft = (width - self.PyLength) / 2;
        int padRight = width - self.PyLength - padLeft;

        var sb = new StringBuilder(self.Value.Length + padLeft + padRight);
        sb.Append(fillchar[0], padLeft);
        sb.Append(self.Value);
        sb.Append(fillchar[0], padRight);
        return PyStrObject.FromString(sb.ToString());
    }

    [PyMethod("count")]
    [AIGenerated]
    [PyFunctionParameters("sub", "/", "start=0", "end=2147483647")]
    private static PyResult Count(PyCallContext context, PyStrObject self, PyArguments arguments)
    {
        if (arguments[0] is not PyStrObject subStr)
            return PyResult.TypeError("count arg must be str");

        int start = 0, end = int.MaxValue;
        if (arguments[1] is PyIntObject startObj)
            start = startObj.Int32Value;
        if (arguments[2] is PyIntObject endObj)
            end = endObj.Int32Value;
        start = ClampRuneStart(start, self.PyLength);
        end = ClampRuneEnd(end, self.PyLength);

        if (start >= end)
            return PyIntObject.FromInteger(0);
        var sliced = self.SubstringByRuneRange(start, end);

        if (string.IsNullOrEmpty(subStr.Value))
            return PyIntObject.FromInteger(PyStrObject.CharIndexToRuneIndex(sliced, sliced.Length) + 1);

        int count = 0;
        int index = 0;
        while ((index = sliced.IndexOf(subStr.Value, index)) is not -1)
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
        if (self.PyLength is 0)
            return PyBoolObject.False;
        foreach (var rune in self.Value.EnumerateRunes())
        {
            if (!Rune.IsLetterOrDigit(rune))
                return PyBoolObject.False;
        }
        return PyBoolObject.True;
    }

    [PyMethod("isalpha")]
    [AIGenerated]
    [PyFunctionParameters]
    private static PyResult IsAlpha(PyCallContext context, PyStrObject self, PyArguments arguments)
    {
        if (self.PyLength is 0)
            return PyBoolObject.False;
        foreach (var rune in self.Value.EnumerateRunes())
        {
            if (!Rune.IsLetter(rune))
                return PyBoolObject.False;
        }
        return PyBoolObject.True;
    }

    [PyMethod("isdigit")]
    [AIGenerated]
    [PyFunctionParameters]
    private static PyResult IsDigit(PyCallContext context, PyStrObject self, PyArguments arguments)
    {
        if (self.PyLength is 0)
            return PyBoolObject.False;
        foreach (var rune in self.Value.EnumerateRunes())
        {
            if (!Rune.IsDigit(rune))
                return PyBoolObject.False;
        }
        return PyBoolObject.True;
    }

    [PyMethod("islower")]
    [AIGenerated]
    [PyFunctionParameters]
    private static PyResult IsLower(PyCallContext context, PyStrObject self, PyArguments arguments)
    {
        if (self.PyLength is 0)
            return PyBoolObject.False;
        bool hasCased = false;
        foreach (var rune in self.Value.EnumerateRunes())
        {
            if (Rune.IsUpper(rune))
                return PyBoolObject.False;
            if (Rune.IsLower(rune))
                hasCased = true;
        }
        return PyBoolObject.FromBoolean(hasCased);
    }

    [PyMethod("isupper")]
    [AIGenerated]
    [PyFunctionParameters]
    private static PyResult IsUpper(PyCallContext context, PyStrObject self, PyArguments arguments)
    {
        if (self.PyLength is 0)
            return PyBoolObject.False;
        bool hasCased = false;
        foreach (var rune in self.Value.EnumerateRunes())
        {
            if (Rune.IsLower(rune))
                return PyBoolObject.False;
            if (Rune.IsUpper(rune))
                hasCased = true;
        }
        return PyBoolObject.FromBoolean(hasCased);
    }

    [PyMethod("title")]
    [AIGenerated]
    [PyFunctionParameters]
    private static PyResult Title(PyCallContext context, PyStrObject self, PyArguments arguments)
    {
        if (self.PyLength is 0)
            return self;
        var sb = new StringBuilder(self.Value.Length);
        bool newWord = true;
        foreach (var rune in self.Value.EnumerateRunes())
        {
            if (Rune.IsLetter(rune))
            {
                sb.Append(newWord ? Rune.ToUpperInvariant(rune).ToString() : Rune.ToLowerInvariant(rune).ToString());
                newWord = false;
            }
            else
            {
                sb.Append(rune.ToString());
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
        foreach (var rune in self.Value.EnumerateRunes())
        {
            if (Rune.IsUpper(rune))
                sb.Append(Rune.ToLowerInvariant(rune).ToString());
            else if (Rune.IsLower(rune))
                sb.Append(Rune.ToUpperInvariant(rune).ToString());
            else
                sb.Append(rune.ToString());
        }
        return PyStrObject.FromString(sb.ToString());
    }

    [PyMethod("zfill")]
    [AIGenerated]
    [PyFunctionParameters("width", "/")]
    private static PyResult Zfill(PyCallContext context, PyStrObject self, PyArguments arguments)
    {
        if (arguments[0] is not PyIntObject widthObj)
            return PyResult.TypeError("width must be int");
        int width = widthObj.Int32Value;
        if (width <= self.PyLength)
            return self;

        var first = self.FirstRune();
        if (self.PyLength > 0 && (first.Value is '+' or '-'))
        {
            int firstCharLen = first.Utf16SequenceLength;
            return PyStrObject.FromString(self.Value[..firstCharLen] + self.Value[firstCharLen..].PadLeft(width - 1, '0'));
        }

        return PyStrObject.FromString(self.Value.PadLeft(width, '0'));
    }

    [PyMethod("format")]
    [AIGenerated]
    [PyFunctionParameters("*args", "**kwargs")]
    private static PyResult Format(PyCallContext context, PyStrObject self, PyArguments arguments)
    {
        var formatStr = self.Value;
        var extraArgs = arguments.ExtraArgs;
        int argIndex = 0;
        var sb = new StringBuilder();

        for (int i = 0; i < formatStr.Length; i++)
        {
            if (formatStr[i] is '{')
            {
                if (i + 1 < formatStr.Length && formatStr[i + 1] is '{')
                {
                    sb.Append('{');
                    i++;
                    continue;
                }

                int end = formatStr.IndexOf('}', i + 1);
                if (end < 0)
                    return PyResult.ValueError("unmatched '{' in format spec");

                var fieldStr = formatStr.AsSpan(i + 1, end - i - 1);
                PyObject? value = null;

                if (fieldStr.IsEmpty)
                {
                    // {} - positional
                    if (argIndex >= extraArgs.Count)
                        return PyResult.IndexError("tuple index out of range");
                    value = extraArgs[argIndex++];
                }
                else
                {
                    int colonIndex = fieldStr.IndexOf(':');
                    ReadOnlySpan<char> name;
                    ReadOnlySpan<char> fmtSpec = default;
                    if (colonIndex >= 0)
                    {
                        name = fieldStr[..colonIndex];
                        fmtSpec = fieldStr[(colonIndex + 1)..];
                    }
                    else
                    {
                        name = fieldStr;
                    }

                    if (name.Length > 0 && (char.IsDigit(name[0]) || (name.Length > 1 && name[0] is '-' && char.IsDigit(name[1]))))
                    {
                        // {0} or {0:spec}
                        if (!int.TryParse(name, out int idx))
                            return PyResult.ValueError("invalid format specifier");
                        if (idx >= extraArgs.Count)
                            return PyResult.IndexError("tuple index out of range");
                        value = extraArgs[idx];
                    }
                    else
                    {
                        // {name} or {name:spec}
                        string key = name.ToString();
                        if (!arguments.TryGetExtraKwarg(key, out value))
                            return PyResult.KeyError(key);
                    }

                    if (!fmtSpec.IsEmpty)
                    {
                        var formatResult = PySpecialMethods.Format(context, value, PyStrObject.FromString(fmtSpec.ToString()));
                        if (formatResult.IsError)
                            return formatResult;
                        value = formatResult.Value;
                    }
                }

                var strResult = PySpecialMethods.Str(context, value);
                if (strResult.IsError)
                    return strResult;
                sb.Append(strResult.Value.Value);

                i = end;
            }
            else if (formatStr[i] is '}')
            {
                if (i + 1 < formatStr.Length && formatStr[i + 1] is '}')
                {
                    sb.Append('}');
                    i++;
                    continue;
                }
                return PyResult.ValueError("Single '}' encountered in format string");
            }
            else
            {
                sb.Append(formatStr[i]);
            }
        }

        return PyStrObject.FromString(sb.ToString());
    }

    [PyMethod("partition")]
    [AIGenerated]
    [PyFunctionParameters("sep", "/")]
    private static PyResult Partition(PyCallContext context, PyStrObject self, PyArguments arguments)
    {
        if (arguments[0] is not PyStrObject sepStr)
            return PyResult.TypeError("partition sep must be str");
        if (string.IsNullOrEmpty(sepStr.Value))
            return PyResult.ValueError("empty separator");

        int idx = self.Value.IndexOf(sepStr.Value, StringComparison.Ordinal);
        if (idx < 0)
        {
            return PyTupleObject.CreateTuple(
                self,
                PyStrObject.Empty,
                PyStrObject.Empty
            );
        }

        return PyTupleObject.CreateTuple(
            PyStrObject.FromString(self.Value[..idx]),
            PyStrObject.FromString(sepStr.Value),
            PyStrObject.FromString(self.Value[(idx + sepStr.Value.Length)..])
        );
    }

    [PyMethod("splitlines")]
    [AIGenerated]
    [PyFunctionParameters("keepends=False", "/")]
    private static PyResult SplitLines(PyCallContext context, PyStrObject self, PyArguments arguments)
    {
        bool keepends = false;
        if (arguments[0] is PyBoolObject keependsBool)
            keepends = keependsBool.BoolValue;

        var lines = new List<PyObject>();
        int start = 0;

        for (int i = 0; i < self.Value.Length; i++)
        {
            char c = self.Value[i];
            int lineEndLen = 0;

            if (c is '\n')
            {
                lineEndLen = 1;
            }
            else if (c is '\r')
            {
                lineEndLen = 1;
                if (i + 1 < self.Value.Length && self.Value[i + 1] is '\n')
                    lineEndLen = 2;
            }
            else if (c is '\v' or '\f' or '\x1c' or '\x1d' or '\x1e'
                  or '\x85' or '\u2028' or '\u2029')
            {
                lineEndLen = 1;
            }

            if (lineEndLen is 0)
                continue;

            string line = self.Value[start..i];
            if (keepends)
                line += self.Value.Substring(i, lineEndLen);
            lines.Add(PyStrObject.FromString(line));

            i += lineEndLen - 1;
            start = i + 1;
        }

        // Add remaining text after last line break
        if (start < self.Value.Length)
            lines.Add(PyStrObject.FromString(self.Value[start..]));

        return PyListObject.CreateList(lines);
    }

    [PyMethod("isspace")]
    [AIGenerated]
    [PyFunctionParameters]
    private static PyResult IsSpace(PyCallContext context, PyStrObject self, PyArguments arguments)
    {
        if (self.PyLength is 0)
            return PyBoolObject.False;

        foreach (var rune in self.Value.EnumerateRunes())
        {
            if (!Rune.IsWhiteSpace(rune))
                return PyBoolObject.False;
        }
        return PyBoolObject.True;
    }

    [PyMethod("expandtabs")]
    [AIGenerated]
    [PyFunctionParameters("tabsize=8", "/")]
    private static PyResult ExpandTabs(PyCallContext context, PyStrObject self, PyArguments arguments)
    {
        int tabsize = 8;
        if (arguments[0] is PyIntObject tabsizeObj)
        {
            tabsize = tabsizeObj.Int32Value;
            if (tabsize < 0)
                return PyResult.ValueError("tabsize must be non-negative");
        }
        else if (arguments[0] is not PyNoneObject)
        {
            return PyResult.TypeError("tabsize must be int");
        }

        var sb = new StringBuilder();
        int col = 0;
        foreach (var rune in self.Value.EnumerateRunes())
        {
            if (rune.Value is '\t')
            {
                if (tabsize > 0)
                {
                    int spaces = tabsize - (col % tabsize);
                    sb.Append(' ', spaces);
                    col += spaces;
                }
            }
            else if (rune.Value is '\n' or '\r')
            {
                sb.Append(rune.ToString());
                col = 0;
            }
            else
            {
                sb.Append(rune.ToString());
                col++;
            }
        }
        return PyStrObject.FromString(sb.ToString());
    }

    [PyMethod("ljust")]
    [AIGenerated]
    [PyFunctionParameters("width", "fillchar=' '", "/")]
    private static PyResult LJust(PyCallContext context, PyStrObject self, PyArguments arguments)
    {
        if (arguments[0] is not PyIntObject widthObj)
            return PyResult.TypeError("width must be int");

        string fillchar = " ";
        if (arguments[1] is PyStrObject fillStr)
        {
            if (fillStr.PyLength is not 1)
                return PyResult.TypeError("fillchar must be a string of length 1");
            fillchar = fillStr.Value;
        }
        else if (arguments[1] is not PyNoneObject)
        {
            return PyResult.TypeError("fillchar must be a character");
        }

        int width = widthObj.Int32Value;
        if (width <= self.PyLength)
            return self;

        int pad = width - self.PyLength;
        return PyStrObject.FromString(self.Value.PadRight(self.Value.Length + pad, fillchar[0]));
    }

    [PyMethod("rjust")]
    [AIGenerated]
    [PyFunctionParameters("width", "fillchar=' '", "/")]
    private static PyResult RJust(PyCallContext context, PyStrObject self, PyArguments arguments)
    {
        if (arguments[0] is not PyIntObject widthObj)
            return PyResult.TypeError("width must be int");

        string fillchar = " ";
        if (arguments[1] is PyStrObject fillStr)
        {
            if (fillStr.PyLength is not 1)
                return PyResult.TypeError("fillchar must be a string of length 1");
            fillchar = fillStr.Value;
        }
        else if (arguments[1] is not PyNoneObject)
        {
            return PyResult.TypeError("fillchar must be a character");
        }

        int width = widthObj.Int32Value;
        if (width <= self.PyLength)
            return self;

        int pad = width - self.PyLength;
        return PyStrObject.FromString(self.Value.PadLeft(self.Value.Length + pad, fillchar[0]));
    }

    [PyMethod("rpartition")]
    [AIGenerated]
    [PyFunctionParameters("sep", "/")]
    private static PyResult RPartition(PyCallContext context, PyStrObject self, PyArguments arguments)
    {
        if (arguments[0] is not PyStrObject sepStr)
            return PyResult.TypeError("rpartition sep must be str");
        if (string.IsNullOrEmpty(sepStr.Value))
            return PyResult.ValueError("empty separator");

        int idx = self.Value.LastIndexOf(sepStr.Value, StringComparison.Ordinal);
        if (idx < 0)
        {
            return PyTupleObject.CreateTuple(
                PyStrObject.Empty,
                PyStrObject.Empty,
                self
            );
        }

        return PyTupleObject.CreateTuple(
            PyStrObject.FromString(self.Value[..idx]),
            PyStrObject.FromString(sepStr.Value),
            PyStrObject.FromString(self.Value[(idx + sepStr.Value.Length)..])
        );
    }

    [PyMethod("removeprefix")]
    [AIGenerated]
    [PyFunctionParameters("prefix", "/")]
    private static PyResult RemovePrefix(PyCallContext context, PyStrObject self, PyArguments arguments)
    {
        if (arguments[0] is not PyStrObject prefixStr)
            return PyResult.TypeError("removeprefix arg must be str");

        if (self.Value.StartsWith(prefixStr.Value))
            return PyStrObject.FromString(self.Value[prefixStr.Value.Length..]);

        return self;
    }

    [PyMethod("removesuffix")]
    [AIGenerated]
    [PyFunctionParameters("suffix", "/")]
    private static PyResult RemoveSuffix(PyCallContext context, PyStrObject self, PyArguments arguments)
    {
        if (arguments[0] is not PyStrObject suffixStr)
            return PyResult.TypeError("removesuffix arg must be str");

        if (self.Value.EndsWith(suffixStr.Value))
            return PyStrObject.FromString(self.Value[..^suffixStr.Value.Length]);

        return self;
    }

    [PyMethod("isascii")]
    [AIGenerated]
    [PyFunctionParameters]
    private static PyResult IsAscii(PyCallContext context, PyStrObject self, PyArguments arguments)
    {
        foreach (var rune in self.Value.EnumerateRunes())
        {
            if (rune.Value > 127)
                return PyBoolObject.False;
        }
        return PyBoolObject.True;
    }

    [PyMethod("istitle")]
    [AIGenerated]
    [PyFunctionParameters]
    private static PyResult IsTitle(PyCallContext context, PyStrObject self, PyArguments arguments)
    {
        if (self.PyLength is 0)
            return PyBoolObject.False;

        bool isCased = false;
        bool previousIsCased = false;

        foreach (var rune in self.Value.EnumerateRunes())
        {
            if (Rune.IsUpper(rune))
            {
                if (previousIsCased)
                    return PyBoolObject.False;
                previousIsCased = true;
                isCased = true;
            }
            else if (Rune.IsLower(rune))
            {
                if (!previousIsCased)
                    return PyBoolObject.False;
                previousIsCased = true;
                isCased = true;
            }
            else
            {
                previousIsCased = false;
            }
        }
        return PyBoolObject.FromBoolean(isCased);
    }

    [PyMethod("isdecimal")]
    [AIGenerated]
    [PyFunctionParameters]
    private static PyResult IsDecimal(PyCallContext context, PyStrObject self, PyArguments arguments)
    {
        if (self.PyLength is 0)
            return PyBoolObject.False;

        foreach (var rune in self.Value.EnumerateRunes())
        {
            if (Rune.GetUnicodeCategory(rune) is not System.Globalization.UnicodeCategory.DecimalDigitNumber)
                return PyBoolObject.False;
        }
        return PyBoolObject.True;
    }

    [PyMethod("isnumeric")]
    [AIGenerated]
    [PyFunctionParameters]
    private static PyResult IsNumeric(PyCallContext context, PyStrObject self, PyArguments arguments)
    {
        if (self.PyLength is 0)
            return PyBoolObject.False;

        foreach (var rune in self.Value.EnumerateRunes())
        {
            var cat = Rune.GetUnicodeCategory(rune);
            if (cat is not System.Globalization.UnicodeCategory.DecimalDigitNumber
                && cat is not System.Globalization.UnicodeCategory.LetterNumber
                && cat is not System.Globalization.UnicodeCategory.OtherNumber)
                return PyBoolObject.False;
        }
        return PyBoolObject.True;
    }

    [PyMethod("isidentifier")]
    [AIGenerated]
    [PyFunctionParameters]
    private static PyResult IsIdentifier(PyCallContext context, PyStrObject self, PyArguments arguments)
    {
        if (self.PyLength is 0)
            return PyBoolObject.False;

        bool first = true;
        foreach (var rune in self.Value.EnumerateRunes())
        {
            if (first)
            {
                if (rune.Value is not '_' && !Rune.IsLetter(rune))
                    return PyBoolObject.False;
                first = false;
            }
            else
            {
                if (rune.Value is not '_' && !Rune.IsLetterOrDigit(rune))
                    return PyBoolObject.False;
            }
        }
        return PyBoolObject.True;
    }

    [PyMethod("isprintable")]
    [AIGenerated]
    [PyFunctionParameters]
    private static PyResult IsPrintable(PyCallContext context, PyStrObject self, PyArguments arguments)
    {
        foreach (var rune in self.Value.EnumerateRunes())
        {
            var cat = Rune.GetUnicodeCategory(rune);
            if (cat is System.Globalization.UnicodeCategory.Control
                or System.Globalization.UnicodeCategory.Surrogate
                or System.Globalization.UnicodeCategory.PrivateUse
                or System.Globalization.UnicodeCategory.Format
                or System.Globalization.UnicodeCategory.LineSeparator
                or System.Globalization.UnicodeCategory.ParagraphSeparator)
            {
                // Allow certain common whitespace that Python considers printable
                // Python considers tab (\t), newline (\n), carriage return (\r),
                // and their Unicode equivalents as printable.
                if (rune.Value is '\t' or '\n' or '\r'
                    || rune.Value is 0x0b or 0x0c /* \v, \f */)
                    continue;

                if (cat is not System.Globalization.UnicodeCategory.Control)
                    return PyBoolObject.False;
                // For control chars other than tab/newline/CR: not printable
                if (rune.Value < 0x10000) // BMP control chars
                    return PyBoolObject.False;
            }
        }
        return PyBoolObject.True;
    }

    [PyMethod("encode")]
    [AIGenerated]
    [PyFunctionParameters("encoding='utf-8'", "errors='strict'")]
    private static PyResult Encode(PyCallContext context, PyStrObject self, PyArguments arguments)
    {
        string encoding = "utf-8";
        if (arguments[0] is PyStrObject encStr)
            encoding = encStr.Value;
        else if (arguments[0] is not PyNoneObject)
            return PyResult.TypeError("encoding must be str");

        string errors = "strict";
        if (arguments[1] is PyStrObject errStr)
            errors = errStr.Value;
        else if (arguments[1] is not PyNoneObject)
            return PyResult.TypeError("errors must be str");

        try
        {
            var enc = System.Text.Encoding.GetEncoding(encoding);
            byte[] bytes;
            if (errors is "strict")
            {
                bytes = enc.GetBytes(self.Value);
            }
            else if (errors is "ignore")
            {
                enc.GetBytes(self.Value, 0, self.Value.Length, new byte[enc.GetMaxByteCount(self.Value.Length)], 0);
                // Simple approach: use encoder fallback
                var encoder = enc.GetEncoder();
                encoder.Fallback = new EncoderReplacementFallback(string.Empty);
                int byteCount = encoder.GetByteCount(self.Value.ToCharArray(), 0, self.Value.Length, true);
                bytes = new byte[byteCount];
                encoder.GetBytes(self.Value.ToCharArray(), 0, self.Value.Length, bytes, 0, true);
            }
            else if (errors is "replace")
            {
                var encoder = enc.GetEncoder();
                encoder.Fallback = new EncoderReplacementFallback("?");
                int byteCount = encoder.GetByteCount(self.Value.ToCharArray(), 0, self.Value.Length, true);
                bytes = new byte[byteCount];
                encoder.GetBytes(self.Value.ToCharArray(), 0, self.Value.Length, bytes, 0, true);
            }
            else if (errors is "xmlcharrefreplace")
            {
                var encoder = enc.GetEncoder();
                encoder.Fallback = new EncoderExceptionFallback();
                // Use custom handling
                bytes = enc.GetBytes(self.Value);
            }
            else if (errors is "backslashreplace")
            {
                var encoder = enc.GetEncoder();
                encoder.Fallback = new EncoderExceptionFallback();
                bytes = enc.GetBytes(self.Value);
            }
            else if (errors is "namereplace")
            {
                var encoder = enc.GetEncoder();
                encoder.Fallback = new EncoderExceptionFallback();
                bytes = enc.GetBytes(self.Value);
            }
            else
            {
                return PyResult.ValueError($"unknown error handler: '{errors}'");
            }
            return PyBytesObject.MoveBytes(bytes);
        }
        catch (ArgumentException)
        {
            return PyResult.ValueError($"unknown encoding: {encoding}");
        }
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
        if (item is PySliceObject slice)
        {
            var indicesResult = slice.Indices(context, self.PyLength, out var indices);
            if (indicesResult.IsError)
                return indicesResult;
            var (start, _, step, length) = indices;
            if (length is 0)
                return PyStrObject.Empty;

            // Collect all runes for slicing
            var runes = new List<Rune>(self.PyLength);
            foreach (var rune in self.Value.EnumerateRunes())
                runes.Add(rune);

            var sb = new StringBuilder(length);
            for (int i = start, ri = 0; ri < length; i += step, ri++)
                sb.Append(runes[i].ToString());
            return PyStrObject.FromString(sb.ToString());
        }

        var result = PySpecialMethods.Index(context, item);
        if (result.IsError)
            return result;
        if (!result.Value.IsInt32)
            return PyResult.IndexError(PySR.Runtime_String_IndexOutOfRange);
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
        // Ordinal (code point) comparison, matching CPython's unicode_compare.
        // string.CompareTo would use culture rules ('a' < 'B' incorrectly).
        return PyBoolObject.FromBoolean(string.CompareOrdinal(self.Value, strObj.Value) < 0);
    }
    protected override PyResult Le(PyCallContext context, PyStrObject self, PyObject other)
    {
        if (other is not PyStrObject strObj)
            return PyNotImplementedObject.NotImplemented;
        return PyBoolObject.FromBoolean(string.CompareOrdinal(self.Value, strObj.Value) <= 0);
    }
    protected override PyResult Gt(PyCallContext context, PyStrObject self, PyObject other)
    {
        if (other is not PyStrObject strObj)
            return PyNotImplementedObject.NotImplemented;
        return PyBoolObject.FromBoolean(string.CompareOrdinal(self.Value, strObj.Value) > 0);
    }
    protected override PyResult Ge(PyCallContext context, PyStrObject self, PyObject other)
    {
        if (other is not PyStrObject strObj)
            return PyNotImplementedObject.NotImplemented;
        return PyBoolObject.FromBoolean(string.CompareOrdinal(self.Value, strObj.Value) >= 0);
    }
    protected override PyResult Mul(PyCallContext context, PyStrObject self, PyObject other)
    {
        var result = PySpecialMethods.Index(context, other);
        if (result.IsError)
            return result;
        var count = result.Value;
        if (count.Value < 0)
            return PyStrObject.Empty;   // CPython: 'x' * -1 == ''
        if (!count.IsInt32)
            return PyResult.OverflowError("cannot fit 'int' into an index-sized integer");
        return PyStrObject.FromString(string.Concat(Enumerable.Repeat(self.Value, count.Int32Value)));
    }
    protected override PyResult RMul(PyCallContext context, PyStrObject self, PyObject other)
    {
        return Mul(context, self, other);
    }

    [AIGenerated]
    protected override PyResult Mod(PyCallContext context, PyStrObject self, PyObject other)
    {
        // Implement Python's % string formatting (old-style %-formatting)
        var formatStr = self.Value;
        IReadOnlyList<PyObject>? args = null;
        PyDictObject? dict = null;

        if (other is PyTupleObject tuple)
            args = tuple;
        else if (other is PyDictObject dictObj)
            dict = dictObj;
        else
            args = [other];

        int argIndex = 0;
        var sb = new StringBuilder();

        for (int i = 0; i < formatStr.Length; i++)
        {
            if (formatStr[i] is not '%')
            {
                sb.Append(formatStr[i]);
                continue;
            }

            i++; // skip '%'
            if (i >= formatStr.Length)
                return PyResult.ValueError("incomplete format");

            if (formatStr[i] is '%')
            {
                sb.Append('%');
                continue;
            }

            PyObject? value;

            // Handle dict-based: %(name)format
            if (formatStr[i] is '(')
            {
                if (dict is null)
                    return PyResult.TypeError("format requires a mapping");

                int closeParen = formatStr.IndexOf(')', i + 1);
                if (closeParen < 0)
                    return PyResult.ValueError("missing ')' in format spec");

                string key = formatStr[(i + 1)..closeParen];
                i = closeParen + 1;

                var keyObj = PyStrObject.FromString(key);
                if (!dict.TryGetValue(keyObj, out value))
                    return PyResult.KeyError(key);
            }
            else
            {
                if (args is null)
                    return PyResult.TypeError("format requires a mapping");
                if (argIndex >= args.Count)
                    return PyResult.TypeError("not enough arguments for format string");
                value = args[argIndex++];
            }

            // Parse flags
            bool flagAlternate = false;
            bool flagZeroPad = false;
            bool flagLeftAlign = false;
            bool flagSpace = false;
            bool flagSign = false;

            while (i < formatStr.Length && " #0-+".Contains(formatStr[i]))
            {
                switch (formatStr[i])
                {
                    case '#': flagAlternate = true; break;
                    case '0': flagZeroPad = true; break;
                    case '-': flagLeftAlign = true; break;
                    case ' ': flagSpace = true; break;
                    case '+': flagSign = true; break;
                }
                i++;
            }

            // Parse width
            int width = -1;
            if (i < formatStr.Length && formatStr[i] is '*')
            {
                if (args is null || argIndex >= args.Count)
                    return PyResult.TypeError("not enough arguments for format string");
                if (args[argIndex] is not PyIntObject widthObj)
                    return PyResult.TypeError("* wants int");
                width = widthObj.Int32Value;
                argIndex++;
                i++;
            }
            else
            {
                var widthStr = string.Empty;
                while (i < formatStr.Length && char.IsDigit(formatStr[i]))
                    widthStr += formatStr[i++];
                if (widthStr.Length > 0)
                    width = int.Parse(widthStr);
            }

            // Parse precision
            int precision = -1;
            if (i < formatStr.Length && formatStr[i] is '.')
            {
                i++;
                if (i < formatStr.Length && formatStr[i] is '*')
                {
                    if (args is null || argIndex >= args.Count)
                        return PyResult.TypeError("not enough arguments for format string");
                    if (args[argIndex] is not PyIntObject precObj)
                        return PyResult.TypeError("* wants int");
                    precision = precObj.Int32Value;
                    argIndex++;
                    i++;
                }
                else
                {
                    var precStr = string.Empty;
                    while (i < formatStr.Length && char.IsDigit(formatStr[i]))
                        precStr += formatStr[i++];
                    if (precStr.Length > 0)
                        precision = int.Parse(precStr);
                    else
                        return PyResult.ValueError("format requires a precision");
                }
            }

            // Skip 'h', 'l', 'L' length modifiers (C style, ignored by Python)
            if (i < formatStr.Length && (formatStr[i] is 'h' or 'l' or 'L'))
                i++;

            // Parse format type
            if (i >= formatStr.Length)
                return PyResult.ValueError("incomplete format");
            char fmtType = formatStr[i];

            // Format the value
            string formatted;
            switch (fmtType)
            {
                case 's':
                    {
                        var strResult = PySpecialMethods.Str(context, value);
                        if (strResult.IsError)
                            return strResult;
                        formatted = strResult.Value.Value;
                        if (precision >= 0 && formatted.Length > precision)
                            formatted = formatted[..precision];
                        break;
                    }
                case 'r':
                    {
                        var reprResult = PySpecialMethods.Repr(context, value);
                        if (reprResult.IsError)
                            return reprResult;
                        formatted = reprResult.Value.Value;
                        if (precision >= 0 && formatted.Length > precision)
                            formatted = formatted[..precision];
                        break;
                    }
                case 'a':
                    {
                        var asciiResult = PyBuiltinFunctions.Ascii.Call(context, [value]);
                        if (asciiResult.IsError)
                            return asciiResult;
                        formatted = ((PyStrObject)asciiResult.Value).Value;
                        if (precision >= 0 && formatted.Length > precision)
                            formatted = formatted[..precision];
                        break;
                    }
                case 'd':
                case 'i':
                case 'u':
                    {
                        var indexResult = PySpecialMethods.Index(context, value);
                        if (indexResult.IsError)
                            return indexResult;
                        var intVal = indexResult.Value.Value;
                        string intStr;
                        if (precision >= 0)
                            intStr = BigInteger.Abs(intVal).ToString($"D{precision}");
                        else
                            intStr = BigInteger.Abs(intVal).ToString();
                        if (intVal.Sign < 0)
                            intStr = "-" + intStr;
                        else if (flagSign)
                            intStr = "+" + intStr;
                        else if (flagSpace)
                            intStr = " " + intStr;
                        formatted = intStr;
                        break;
                    }
                case 'o':
                    {
                        var indexResult = PySpecialMethods.Index(context, value);
                        if (indexResult.IsError)
                            return indexResult;
                        var octVal = indexResult.Value.Value;
                        bool isNeg = octVal.Sign < 0;
                        var absVal = isNeg ? -octVal : octVal;
                        string digits = absVal == 0 ? "0" : BigIntegerToBase(absVal, 8, false);
                        // Apply precision (minimum number of digits)
                        if (precision >= 0 && digits.Length < precision)
                            digits = new string('0', precision - digits.Length) + digits;
                        // CPython '%#o' adds the 0o prefix for ALL values
                        // (including 0 and negatives), with the sign first.
                        string sign = isNeg ? "-" : string.Empty;
                        string prefix = flagAlternate ? "0o" : string.Empty;
                        formatted = sign + prefix + digits;
                        break;
                    }
                case 'x':
                    {
                        var indexResult = PySpecialMethods.Index(context, value);
                        if (indexResult.IsError)
                            return indexResult;
                        var hexBigInt = indexResult.Value.Value;
                        bool isNeg = hexBigInt.Sign < 0;
                        var absVal = isNeg ? -hexBigInt : hexBigInt;
                        string digits = absVal == 0 ? "0" : BigIntegerToBase(absVal, 16, false);
                        // Apply precision (minimum number of digits)
                        if (precision >= 0 && digits.Length < precision)
                            digits = new string('0', precision - digits.Length) + digits;
                        // CPython '%#x' adds the 0x prefix for ALL values
                        // (including 0 and negatives), with the sign first.
                        string sign = isNeg ? "-" : string.Empty;
                        string prefix = flagAlternate ? "0x" : string.Empty;
                        formatted = sign + prefix + digits;
                        break;
                    }
                case 'X':
                    {
                        var indexResult = PySpecialMethods.Index(context, value);
                        if (indexResult.IsError)
                            return indexResult;
                        var hexBigInt = indexResult.Value.Value;
                        bool isNeg = hexBigInt.Sign < 0;
                        var absVal = isNeg ? -hexBigInt : hexBigInt;
                        string digits = absVal == 0 ? "0" : BigIntegerToBase(absVal, 16, true);
                        // Apply precision (minimum number of digits)
                        if (precision >= 0 && digits.Length < precision)
                            digits = new string('0', precision - digits.Length) + digits;
                        // CPython '%#X' adds the 0X prefix for ALL values
                        // (including 0 and negatives), with the sign first.
                        string sign = isNeg ? "-" : string.Empty;
                        string prefix = flagAlternate ? "0X" : string.Empty;
                        formatted = sign + prefix + digits;
                        break;
                    }
                case 'e':
                case 'E':
                    {
                        var floatResult = PySpecialMethods.Float(context, value);
                        if (floatResult.IsError)
                            return floatResult;
                        double d = floatResult.Value.Value;
                        int prec = precision >= 0 ? precision : 6;
                        string fmt = fmtType is 'e' ? $"e{prec}" : $"E{prec}";
                        formatted = d.ToString(fmt, CultureInfo.InvariantCulture);
                        if (double.IsNaN(d) || double.IsInfinity(d))
                        {
                            // CPython: '%e' -> 'inf'/'nan', '%E' -> 'INF'/'NAN'
                            formatted = FormatNonFinite(d, fmtType is 'E');
                        }
                        else
                        {
                            // .NET 'e' pads the exponent to 3 digits (e+000);
                            // CPython %e uses at least 2 (e+00).
                            formatted = FixExponentWidth(formatted);
                            if (flagAlternate && precision is 0)
                            {
                                // Force decimal point: remove trailing digits and keep the dot
                                int dotIndex = formatted.IndexOf('.');
                                if (dotIndex < 0)
                                {
                                    int eIndex = formatted.IndexOf('e');
                                    if (eIndex < 0)
                                        eIndex = formatted.IndexOf('E');
                                    formatted = formatted.Insert(eIndex < 0 ? formatted.Length : eIndex, ".");
                                }
                            }
                        }
                        // '+'/' ' flags also apply to nan/inf ('+inf', '+nan');
                        // NaN has no sign (its sign bit may be set) and -0.0
                        // counts as negative, so no flag is added for -0.0.
                        bool showFlag = double.IsNaN(d) || !double.IsNegative(d);
                        if (showFlag && flagSign)
                            formatted = "+" + formatted;
                        else if (showFlag && flagSpace)
                            formatted = " " + formatted;
                        break;
                    }
                case 'f':
                case 'F':
                    {
                        var floatResult = PySpecialMethods.Float(context, value);
                        if (floatResult.IsError)
                            return floatResult;
                        double d = floatResult.Value.Value;
                        int prec = precision >= 0 ? precision : 6;
                        string fmt = $"F{prec}";
                        formatted = d.ToString(fmt, CultureInfo.InvariantCulture);
                        if (double.IsNaN(d) || double.IsInfinity(d))
                        {
                            // CPython: '%f' -> 'inf'/'nan', '%F' -> 'INF'/'NAN'
                            formatted = FormatNonFinite(d, fmtType is 'F');
                        }
                        else
                        {
                            if (flagAlternate && precision is 0)
                            {
                                // Force decimal point: e.g. "3" -> "3."
                                if (!formatted.Contains('.'))
                                    formatted += ".";
                            }
                        }
                        // '+'/' ' flags also apply to nan/inf; -0.0 counts as negative.
                        bool showFlag = double.IsNaN(d) || !double.IsNegative(d);
                        if (showFlag && flagSign)
                            formatted = "+" + formatted;
                        else if (showFlag && flagSpace)
                            formatted = " " + formatted;
                        break;
                    }
                case 'g':
                case 'G':
                    {
                        var floatResult = PySpecialMethods.Float(context, value);
                        if (floatResult.IsError)
                            return floatResult;
                        double d = floatResult.Value.Value;
                        int prec = precision >= 0 ? precision : 6;
                        // CPython %g treats precision 0 as 1 significant digit;
                        // .NET 'g0' is the shortest round-trip form, so use 1.
                        int gPrec = prec is 0 ? 1 : prec;
                        // Lowercase 'g' makes .NET emit a lowercase 'e'.
                        string fmt = fmtType is 'g' ? $"g{gPrec}" : $"G{gPrec}";
                        formatted = d.ToString(fmt, CultureInfo.InvariantCulture);
                        if (double.IsNaN(d) || double.IsInfinity(d))
                        {
                            // CPython: '%g' -> 'inf'/'nan', '%G' -> 'INF'/'NAN'
                            formatted = FormatNonFinite(d, fmtType is 'G');
                        }
                        else
                        {
                            if (flagAlternate)
                                // CPython %#g keeps trailing zeros up to the
                                // significant digits and forces a decimal point.
                                formatted = AddGTrailingZeros(formatted, gPrec);
                        }
                        // '+'/' ' flags also apply to nan/inf; -0.0 counts as negative.
                        bool showFlag = double.IsNaN(d) || !double.IsNegative(d);
                        if (showFlag && flagSign)
                            formatted = "+" + formatted;
                        else if (showFlag && flagSpace)
                            formatted = " " + formatted;
                        break;
                    }
                case 'c':
                    {
                        if (value is PyStrObject { Value.Length: 1 } cStr)
                        {
                            formatted = cStr.Value;
                        }
                        else
                        {
                            var indexResult = PySpecialMethods.Index(context, value);
                            if (indexResult.IsError)
                                return indexResult;
                            var codePoint = indexResult.Value.Value;   // BigInteger: range check before narrowing
                            if (codePoint < 0 || codePoint > 0x10FFFF)
                                return PyResult.OverflowError("%c arg not in range(0x110000)");
                            int cp = (int)codePoint;
                            // CPython allows lone surrogates (e.g. '%c' % 0xD800 -> '\ud800'); .NET string can store them
                            formatted = cp <= 0xFFFF ? ((char)cp).ToString() : char.ConvertFromUtf32(cp);
                        }
                        break;
                    }
                default:
                    return PyResult.ValueError($"unsupported format character '{fmtType}' (0x{(int)fmtType:x})");
            }

            // Apply # flag for float formats that already handled precision decimal point
            // (additional # handling for f/e/g already done above)

            // Override width if the # flag added extra characters for octal/hex

            // Apply width and alignment
            if (width > formatted.Length)
            {
                char padChar = flagZeroPad && !flagLeftAlign ? '0' : ' ';
                if (flagLeftAlign)
                {
                    formatted = formatted.PadRight(width, padChar);
                }
                else if (flagZeroPad && formatted.Length > 0)
                {
                    // Zero-padding: zeros go after any sign and any
                    // '0x'/'0o'/'0X' alternate prefix ('%#08x' % -16 -> '-0x00010').
                    int padCount = width;
                    string head = string.Empty;
                    string tail = formatted;
                    if (tail[0] is '+' or '-' or ' ')
                    {
                        head = tail[..1];
                        tail = tail[1..];
                        padCount--;
                    }
                    if (tail.Length >= 2 && tail[0] is '0' && tail[1] is 'x' or 'o' or 'X')
                    {
                        head += tail[..2];
                        tail = tail[2..];
                        padCount -= 2;
                    }
                    formatted = head + tail.PadLeft(padCount, padChar);
                }
                else
                {
                    formatted = formatted.PadLeft(width, padChar);
                }
            }

            sb.Append(formatted);
        }

        // Check for unused arguments
        if (args is not null && argIndex < args.Count)
            return PyResult.TypeError("not all arguments converted during string formatting");

        return PyStrObject.FromString(sb.ToString());
    }

    protected override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        if (!PyArgsValidator.ValidateSinglePositionalArg(args, kwargs, out var err))
            return err.Value;
        return PySpecialMethods.Str(context, args[0]);
    }

    private static string BigIntegerToBase(BigInteger value, int radix, bool upper)
    {
        if (value.IsZero)
            return "0";
        var sb = new StringBuilder();
        while (value > 0)
        {
            int digit = (int)(value % radix);
            char c = digit < 10 ? (char)('0' + digit) : (char)((upper ? 'A' : 'a') + digit - 10);
            sb.Append(c);
            value /= radix;
        }
        // Reverse the string
        var chars = sb.ToString().ToCharArray();
        Array.Reverse(chars);
        return new string(chars);
    }

    private static string FormatNonFinite(double d, bool upper)
    {
        // CPython old-style %: non-finite values print as 'inf'/'nan'
        // (%e/%f/%g) or 'INF'/'NAN' (%E/%F/%G), with a leading '-'
        // for negative infinity.
        if (double.IsNaN(d))
            return upper ? "NAN" : "nan";
        if (d < 0)
            return upper ? "-INF" : "-inf";
        return upper ? "INF" : "inf";
    }

    private static string FixExponentWidth(string s)
    {
        // .NET 'e'/'E' format pads the exponent to 3 digits (e+000);
        // CPython %e/%E uses at least 2 (e+00). Drop the leading zero of
        // a 3-digit exponent below 100, keeping 3 digits for exponents >= 100.
        int marker = s.IndexOf('e');
        if (marker < 0)
            marker = s.IndexOf('E');
        if (marker < 0)
            return s;
        int signPos = marker + 1;
        if (signPos + 3 < s.Length &&
            (s[signPos] is '+' or '-') &&
            s[signPos + 1] is '0' &&
            char.IsAsciiDigit(s[signPos + 2]) &&
            char.IsAsciiDigit(s[signPos + 3]))
            return s[..(signPos + 1)] + s[(signPos + 2)..];
        return s;
    }

    private static string AddGTrailingZeros(string s, int sigPrec)
    {
        // CPython %#g keeps the decimal point and pads trailing zeros so the
        // mantissa shows exactly 'sigPrec' significant digits.
        int signLen = s.Length > 0 && (s[0] is '+' or '-' or ' ') ? 1 : 0;
        string sign = s[..signLen];
        string body = s[signLen..];
        int eIdx = body.IndexOf('e');
        if (eIdx < 0)
            eIdx = body.IndexOf('E');
        string mantissa = eIdx < 0 ? body : body[..eIdx];
        string exponent = eIdx < 0 ? string.Empty : body[eIdx..];

        int zeros = sigPrec - CountSignificantDigits(mantissa);
        if (zeros > 0)
        {
            if (mantissa.Contains('.'))
                mantissa += new string('0', zeros);
            else
                mantissa += "." + new string('0', zeros);
        }
        else if (!mantissa.Contains('.'))
        {
            mantissa += ".";
        }
        return sign + mantissa + exponent;
    }

    private static int CountSignificantDigits(string mantissa)
    {
        // Count digits after the first non-zero digit; an all-zero mantissa
        // ("0", "0.000") counts as 1 significant digit.
        int count = 0;
        bool started = false;
        foreach (char c in mantissa)
        {
            if (char.IsAsciiDigit(c))
            {
                if (c is not '0')
                {
                    started = true;
                    count++;
                }
                else if (started)
                {
                    count++;
                }
            }
        }
        return started ? count : 1;
    }

    protected override PyResult Contains(PyCallContext context, PyStrObject self, PyObject item)
    {
        if (item is not PyStrObject { Value: var str })
            return PyResult.TypeError(null);

        return PyBoolObject.FromBoolean(self.Value.Contains(str));
    }
}