using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;
using PySharp.Utility;
using System.Buffers;
using System.Diagnostics;
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
            return field = CountCodePoints(Value);
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

    /// <summary>
    /// A fresh instance outside the empty singleton and the character pool,
    /// for callers that retag the result to another type (subclass
    /// construction); <see cref="FromString"/> may hand out shared objects.
    /// </summary>
    public static PyStrObject FromStringNoCache(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new PyStrObject(value);
    }
    // CPython's chr(): a code point in U+D800-U+DFFF yields a lone surrogate
    internal static PyStrObject FromCodePoint(int codePoint)
    {
        if ((uint)codePoint < CharPoolSize)
            return _charPool[codePoint];
        return codePoint < 0x10000
            ? new PyStrObject(((char)codePoint).ToString())
            : new PyStrObject(char.ConvertFromUtf32(codePoint));
    }

    internal string Repr()
    {
        return PyStrConverter.FromStringToLiteral(Value);
    }

    // CPython's str stores code points (PEP 393), where U+D800-U+DFFF are
    // ordinary values; System.Rune cannot represent a surrogate and
    // String.EnumerateRunes() yields U+FFFD for an unpaired one, so the
    // character views below step over UTF-16 code units themselves.
    internal ref struct CodePointEnumerator(ReadOnlySpan<char> value)
    {
        private readonly ReadOnlySpan<char> _value = value;
        private int _index = -1;

        public int Current { get; private set; } = -1;

        public bool MoveNext()
        {
            var next = _index + 1;
            if (next >= _value.Length)
                return false;

            _index = next;
            var unit = _value[next];
            if (char.IsHighSurrogate(unit) && next + 1 < _value.Length && char.IsLowSurrogate(_value[next + 1]))
            {
                _index = next + 1;
                Current = char.ConvertToUtf32(unit, _value[next + 1]);
            }
            else
            {
                Current = unit;
            }
            return true;
        }
    }

    internal CodePointEnumerator EnumerateCodePoints() => new(Value);

    internal static int CountCodePoints(ReadOnlySpan<char> value)
    {
        var count = 0;
        var enumerator = new CodePointEnumerator(value);
        while (enumerator.MoveNext())
            count++;
        return count;
    }

    internal static int CodePointAt(ReadOnlySpan<char> value, int index)
    {
        var enumerator = new CodePointEnumerator(value);
        while (enumerator.MoveNext())
        {
            if (index-- is 0)
                return enumerator.Current;
        }
        throw new UnreachableException();
    }

    internal int PyCharAt(int index) => CodePointAt(Value, index);

    // width in UTF-16 code units of the code point starting at index
    internal static int CharWidthAt(ReadOnlySpan<char> value, int index) =>
        char.IsHighSurrogate(value[index]) && index + 1 < value.Length && char.IsLowSurrogate(value[index + 1]) ? 2 : 1;

    internal static void AppendCodePoint(StringBuilder builder, int codePoint)
    {
        if (codePoint < 0x10000)
            builder.Append((char)codePoint);
        else
            builder.Append(char.ConvertFromUtf32(codePoint));
    }

    internal static void AppendRepeated(StringBuilder builder, string value, int count)
    {
        for (var i = 0; i < count; i++)
            builder.Append(value);
    }

    // CPython unicode_compare: ordering is by code point, not by UTF-16
    // code unit (a surrogate pair sorts above U+E000-U+FFFF)
    internal static int CompareCodePoints(ReadOnlySpan<char> left, ReadOnlySpan<char> right)
    {
        var leftEnumerator = new CodePointEnumerator(left);
        var rightEnumerator = new CodePointEnumerator(right);
        while (true)
        {
            var hasLeft = leftEnumerator.MoveNext();
            var hasRight = rightEnumerator.MoveNext();
            if (!hasLeft || !hasRight)
                return hasLeft == hasRight ? 0 : hasLeft ? 1 : -1;
            if (leftEnumerator.Current != rightEnumerator.Current)
                return leftEnumerator.Current < rightEnumerator.Current ? -1 : 1;
        }
    }

    // CPython's isalpha/isdigit/... shape: every code point must satisfy the
    // predicate, and the empty string is False
    internal static PyResult AllCodePoints(PyStrObject self, Func<int, bool> predicate)
    {
        if (self.PyLength is 0)
            return PyBoolObject.False;

        var enumerator = self.EnumerateCodePoints();
        while (enumerator.MoveNext())
        {
            if (!predicate(enumerator.Current))
                return PyBoolObject.False;
        }
        return PyBoolObject.True;
    }

    /// <summary>Prefix of at most <paramref name="count"/> code points.</summary>
    internal static string CodePointPrefix(string value, int count)
    {
        if (CountCodePoints(value) <= count)
            return value;

        var builder = new StringBuilder(count);
        var enumerator = new CodePointEnumerator(value);
        for (var taken = 0; taken < count && enumerator.MoveNext(); taken++)
            AppendCodePoint(builder, enumerator.Current);
        return builder.ToString();
    }

    /// <summary>Convert a code-point index to a char index in the string.</summary>
    internal int CodePointIndexToCharIndex(int codePointIndex) => CodePointIndexToCharIndex(Value, codePointIndex);

    internal static int CodePointIndexToCharIndex(ReadOnlySpan<char> value, int codePointIndex)
    {
        if (codePointIndex <= 0)
            return 0;
        int count = 0;
        for (int i = 0; i < value.Length; i += CharWidthAt(value, i))
        {
            if (count == codePointIndex)
                return i;
            count++;
        }
        return value.Length;
    }

    /// <summary>Convert a char index back to a code-point index.</summary>
    internal static int CharIndexToCodePointIndex(string value, int charIndex)
    {
        int count = 0;
        for (int i = 0; i < value.Length && i < charIndex; i += CharWidthAt(value, i))
            count++;
        return count;
    }

    /// <summary>Return the substring covering the code-point range [start, end).</summary>
    internal string SubstringByCodePointRange(int start, int end)
    {
        int startChar = CodePointIndexToCharIndex(start);
        int endChar = CodePointIndexToCharIndex(end);
        return Value[startChar..endChar];
    }

    /// <summary>First code point of the string, or -1 when empty.</summary>
    internal int FirstCodePoint() => Value.Length is 0 ? -1 : CodePointAt(Value, 0);

    // CPython's unicode_upper/lower/casefold shape: map every code point and
    // rebuild the string
    internal static PyResult MapCodePoints(PyStrObject self, Func<int, string> map)
    {
        var builder = new StringBuilder(self.Value.Length);
        var enumerator = self.EnumerateCodePoints();
        while (enumerator.MoveNext())
            builder.Append(map(enumerator.Current));
        return PyStrObject.FromString(builder.ToString());
    }

    internal static int[] ToCodePointArray(PyStrObject self) => ToCodePointArray(self.Value);

    internal static int[] ToCodePointArray(ReadOnlySpan<char> value)
    {
        var result = new int[CountCodePoints(value)];
        var enumerator = new CodePointEnumerator(value);
        for (var i = 0; i < result.Length; i++)
        {
            enumerator.MoveNext();
            result[i] = enumerator.Current;
        }
        return result;
    }

    // CPython lower_ucs4: U+03A3 takes the context-sensitive Final_Sigma
    // rule, every other code point the table
    internal static string ToLowerAt(int[] codePoints, int index) =>
        codePoints[index] is 0x3A3
            ? (IsFinalSigma(codePoints, index) ? "\u03C2" : "\u03C3")
            : PyUnicodeData.ToLower(codePoints[index]);

    private static bool IsFinalSigma(int[] codePoints, int index)
    {
        var j = index - 1;
        while (j >= 0 && PyUnicodeData.IsCaseIgnorable(codePoints[j]))
            j--;

        if (j < 0 || !PyUnicodeData.IsCased(codePoints[j]))
            return false;

        j = index + 1;
        while (j < codePoints.Length && PyUnicodeData.IsCaseIgnorable(codePoints[j]))
            j++;
        return j == codePoints.Length || !PyUnicodeData.IsCased(codePoints[j]);
    }

    public static int GetHashCode(string s)
    {
        var hashCode = s.GetHashCode();
        if (hashCode is -1)
            return -2;
        return hashCode;
    }

    public override int GetHashCode()
    {
        return GetHashCode(Value);
    }

    // CPython unicode_expandtabs: the column counts runes, a tab advances
    // to the next tab stop, and \n/\r reset the column
    internal static string ExpandTabsCore(string value, int tabsize)
    {
        var sb = new StringBuilder();
        int col = 0;
        foreach (var rune in value.EnumerateRunes())
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
        return sb.ToString();
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
    private static PyResult Upper(PyCallContext context, PyStrObject self, PyArguments arguments) =>
        PyStrObject.MapCodePoints(self, PyUnicodeData.ToUpper);

    [PyMethod("lower")]
    [AIGenerated]
    [PyFunctionParameters]
    private static PyResult Lower(PyCallContext context, PyStrObject self, PyArguments arguments)
    {
        var codePoints = PyStrObject.ToCodePointArray(self);
        var sb = new StringBuilder(self.Value.Length);
        for (var i = 0; i < codePoints.Length; i++)
            sb.Append(PyStrObject.ToLowerAt(codePoints, i));
        return PyStrObject.FromString(sb.ToString());
    }

    [PyMethod("strip")]
    [AIGenerated]
    [PyFunctionParameters("chars=None", "/")]
    private static PyResult Strip(PyCallContext context, PyStrObject self, PyArguments arguments)
    {
        if (arguments[0] is PyNoneObject)
            return PyStrObject.FromString(self.Value.Trim());
        if (arguments[0] is PyStrObject charsStr)
        {
            // CPython: an empty chars set strips nothing, unlike .NET Trim(),
            // where an empty char[] means "trim all whitespace".
            if (charsStr.Value.Length is 0)
                return self;
            return PyStrObject.FromString(self.Value.Trim(charsStr.Value.ToCharArray()));
        }
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
        {
            // CPython: an empty chars set strips nothing.
            if (charsStr.Value.Length is 0)
                return self;
            return PyStrObject.FromString(self.Value.TrimStart(charsStr.Value.ToCharArray()));
        }
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
        {
            // CPython: an empty chars set strips nothing.
            if (charsStr.Value.Length is 0)
                return self;
            return PyStrObject.FromString(self.Value.TrimEnd(charsStr.Value.ToCharArray()));
        }
        return PyResult.TypeError($"rstrip arg must be None or str");
    }

    [PyMethod("startswith")]
    [AIGenerated]
    [PyFunctionParameters("prefix", "start=0", "end=2147483647", "/")]
    private static PyResult StartsWith(PyCallContext context, PyStrObject self, PyArguments arguments)
    {
        var sub = arguments[0];

        if (!TrySliceIndex(context, arguments[1], 0, out int start, out var startError))
            return startError;
        if (!TrySliceIndex(context, arguments[2], int.MaxValue, out int end, out var endError))
            return endError;

        if (sub is PyTupleObject prefixTuple)
        {
            foreach (var item in prefixTuple)
            {
                if (item is not PyStrObject itemStr)
                    return PyResult.TypeError(PySR.Runtime_Str_StartswithTupleItemMustBeStr, item.PyType.Name);

                if (TailMatch(self, itemStr, start, end, startswith: true))
                    return PyBoolObject.True;
            }
            // nothing matched
            return PyBoolObject.False;
        }

        if (sub is not PyStrObject prefixStr)
            return PyResult.TypeError(PySR.Runtime_Str_StartswithFirstArgMustBeStr, sub.PyType.Name);

        return PyBoolObject.FromBoolean(TailMatch(self, prefixStr, start, end, startswith: true));
    }

    [PyMethod("endswith")]
    [AIGenerated]
    [PyFunctionParameters("suffix", "start=0", "end=2147483647", "/")]
    private static PyResult EndsWith(PyCallContext context, PyStrObject self, PyArguments arguments)
    {
        var sub = arguments[0];

        if (!TrySliceIndex(context, arguments[1], 0, out int start, out var startError))
            return startError;
        if (!TrySliceIndex(context, arguments[2], int.MaxValue, out int end, out var endError))
            return endError;

        if (sub is PyTupleObject suffixTuple)
        {
            foreach (var item in suffixTuple)
            {
                if (item is not PyStrObject itemStr)
                    return PyResult.TypeError(PySR.Runtime_Str_EndswithTupleItemMustBeStr, item.PyType.Name);

                if (TailMatch(self, itemStr, start, end, startswith: false))
                    return PyBoolObject.True;
            }
            // nothing matched
            return PyBoolObject.False;
        }

        if (sub is not PyStrObject suffixStr)
            return PyResult.TypeError(PySR.Runtime_Str_EndswithFirstArgMustBeStr, sub.PyType.Name);

        return PyBoolObject.FromBoolean(TailMatch(self, suffixStr, start, end, startswith: false));
    }

    private static bool TailMatch(PyStrObject self, PyStrObject needle, int start, int end, bool startswith)
    {
        // CPython adjust_indices wraps a negative start but keeps a
        // positive start as-is even above the length; tailmatch then
        // fails the window check, so an empty needle is False only
        // past the end of the string
        if (start < 0)
            start = ClampRuneStart(start, self.PyLength);
        end = ClampRuneEnd(end, self.PyLength);
        // the window must at least fit the needle, so a length-0
        // needle matches at every valid position, including
        // zero-width windows and the empty string
        if (end - start < needle.PyLength)
            return false;
        var sliced = self.SubstringByCodePointRange(start, end);
        return startswith ? sliced.StartsWith(needle.Value, StringComparison.Ordinal) : sliced.EndsWith(needle.Value, StringComparison.Ordinal);
    }

    [PyMethod("replace")]
    [AIGenerated]
    [PyFunctionParameters("old", "new", "/", "count=-1")]
    private static PyResult Replace(PyCallContext context, PyStrObject self, PyArguments arguments)
    {
        if (arguments[0] is not PyStrObject oldStr)
            return PyResult.TypeError(PySR.Runtime_Str_MethodArgMustBeStr, "replace", 1, arguments[0].PyType.Name);
        if (arguments[1] is not PyStrObject newStr)
            return PyResult.TypeError(PySR.Runtime_Str_MethodArgMustBeStr, "replace", 2, arguments[1].PyType.Name);

        if (!TrySizeArg(context, arguments[2], PySR.Runtime_Number_Int_TooLargeForSsize, out int count, out var countError))
            return countError;

        if (string.IsNullOrEmpty(oldStr.Value))
        {
            // CPython interleave semantics for an empty oldValue: insert
            // newStr n times between the characters, where n = min(count,
            // len(s)+1) (count < 0 means "no limit", i.e. n = len(s)+1).
            var codePoints = new List<int>(self.PyLength);
            var enumerator = self.EnumerateCodePoints();
            while (enumerator.MoveNext())
                codePoints.Add(enumerator.Current);
            int slen = codePoints.Count;
            int n = count < 0 ? slen + 1 : Math.Min(count, slen + 1);
            if (n is 0)
                return self;
            var sb = new StringBuilder();
            for (int i = 0; i < n; i++)
            {
                sb.Append(newStr.Value);
                if (i < slen)
                    PyStrObject.AppendCodePoint(sb, codePoints[i]);
            }
            for (int i = n; i < slen; i++)
                PyStrObject.AppendCodePoint(sb, codePoints[i]);
            return PyStrObject.FromString(sb.ToString());
        }

        if (count < 0)
            return PyStrObject.FromString(self.Value.Replace(oldStr.Value, newStr.Value));

        var resObj = self.Value;
        int startIndex = 0;
        while (count > 0)
        {
            int idx = resObj.IndexOf(oldStr.Value, startIndex, StringComparison.Ordinal);
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

        // CPython do_split: the separator is validated before maxsplit
        if (sepObj is not PyNoneObject and not PyStrObject)
            return PyResult.TypeError(PySR.Runtime_Str_SplitSepMustBeStr, sepObj.PyType.Name);

        if (!TrySizeArg(context, arguments[1], PySR.Runtime_Number_Int_TooLargeForSsize, out int maxsplit, out var maxsplitError))
            return maxsplitError;

        string[] parts;
        if (sepObj is PyNoneObject)
        {
            if (maxsplit < 0)
                parts = self.Value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            else
                parts = self.Value.Split((char[]?)null, maxsplit + 1, StringSplitOptions.RemoveEmptyEntries);
        }
        else
        {
            var sepStr = (PyStrObject)sepObj;
            if (string.IsNullOrEmpty(sepStr.Value))
                return PyResult.ValueError("empty separator");

            if (maxsplit < 0)
                parts = self.Value.Split([sepStr.Value], StringSplitOptions.None);
            else
                parts = self.Value.Split([sepStr.Value], maxsplit + 1, StringSplitOptions.None);
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

        // CPython do_xsplit: the separator is validated before maxsplit
        if (sepObj is not PyNoneObject and not PyStrObject)
            return PyResult.TypeError(PySR.Runtime_Str_SplitSepMustBeStr, sepObj.PyType.Name);

        if (!TrySizeArg(context, arguments[1], PySR.Runtime_Number_Int_TooLargeForSsize, out int maxsplit, out var maxsplitError))
            return maxsplitError;

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
        else
        {
            var sepStr = (PyStrObject)sepObj;
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
                    int idx = remaining.LastIndexOf(sepStr.Value, StringComparison.Ordinal);
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

        var list = new List<PyObject>(parts.Length);
        foreach (var p in parts)
            list.Add(PyStrObject.FromString(p));

        return PyListObject.CreateList(list);
    }

    private static int ClampRuneStart(int start, int length)
    {
        start = PyUtils.MapIndex(start, length);
        return start < 0 ? 0 : start > length ? length : start;
    }
    private static int ClampRuneEnd(int end, int length)
    {
        if (end < 0)
            end += length;
        return end < 0 ? 0 : end > length ? length : end;
    }

    // CPython _PyEval_SliceIndex: None keeps the method default,
    // anything with __index__ converts through Py_ssize_t (saturating,
    // see SaturateIndex), and everything else raises the slice-indices
    // TypeError regardless of the object's actual type
    private static bool TrySliceIndex(PyCallContext context, PyObject arg, int defaultValue, out int value, out PyResult error)
    {
        if (arg is PyNoneObject)
        {
            value = defaultValue;
            error = default;
            return true;
        }

        var indexResult = PySpecialMethods.Index(context, arg);
        if (indexResult.IsError)
        {
            value = default;
            error = PyTypeErrorObjectType.Shared.IsInstance(indexResult.Exception)
                ? PyResult.TypeError(PySR.Runtime_Slice_IndicesMustBeInt)
                : indexResult;
            return false;
        }

        value = PyUtils.SaturateIndex(indexResult.Value.Value);
        error = default;
        return true;
    }

    // CPython clinic Py_ssize_t/int converters: non-int arguments convert
    // through __index__ (propagating its "cannot be interpreted as an
    // integer" TypeError) and out-of-range ints raise OverflowError with
    // the C-level type name
    private static bool TrySizeArg(PyCallContext context, PyObject arg, string overflowMessage, out int value, out PyResult error)
    {
        var indexResult = PySpecialMethods.Index(context, arg);
        if (indexResult.IsError)
        {
            value = default;
            error = indexResult;
            return false;
        }

        var big = indexResult.Value.Value;
        if (big > int.MaxValue || big < int.MinValue)
        {
            value = default;
            error = PyResult.OverflowError(overflowMessage);
            return false;
        }

        value = (int)big;
        error = default;
        return true;
    }

    [PyMethod("find")]
    [AIGenerated]
    [PyFunctionParameters("sub", "start=0", "end=2147483647", "/")]
    private static PyResult Find(PyCallContext context, PyStrObject self, PyArguments arguments)
    {
        return FindImpl(context, self, arguments, "find");
    }

    private static PyResult FindImpl(PyCallContext context, PyStrObject self, PyArguments arguments, string methodName)
    {
        if (arguments[0] is not PyStrObject subStr)
            return PyResult.TypeError(PySR.Runtime_Str_MethodArgMustBeStr, methodName, 1, arguments[0].PyType.Name);
        if (!TrySliceIndex(context, arguments[1], 0, out int start, out var startError))
            return startError;
        if (!TrySliceIndex(context, arguments[2], int.MaxValue, out int end, out var endError))
            return endError;
        // CPython ADJUST_INDICES: like startswith above, start clamps
        // only at 0 so an above-length start keeps end - start negative
        if (start < 0)
            start = ClampRuneStart(start, self.PyLength);
        end = ClampRuneEnd(end, self.PyLength);
        // stringlib/find.h: an empty needle is found at the window
        // start whenever the window is valid (end - start >= 0)
        if (subStr.Value.Length is 0)
            return end - start < 0 ? PyIntObject.MinusOne : PyIntObject.FromInteger(start);
        if (start >= end)
            return PyIntObject.MinusOne;
        var sliced = self.SubstringByCodePointRange(start, end);
        int charIdx = sliced.IndexOf(subStr.Value, StringComparison.Ordinal);
        if (charIdx < 0)
            return PyIntObject.MinusOne;
        int charStart = self.CodePointIndexToCharIndex(start);
        int resultRuneIdx = PyStrObject.CharIndexToCodePointIndex(self.Value, charStart + charIdx);
        return PyIntObject.FromInteger(resultRuneIdx);
    }

    [PyMethod("rfind")]
    [AIGenerated]
    [PyFunctionParameters("sub", "start=0", "end=2147483647", "/")]
    private static PyResult RFind(PyCallContext context, PyStrObject self, PyArguments arguments)
    {
        return RFindImpl(context, self, arguments, "rfind");
    }

    private static PyResult RFindImpl(PyCallContext context, PyStrObject self, PyArguments arguments, string methodName)
    {
        if (arguments[0] is not PyStrObject subStr)
            return PyResult.TypeError(PySR.Runtime_Str_MethodArgMustBeStr, methodName, 1, arguments[0].PyType.Name);
        if (!TrySliceIndex(context, arguments[1], 0, out int start, out var startError))
            return startError;
        if (!TrySliceIndex(context, arguments[2], int.MaxValue, out int end, out var endError))
            return endError;
        // CPython ADJUST_INDICES: start clamps only at 0
        if (start < 0)
            start = ClampRuneStart(start, self.PyLength);
        end = ClampRuneEnd(end, self.PyLength);
        // stringlib/find.h: an empty needle is reported at the window
        // end whenever the window is valid (end - start >= 0)
        if (subStr.Value.Length is 0)
            return end - start < 0 ? PyIntObject.MinusOne : PyIntObject.FromInteger(end);
        if (start >= end)
            return PyIntObject.MinusOne;
        var sliced = self.SubstringByCodePointRange(start, end);
        int charIdx = sliced.LastIndexOf(subStr.Value, StringComparison.Ordinal);
        if (charIdx < 0)
            return PyIntObject.MinusOne;
        int charStart = self.CodePointIndexToCharIndex(start);
        int resultRuneIdx = PyStrObject.CharIndexToCodePointIndex(self.Value, charStart + charIdx);
        return PyIntObject.FromInteger(resultRuneIdx);
    }

    [PyMethod("index")]
    [AIGenerated]
    [PyFunctionParameters("sub", "start=0", "end=2147483647", "/")]
    private static PyResult Index(PyCallContext context, PyStrObject self, PyArguments arguments)
    {
        var result = FindImpl(context, self, arguments, "index");
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
    [PyFunctionParameters("sub", "start=0", "end=2147483647", "/")]
    private static PyResult RIndex(PyCallContext context, PyStrObject self, PyArguments arguments)
    {
        var result = RFindImpl(context, self, arguments, "rindex");
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
        // CPython unicode_capitalize: the first character takes the titlecase
        // mapping, the rest the (context-sensitive) lowercase one
        var codePoints = PyStrObject.ToCodePointArray(self);
        var sb = new StringBuilder();
        sb.Append(PyUnicodeData.ToTitle(codePoints[0]));
        for (var i = 1; i < codePoints.Length; i++)
            sb.Append(PyStrObject.ToLowerAt(codePoints, i));
        return PyStrObject.FromString(sb.ToString());
    }

    [PyMethod("casefold")]
    [AIGenerated]
    [PyFunctionParameters]
    private static PyResult Casefold(PyCallContext context, PyStrObject self, PyArguments arguments) =>
        PyStrObject.MapCodePoints(self, PyUnicodeData.ToCasefold);

    [PyMethod("center")]
    [AIGenerated]
    [PyFunctionParameters("width", "fillchar=' '", "/")]
    private static PyResult Center(PyCallContext context, PyStrObject self, PyArguments arguments)
    {
        if (!TrySizeArg(context, arguments[0], PySR.Runtime_Number_Int_TooLargeForSsize, out int width, out var widthError))
            return widthError;

        string fillchar;
        if (arguments[1] is PyStrObject fillStr)
        {
            if (fillStr.PyLength is not 1)
                return PyResult.TypeError(PySR.Runtime_Str_FillCharLength);
            fillchar = fillStr.Value;
        }
        else
        {
            // CPython: the fillchar default applies by omission only; None
            // reports as NoneType like any other non-string
            return PyResult.TypeError(PySR.Runtime_Str_FillCharMustBeUnicode, arguments[1].PyType.Name);
        }
        if (width <= self.PyLength)
            return self;

        int marg = width - self.PyLength;
        // CPython unicode_center_impl: when both marg and width are odd the
        // odd remainder column goes to the left (marg & width & 1)
        int padLeft = marg / 2 + (marg & width & 1);
        int padRight = marg - padLeft;

        // the fill character is one code point, which an astral one spells
        // with two code units — repeat the whole character, not a unit of it
        var sb = new StringBuilder(self.Value.Length + (padLeft + padRight) * fillchar.Length);
        PyStrObject.AppendRepeated(sb, fillchar, padLeft);
        sb.Append(self.Value);
        PyStrObject.AppendRepeated(sb, fillchar, padRight);
        return PyStrObject.FromString(sb.ToString());
    }

    [PyMethod("count")]
    [AIGenerated]
    [PyFunctionParameters("sub", "start=0", "end=2147483647", "/")]
    private static PyResult Count(PyCallContext context, PyStrObject self, PyArguments arguments)
    {
        if (arguments[0] is not PyStrObject subStr)
            return PyResult.TypeError(PySR.Runtime_Str_MethodArgMustBeStr, "count", 1, arguments[0].PyType.Name);

        if (!TrySliceIndex(context, arguments[1], 0, out int start, out var startError))
            return startError;
        if (!TrySliceIndex(context, arguments[2], int.MaxValue, out int end, out var endError))
            return endError;
        // CPython ADJUST_INDICES: start clamps only at 0
        if (start < 0)
            start = ClampRuneStart(start, self.PyLength);
        end = ClampRuneEnd(end, self.PyLength);

        // stringlib/count.h: an empty needle counts the insertion positions,
        // (end - start) + 1 for a valid window; a negative window (start
        // above the clamped end) misses before the needle is even considered
        if (subStr.Value.Length is 0)
            return end - start < 0 ? PyIntObject.Zero : PyIntObject.FromInteger(end - start + 1);

        if (start >= end)
            return PyIntObject.Zero;
        var sliced = self.SubstringByCodePointRange(start, end);

        int count = 0;
        int index = 0;
        while ((index = sliced.IndexOf(subStr.Value, index, StringComparison.Ordinal)) is not -1)
        {
            count++;
            index += subStr.Value.Length;
        }
        return PyIntObject.FromInteger(count);
    }

    [PyMethod("isalnum")]
    [AIGenerated]
    [PyFunctionParameters]
    private static PyResult IsAlnum(PyCallContext context, PyStrObject self, PyArguments arguments) =>
        PyStrObject.AllCodePoints(self, PyUnicodeData.IsAlnum);

    [PyMethod("isalpha")]
    [AIGenerated]
    [PyFunctionParameters]
    private static PyResult IsAlpha(PyCallContext context, PyStrObject self, PyArguments arguments) =>
        PyStrObject.AllCodePoints(self, PyUnicodeData.IsAlpha);

    [PyMethod("isdigit")]
    [AIGenerated]
    [PyFunctionParameters]
    private static PyResult IsDigit(PyCallContext context, PyStrObject self, PyArguments arguments) =>
        PyStrObject.AllCodePoints(self, PyUnicodeData.IsDigit);

    [PyMethod("islower")]
    [AIGenerated]
    [PyFunctionParameters]
    private static PyResult IsLower(PyCallContext context, PyStrObject self, PyArguments arguments)
    {
        if (self.PyLength is 0)
            return PyBoolObject.False;

        var hasCased = false;
        var enumerator = self.EnumerateCodePoints();
        while (enumerator.MoveNext())
        {
            if (PyUnicodeData.IsUpper(enumerator.Current))
                return PyBoolObject.False;
            if (PyUnicodeData.IsLower(enumerator.Current))
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

        var hasCased = false;
        var enumerator = self.EnumerateCodePoints();
        while (enumerator.MoveNext())
        {
            if (PyUnicodeData.IsLower(enumerator.Current))
                return PyBoolObject.False;
            if (PyUnicodeData.IsUpper(enumerator.Current))
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
        var codePoints = PyStrObject.ToCodePointArray(self);
        var sb = new StringBuilder(self.Value.Length);
        var previousIsCased = false;
        for (var i = 0; i < codePoints.Length; i++)
        {
            if (PyUnicodeData.IsCased(codePoints[i]))
            {
                sb.Append(previousIsCased ? PyStrObject.ToLowerAt(codePoints, i) : PyUnicodeData.ToTitle(codePoints[i]));
                previousIsCased = true;
            }
            else
            {
                PyStrObject.AppendCodePoint(sb, codePoints[i]);
                previousIsCased = false;
            }
        }
        return PyStrObject.FromString(sb.ToString());
    }

    [PyMethod("swapcase")]
    [AIGenerated]
    [PyFunctionParameters]
    private static PyResult Swapcase(PyCallContext context, PyStrObject self, PyArguments arguments)
    {
        var codePoints = PyStrObject.ToCodePointArray(self);
        var sb = new StringBuilder(self.Value.Length);
        for (var i = 0; i < codePoints.Length; i++)
        {
            if (PyUnicodeData.IsUpper(codePoints[i]))
                sb.Append(PyStrObject.ToLowerAt(codePoints, i));
            else if (PyUnicodeData.IsLower(codePoints[i]))
                sb.Append(PyUnicodeData.ToUpper(codePoints[i]));
            else
                PyStrObject.AppendCodePoint(sb, codePoints[i]);
        }
        return PyStrObject.FromString(sb.ToString());
    }

    [PyMethod("zfill")]
    [AIGenerated]
    [PyFunctionParameters("width", "/")]
    private static PyResult Zfill(PyCallContext context, PyStrObject self, PyArguments arguments)
    {
        if (!TrySizeArg(context, arguments[0], PySR.Runtime_Number_Int_TooLargeForSsize, out int width, out var widthError))
            return widthError;
        if (width <= self.PyLength)
            return self;

        // CPython unicode_zfill: '0' fills up to the requested code point
        // count, placed after a leading sign when there is one
        var fill = width - self.PyLength;
        var first = self.FirstCodePoint();
        var signLength = self.PyLength is not 0 && (first is '+' or '-') ? 1 : 0;
        var sb = new StringBuilder(self.Value.Length + fill);
        sb.Append(self.Value, 0, signLength);
        sb.Append('0', fill);
        sb.Append(self.Value, signLength, self.Value.Length - signLength);
        return PyStrObject.FromString(sb.ToString());
    }

    [AIGenerated]
    protected override PyResult Format(PyCallContext context, PyStrObject self, PyObject formatSpec)
    {
        if (formatSpec is not PyStrObject specStr)
            return PyResult.TypeError(PySR.Runtime_Object_FormatArg2NonString, formatSpec.PyType.FullName);

        // CPython _PyUnicode_FormatAdvancedWriter: a zero-length spec makes
        // __format__ equivalent to str(obj)
        if (specStr.Value.Length is 0)
            return PySpecialMethods.Str(context, self);

        if (!PyFormatSpec.TryParse(specStr.Value, out var spec, out var bothSeparators))
            return PyFormatSpec.ParseError(specStr.Value, self.PyType.FullName);

        // CPython validates in this order: the parse-level grouping check,
        // the presentation-type dispatch, then format_string_internal's
        // flag checks (',c' -> Cannot specify, '+d' -> Unknown format code,
        // '+5' -> Sign not allowed)
        var groupingError = PyFormatSpec.ValidateGrouping(spec, bothSeparators, spec.Type ?? 's');
        if (groupingError.IsError)
            return groupingError;

        if (spec.Type is not (null or 's'))
            return PyResult.ValueError(PySR.Runtime_Object_FormatUnknownCode, spec.Type, self.PyType.FullName);

        if (spec.Sign is not null)
        {
            if (spec.Sign is ' ')
                return PyResult.ValueError(PySR.Runtime_Object_FormatSpaceNotAllowed);

            return PyResult.ValueError(PySR.Runtime_Object_FormatSignNotAllowed);
        }

        if (spec.CoercePositiveZero)
            return PyResult.ValueError(PySR.Runtime_Object_FormatZNegCoercionNotAllowed);

        if (spec.AlternateForm)
            return PyResult.ValueError(PySR.Runtime_Object_FormatAlternateNotAllowed);

        if (spec.Align is '=')
            return PyResult.ValueError(PySR.Runtime_Object_FormatAlignNotAllowed);

        var text = self.Value;
        var length = self.PyLength;
        if (spec.Precision is int precision && precision < length)
        {
            text = self.SubstringByCodePointRange(0, precision);
            length = precision;
        }

        if (spec.Width is int width && width > length)
        {
            // CPython zero padding only replaces the default fill for
            // strings; the default alignment stays '<'
            var fill = spec.Fill ?? (spec.SignAwareZeroPadding ? '0' : ' ');
            var align = spec.Align ?? '<';
            var padding = width - length;
            text = align switch
            {
                '<' => text + new string(fill, padding),
                '^' => new string(fill, padding / 2) + text + new string(fill, padding - padding / 2),
                _ => new string(fill, padding) + text,
            };
        }

        return PyStrObject.FromString(text);
    }

    [PyMethod("format")]
    [AIGenerated]
    [PyFunctionParameters("*args", "**kwargs")]
    private static PyResult Format(PyCallContext context, PyStrObject self, PyArguments arguments)
    {
        var autoNumber = 0;
        // -1 = no numbered field seen yet, 0 = manual, 1 = automatic; shared
        // with nested format-spec expansions like CPython.
        var numberMode = -1;
        return ExpandFormatMarkup(context, self.Value, arguments, recursionDepth: 2, ref autoNumber, ref numberMode);
    }

    // Mirrors CPython do_string_format (Objects/stringlib/unicode_format.h):
    // doubled braces escape, {field[!conv][:spec]} markup, one level of
    // spec nesting below the current one
    private static PyResult ExpandFormatMarkup(PyCallContext context, ReadOnlySpan<char> format, PyArguments arguments, int recursionDepth, ref int autoNumber, ref int numberMode)
    {
        if (recursionDepth <= 0)
            return PyResult.ValueError("Max string recursion exceeded");

        var sb = new StringBuilder();
        var i = 0;
        while (i < format.Length)
        {
            var c = format[i];
            if (c is not ('{' or '}'))
            {
                sb.Append(c);
                i++;
                continue;
            }

            if (i + 1 < format.Length && format[i + 1] == c)
            {
                sb.Append(c);
                i += 2;
                continue;
            }

            if (c is '}')
                return PyResult.ValueError("Single '}' encountered in format string");

            if (i + 1 == format.Length)
                return PyResult.ValueError("Single '{' encountered in format string");

            if (!TryParseMarkupField(format[(i + 1)..], out var field, out var fieldLength, out var error))
                return PyResult.ValueError(error);

            var rendered = RenderMarkupField(context, field, arguments, recursionDepth, ref autoNumber, ref numberMode);
            if (rendered.IsError)
                return rendered;

            sb.Append(((PyStrObject)rendered.Value).Value);
            i += 1 + fieldLength;
        }

        return PyStrObject.FromString(sb.ToString());
    }

    private ref struct MarkupField
    {
        public ReadOnlySpan<char> Name;
        public char? Conversion;
        public ReadOnlySpan<char> Spec;
        public bool SpecNeedsExpanding;
    }

    // Mirrors CPython parse_field: the name runs to the first unbracketed
    // '!' or ':' (a '[' swallows up to its ']'), one conversion char may
    // follow '!', and the spec balances nested braces
    private static bool TryParseMarkupField(ReadOnlySpan<char> s, out MarkupField field, out int length, out string error)
    {
        field = default;
        length = 0;

        var i = 0;
        char c = '\0';
        while (i < s.Length)
        {
            c = s[i++];
            if (c is '{')
            {
                error = "unexpected '{' in field name";
                return false;
            }

            if (c is '[')
            {
                while (i < s.Length && s[i] is not ']')
                    i++;
                continue;
            }

            if (c is '}' or ':' or '!')
                break;
        }

        field.Name = s[..Math.Max(0, i - 1)];

        if (c is '!' or ':')
        {
            if (c is '!')
            {
                if (i >= s.Length)
                {
                    error = "end of string while looking for conversion specifier";
                    return false;
                }

                field.Conversion = s[i++];

                if (i < s.Length)
                {
                    c = s[i++];
                    if (c is '}')
                    {
                        length = i;
                        error = string.Empty;
                        return true;
                    }

                    if (c is not ':')
                    {
                        error = "expected ':' after conversion specifier";
                        return false;
                    }
                }

                // hitting the end right after the conversion falls through
                // to the spec scan, which reports the unmatched brace
            }

            var specStart = i;
            var depth = 1;
            while (i < s.Length)
            {
                c = s[i++];
                if (c is '{')
                {
                    depth++;
                    field.SpecNeedsExpanding = true;
                }
                else if (c is '}')
                {
                    depth--;
                    if (depth is 0)
                    {
                        field.Spec = s[specStart..(i - 1)];
                        length = i;
                        error = string.Empty;
                        return true;
                    }
                }
            }

            error = "unmatched '{' in format spec";
            return false;
        }

        if (c is '}')
        {
            length = i;
            error = string.Empty;
            return true;
        }

        error = "expected '}' before end of string";
        return false;
    }

    private static PyResult RenderMarkupField(PyCallContext context, MarkupField field, PyArguments arguments, int recursionDepth, ref int autoNumber, ref int numberMode)
    {
        var name = field.Name;
        var i = 0;
        while (i < name.Length && name[i] is not ('.' or '['))
            i++;

        var first = name[..i];
        PyObject? value;
        if (first.IsEmpty)
        {
            // Bare {} takes the next positional argument; CPython forbids
            // switching to automatic once a manual numeric field was used.
            if (numberMode is 0)
                return PyResult.ValueError(PySR.Runtime_Str_Format_ManualToAutoFieldNumber);
            numberMode = 1;

            var args = arguments.ExtraArgs;
            if (autoNumber >= args.Count)
                return PyResult.IndexError(PySR.Format(PySR.Runtime_Str_Format_ReplacementIndexOutOfRange, autoNumber));
            value = args[autoNumber++];
        }
        else if (IsAllAsciiDigits(first))
        {
            // Numeric names are manual fields and lock out automatic ones;
            // keyword names do not participate in the mode check.
            if (numberMode is 1)
                return PyResult.ValueError(PySR.Runtime_Str_Format_AutoToManualFieldSpecification);
            numberMode = 0;

            if (!int.TryParse(first, out var index))
                return PyResult.ValueError("Too many decimal digits in format string");

            var args = arguments.ExtraArgs;
            if (index >= args.Count)
                return PyResult.IndexError(PySR.Format(PySR.Runtime_Str_Format_ReplacementIndexOutOfRange, index));
            value = args[index];
        }
        else if (!arguments.TryGetExtraKwarg(first.ToString(), out value))
        {
            return PyResult.KeyError(first.ToString());
        }

        // '.'attribute and '[item]' accesses, left to right
        while (i < name.Length)
        {
            if (name[i] is '.')
            {
                i++;
                var start = i;
                while (i < name.Length && name[i] is not ('.' or '['))
                    i++;

                var attr = name[start..i];
                if (attr.IsEmpty)
                    return PyResult.ValueError("Empty attribute in format string");

                var attrResult = PyOperators.GetAttr(context, value, PyStrObject.FromString(attr.ToString()));
                if (attrResult.IsError)
                    return attrResult;
                value = attrResult.Value;
            }
            else if (name[i] is '[')
            {
                i++;
                var start = i;
                while (i < name.Length && name[i] is not ']')
                    i++;

                if (i >= name.Length)
                    return PyResult.ValueError("Missing ']' in format string");

                var key = name[start..i];
                i++; // skip ']'

                var itemResult = PySpecialMethods.GetItem(
                    context,
                    value,
                    IsAllAsciiDigits(key)
                        ? PyIntObject.FromInteger(int.Parse(key, CultureInfo.InvariantCulture))
                        : PyStrObject.FromString(key.ToString()));
                if (itemResult.IsError)
                    return itemResult;
                value = itemResult.Value;
            }
            else
            {
                return PyResult.ValueError("Only '.' or '[' may follow ']' in format field specifier");
            }
        }

        if (field.Conversion is char conversion)
        {
            switch (conversion)
            {
                case 's':
                {
                    var str = PySpecialMethods.Str(context, value);
                    if (str.IsError)
                        return str;
                    value = str.Value;
                    break;
                }
                case 'r':
                {
                    var repr = PySpecialMethods.Repr(context, value);
                    if (repr.IsError)
                        return repr;
                    value = repr.Value;
                    break;
                }
                case 'a':
                {
                    var ascii = PySpecialMethods.Repr(context, value);
                    if (ascii.IsError)
                        return ascii;
                    value = PyStrObject.FromString(PyBuiltinFunctions.EscapeNonAscii(ascii.Value.Value));
                    break;
                }
                default:
                    return PyResult.ValueError($"Unknown conversion specifier {conversion}");
            }
        }

        if (field.SpecNeedsExpanding)
        {
            var expanded = ExpandFormatMarkup(context, field.Spec, arguments, recursionDepth - 1, ref autoNumber, ref numberMode);
            if (expanded.IsError)
                return expanded;

            return PySpecialMethods.Format(context, value, expanded.Value);
        }

        return PySpecialMethods.Format(context, value, field.Spec.IsEmpty ? PyStrObject.Empty : PyStrObject.FromString(field.Spec.ToString()));
    }

    private static bool IsAllAsciiDigits(ReadOnlySpan<char> s)
    {
        if (s.IsEmpty)
            return false;

        foreach (var c in s)
        {
            if (!char.IsAsciiDigit(c))
                return false;
        }

        return true;
    }

    [PyMethod("partition")]
    [AIGenerated]
    [PyFunctionParameters("sep", "/")]
    private static PyResult Partition(PyCallContext context, PyStrObject self, PyArguments arguments)
    {
        if (arguments[0] is not PyStrObject sepStr)
            return PyResult.TypeError(PySR.Runtime_Str_SepMustBeStr, arguments[0].PyType.Name);
        if (string.IsNullOrEmpty(sepStr.Value))
            return PyResult.ValueError("empty separator");

        int idx = self.Value.IndexOf(sepStr.Value, StringComparison.Ordinal);
        // CPython stringlib/partition.h: a missing separator raises instead
        // of returning the whole string (that fallback belongs to rpartition)
        if (idx < 0)
            return PyResult.ValueError("substring not found");

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
        // CPython keepends is a plain truth test on the raw argument
        // (PyObject_IsTrue): int 1/2, 1.5 and any other truthy object
        // keep the line endings
        var keependsResult = PySpecialMethods.Bool(context, arguments[0]);
        if (keependsResult.IsError)
            return keependsResult;
        bool keepends = keependsResult.Value.BoolValue;

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
    private static PyResult IsSpace(PyCallContext context, PyStrObject self, PyArguments arguments) =>
        PyStrObject.AllCodePoints(self, PyUnicodeData.IsSpace);

    [PyMethod("expandtabs")]
    [AIGenerated]
    [PyFunctionParameters("tabsize=8", "/")]
    private static PyResult ExpandTabs(PyCallContext context, PyStrObject self, PyArguments arguments)
    {
        // expandtabs uses the C int converter (its overflow message names
        // "C int", unlike the ssize_t converters elsewhere)
        if (!TrySizeArg(context, arguments[0], PySR.Runtime_Number_Int_MaxDigitsNotInt32, out int tabsize, out var tabsizeError))
            return tabsizeError;

        // CPython unicode_expandtabs: a negative tabsize means 0
        // (tab deletion), it is accepted rather than rejected
        if (tabsize < 0)
            tabsize = 0;

        return PyStrObject.FromString(PyStrObject.ExpandTabsCore(self.Value, tabsize));
    }


    [PyMethod("ljust")]
    [AIGenerated]
    [PyFunctionParameters("width", "fillchar=' '", "/")]
    private static PyResult LJust(PyCallContext context, PyStrObject self, PyArguments arguments)
    {
        if (!TrySizeArg(context, arguments[0], PySR.Runtime_Number_Int_TooLargeForSsize, out int width, out var widthError))
            return widthError;

        string fillchar;
        if (arguments[1] is PyStrObject fillStr)
        {
            if (fillStr.PyLength is not 1)
                return PyResult.TypeError(PySR.Runtime_Str_FillCharLength);
            fillchar = fillStr.Value;
        }
        else
        {
            // CPython: the fillchar default applies by omission only; None
            // reports as NoneType like any other non-string
            return PyResult.TypeError(PySR.Runtime_Str_FillCharMustBeUnicode, arguments[1].PyType.Name);
        }
        if (width <= self.PyLength)
            return self;

        var pad = width - self.PyLength;
        var sb = new StringBuilder(self.Value.Length + pad * fillchar.Length);
        sb.Append(self.Value);
        PyStrObject.AppendRepeated(sb, fillchar, pad);
        return PyStrObject.FromString(sb.ToString());
    }

    [PyMethod("rjust")]
    [AIGenerated]
    [PyFunctionParameters("width", "fillchar=' '", "/")]
    private static PyResult RJust(PyCallContext context, PyStrObject self, PyArguments arguments)
    {
        if (!TrySizeArg(context, arguments[0], PySR.Runtime_Number_Int_TooLargeForSsize, out int width, out var widthError))
            return widthError;

        string fillchar;
        if (arguments[1] is PyStrObject fillStr)
        {
            if (fillStr.PyLength is not 1)
                return PyResult.TypeError(PySR.Runtime_Str_FillCharLength);
            fillchar = fillStr.Value;
        }
        else
        {
            // CPython: the fillchar default applies by omission only; None
            // reports as NoneType like any other non-string
            return PyResult.TypeError(PySR.Runtime_Str_FillCharMustBeUnicode, arguments[1].PyType.Name);
        }
        if (width <= self.PyLength)
            return self;

        var pad = width - self.PyLength;
        var sb = new StringBuilder(self.Value.Length + pad * fillchar.Length);
        PyStrObject.AppendRepeated(sb, fillchar, pad);
        sb.Append(self.Value);
        return PyStrObject.FromString(sb.ToString());
    }

    [PyMethod("rpartition")]
    [AIGenerated]
    [PyFunctionParameters("sep", "/")]
    private static PyResult RPartition(PyCallContext context, PyStrObject self, PyArguments arguments)
    {
        if (arguments[0] is not PyStrObject sepStr)
            return PyResult.TypeError(PySR.Runtime_Str_SepMustBeStr, arguments[0].PyType.Name);
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
            return PyResult.TypeError(PySR.Runtime_Str_PrefixArgMustBeStr, "removeprefix", arguments[0].PyType.Name);

        if (self.Value.StartsWith(prefixStr.Value, StringComparison.Ordinal))
            return PyStrObject.FromString(self.Value[prefixStr.Value.Length..]);

        return self;
    }

    [PyMethod("removesuffix")]
    [AIGenerated]
    [PyFunctionParameters("suffix", "/")]
    private static PyResult RemoveSuffix(PyCallContext context, PyStrObject self, PyArguments arguments)
    {
        if (arguments[0] is not PyStrObject suffixStr)
            return PyResult.TypeError(PySR.Runtime_Str_PrefixArgMustBeStr, "removesuffix", arguments[0].PyType.Name);

        if (self.Value.EndsWith(suffixStr.Value, StringComparison.Ordinal))
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

        var enumerator = self.EnumerateCodePoints();
        while (enumerator.MoveNext())
        {
            var codePoint = enumerator.Current;
            // CPython treats titlecase as upper in this state machine
            if (PyUnicodeData.IsUpper(codePoint) || PyUnicodeData.IsTitle(codePoint))
            {
                if (previousIsCased)
                    return PyBoolObject.False;
                previousIsCased = true;
                isCased = true;
            }
            else if (PyUnicodeData.IsLower(codePoint))
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
    private static PyResult IsDecimal(PyCallContext context, PyStrObject self, PyArguments arguments) =>
        PyStrObject.AllCodePoints(self, PyUnicodeData.IsDecimal);

    [PyMethod("isnumeric")]
    [AIGenerated]
    [PyFunctionParameters]
    private static PyResult IsNumeric(PyCallContext context, PyStrObject self, PyArguments arguments) =>
        PyStrObject.AllCodePoints(self, PyUnicodeData.IsNumeric);

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
        var enumerator = self.EnumerateCodePoints();
        while (enumerator.MoveNext())
        {
            if (!PyUnicodeData.IsPrintable(enumerator.Current))
                return PyBoolObject.False;
        }
        return PyBoolObject.True;
    }

    [PyMethod("encode")]
    [AIGenerated]
    [PyFunctionParameters("encoding='utf-8'", "errors='strict'")]
    private static PyResult Encode(PyCallContext context, PyStrObject self, PyArguments arguments)
    {
        // CPython 3.14's strict converters: neither parameter accepts None
        if (arguments[0] is not PyStrObject encodingArg)
            return PyResult.TypeError(PySR.Runtime_StrEncode_ArgMustBeStr, "encoding", arguments[0] is PyNoneObject ? "None" : arguments[0].PyType.Name);
        if (arguments[1] is not PyStrObject errorsArg)
            return PyResult.TypeError(PySR.Runtime_StrEncode_ArgMustBeStr, "errors", arguments[1] is PyNoneObject ? "None" : arguments[1].PyType.Name);

        return EncodeCore(context, self.Value, encodingArg.Value, errorsArg.Value);
    }

    // The str.encode core shared with the bytes()/bytearray()
    // string+encoding constructors: codec resolution, error handlers and
    // the bare utf-16/32 BOM all match unicode_encode here. .NET's encoders
    // substitute a replacement character instead of failing, so the whole
    // string goes through an encoder that reports the first unmappable
    // code point, and the CPython handler for errors= decides what happens
    // from there.
    internal static PyResult EncodeCore(PyCallContext context, string value, string encoding, string errors)
    {
        Encoding enc;
        try
        {
            enc = GetEncoding(encoding);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
        {
            return PyResult.LookupError(PySR.Runtime_Codec_UnknownEncoding, encoding);
        }

        var codec = PyCodecInfo.Classify(NormalizeEncodingName(encoding));
        var strict = CreateReportingEncoding(enc);
        var codePoints = PyStrObject.ToCodePointArray(value);
        var bytes = new List<byte>(value.Length);

        var position = 0;
        while (position < codePoints.Length)
        {
            var rest = value[PyStrObject.CodePointIndexToCharIndex(value, position)..];
            byte[] encoded;
            try
            {
                encoded = strict.GetBytes(rest);
            }
            catch (EncoderFallbackException ex)
            {
                // the failed call discards what it had encoded so far
                if (ex.Index > 0)
                    bytes.AddRange(strict.GetBytes(rest[..ex.Index]));
                var failed = position + PyStrObject.CharIndexToCodePointIndex(rest, ex.Index);
                var runEnd = codec.CollectsRun ? UnmappableRunEnd(strict, codePoints, failed) : failed + 1;
                var error = ApplyEncodeErrorHandler(codec, codePoints, failed, runEnd, errors, bytes, strict, value);
                if (error is not null)
                    return PyResult.FromException(error);
                position = runEnd;
                continue;
            }
            bytes.AddRange(encoded);
            break;
        }

        if (codec.EmitsBom)
            bytes.InsertRange(0, enc.GetPreamble());
        return PyBytesObject.MoveBytes([.. bytes]);
    }

    /// <summary>
    /// An encoding whose encoder reports the first code point it cannot
    /// represent instead of replacing it. .NET refuses the fallback objects
    /// for a few codecs; those keep their own substitution behavior.
    /// </summary>
    private static Encoding CreateReportingEncoding(Encoding enc)
    {
        try
        {
            return Encoding.GetEncoding(enc.CodePage, new EncoderExceptionFallback(), new DecoderExceptionFallback());
        }
        catch (ArgumentException)
        {
            return enc;
        }
    }

    // CPython extends an unmappable character over the run of code points
    // the encoder also cannot map, so the error covers the whole run
    private static int UnmappableRunEnd(Encoding strict, int[] codePoints, int start)
    {
        var end = start + 1;
        while (end < codePoints.Length && !CanEncode(strict, codePoints[end]))
            end++;
        return end;
    }

    private static bool CanEncode(Encoding strict, int codePoint)
    {
        try
        {
            strict.GetBytes(PyStrObject.FromCodePoint(codePoint).Value);
            return true;
        }
        catch (EncoderFallbackException)
        {
            return false;
        }
    }

    /// <summary>
    /// CPython's encode error handlers, applied to the run of unmappable
    /// code points [start, end). Returns the exception to raise, or null
    /// after the run's replacement has been appended to <paramref name="bytes"/>.
    /// </summary>
    private static PyExceptionObject? ApplyEncodeErrorHandler(
        PyCodecInfo.Codec codec, int[] codePoints, int start, int end, string errors,
        List<byte> bytes, Encoding strict, string value)
    {
        switch (errors)
        {
            case "ignore":
                return null;
            case "replace":
                return AppendEncodedText(bytes, strict, codec, value, start, end, new string('?', end - start));
            case "backslashreplace" or "xmlcharrefreplace" or "namereplace":
            {
                var text = new StringBuilder();
                for (var i = start; i < end; i++)
                    text.Append(EncodeErrorReplacement(errors, codePoints[i]));
                return AppendEncodedText(bytes, strict, codec, value, start, end, text.ToString());
            }
            case "surrogateescape":
                for (var i = start; i < end; i++)
                {
                    // only bytes escaped into U+DC80-U+DCFF are recoverable;
                    // anything else keeps the codec's own error
                    if (codePoints[i] is < 0xDC80 or > 0xDCFF)
                        return UnicodeEncodeError(codec, value, start, end);
                }
                // the handler hands back one byte per code point, which the
                // utf-16/utf-32 encoders reject as a partial unit
                if ((end - start) % codec.UnitSize is not 0)
                    return UnicodeEncodeError(codec, value, start, end);
                for (var i = start; i < end; i++)
                    bytes.Add((byte)(codePoints[i] - 0xDC00));
                return null;
            case "surrogatepass":
                if (!codec.SupportsSurrogatePass)
                    return UnicodeEncodeError(codec, value, start, end);
                for (var i = start; i < end; i++)
                {
                    if (codePoints[i] is < 0xD800 or > 0xDFFF)
                        return UnicodeEncodeError(codec, value, start, end);
                    AppendSurrogateUnit(bytes, codec.Kind, codePoints[i]);
                }
                return null;
            default:
                // CPython resolves the handler name at the first actual
                // encoding error (unicode_encode_call_errorhandler), so a
                // name it does not know is only rejected once one happens
                return errors is "strict" ? UnicodeEncodeError(codec, value, start, end) : UnknownErrorHandler(errors);
        }
    }

    // the replacement text is itself encoded through the codec, so it lands
    // in the codec's own byte order (utf-16 '?' is two bytes)
    private static PyExceptionObject? AppendEncodedText(
        List<byte> bytes, Encoding strict, PyCodecInfo.Codec codec, string value, int start, int end, string text)
    {
        try
        {
            bytes.AddRange(strict.GetBytes(text));
            return null;
        }
        catch (EncoderFallbackException)
        {
            return UnicodeEncodeError(codec, value, start, end);
        }
    }

    // CPython surrogatepass: the surrogate's own code unit in the codec's
    // byte order; the bare utf-16/utf-32 codecs write little-endian units
    private static void AppendSurrogateUnit(List<byte> bytes, PyCodecInfo.CodecKind kind, int codePoint)
    {
        switch (kind)
        {
            case PyCodecInfo.CodecKind.Utf8:
                bytes.Add((byte)(0xE0 | (codePoint >> 12)));
                bytes.Add((byte)(0x80 | ((codePoint >> 6) & 0x3F)));
                bytes.Add((byte)(0x80 | (codePoint & 0x3F)));
                break;
            case PyCodecInfo.CodecKind.Utf16 or PyCodecInfo.CodecKind.Utf16Le:
                bytes.Add((byte)codePoint);
                bytes.Add((byte)(codePoint >> 8));
                break;
            case PyCodecInfo.CodecKind.Utf16Be:
                bytes.Add((byte)(codePoint >> 8));
                bytes.Add((byte)codePoint);
                break;
            case PyCodecInfo.CodecKind.Utf32 or PyCodecInfo.CodecKind.Utf32Le:
                bytes.Add((byte)codePoint);
                bytes.Add((byte)(codePoint >> 8));
                bytes.Add((byte)(codePoint >> 16));
                bytes.Add((byte)(codePoint >> 24));
                break;
            default:
                bytes.Add((byte)(codePoint >> 24));
                bytes.Add((byte)(codePoint >> 16));
                bytes.Add((byte)(codePoint >> 8));
                bytes.Add((byte)codePoint);
                break;
        }
    }

    private static PyExceptionObject UnicodeEncodeError(PyCodecInfo.Codec codec, string value, int start, int end)
    {
        return PyExceptionObject.UnsafeCreate(PyUnicodeEncodeErrorObjectType.Shared,
            [
                PyStrObject.FromString(codec.ErrorName),
                PyStrObject.FromString(value),
                PyIntObject.FromInteger(start),
                PyIntObject.FromInteger(end),
                PyStrObject.FromString(codec.Reason),
            ]);
    }

    private static PyExceptionObject UnknownErrorHandler(string errors)
    {
        return PyResult.LookupError(PySR.Runtime_Codec_UnknownErrorHandlerName, errors).Exception!;
    }

    /// <summary>
    /// The replacement text of CPython's escape-style encode error handlers:
    /// xmlcharrefreplace ('&amp;#NNNN;'), backslashreplace ('\xNN' / '\uNNNN' /
    /// '\UNNNNNNNN') and namereplace ('\N{NAME}').
    /// </summary>
    private static string EncodeErrorReplacement(string errors, int codePoint)
    {
        switch (errors)
        {
            case "xmlcharrefreplace":
                return $"&#{codePoint};";
            case "namereplace":
                return GetNameReplacement(codePoint);
            default:
                return BackslashEscape(codePoint);
        }
    }

    private static string BackslashEscape(int codePoint) => codePoint switch
    {
        <= 0xFF => $"\\x{codePoint:x2}",
        <= 0xFFFF => $"\\u{codePoint:x4}",
        _ => $"\\U{codePoint:x8}",
    };

    /// <summary>
    /// Resolves a Python codec name to a .NET encoding. Python normalizes
    /// codec names by lowercasing and dropping non-alphanumeric characters,
    /// so 'utf-16-le' / 'utf-16be' / 'utf_16_le' all denote the same codec;
    /// .NET's Encoding.GetEncoding does not know the dashed 'utf-16-le' form.
    /// The Python alias families latin-1, mbcs, mac-roman and cpNNNN map to
    /// their .NET codepages.
    /// </summary>
    internal static Encoding GetEncoding(string name)
    {
        CodePagesEncoding.EnsureRegistered();
        var normalized = NormalizeEncodingName(name);
        switch (normalized)
        {
            case "utf8":
                return Encoding.UTF8;
            case "utf16le":
                return Encoding.Unicode;
            case "utf16be":
                return Encoding.BigEndianUnicode;
            case "utf32le":
                return Encoding.UTF32;
            case "utf32be":
                return new UTF32Encoding(bigEndian: true, byteOrderMark: false);
            // iso-8859-1 and every Python alias thereof ('latin-1' itself is
            // not a .NET name)
            case "latin1" or "latin" or "l1" or "8859" or "88591" or "iso8859" or "iso88591" or "iso885911987" or "isoir100" or "csisolatin1" or "ibm819" or "cp819":
                return Encoding.Latin1;
            case "macroman":
                return Encoding.GetEncoding(10000);
            case "mbcs":
                return Encoding.Default;
        }
        // The Windows codepages are primary Python codec names as cpNNNN
        // (and bare NNNN), while .NET only registers some of them by name
        if (normalized.StartsWith("cp", StringComparison.Ordinal) && int.TryParse(normalized[2..], out var cpCodepage))
            return Encoding.GetEncoding(cpCodepage);
        if (int.TryParse(normalized, out var codepage))
            return Encoding.GetEncoding(codepage);
        try
        {
            return Encoding.GetEncoding(name);
        }
        catch (ArgumentException)
        {
        }
        return Encoding.GetEncoding(normalized);
    }

    internal static string NormalizeEncodingName(string name)
    {
        var sb = new StringBuilder(name.Length);
        foreach (var c in name)
        {
            if (char.IsLetterOrDigit(c))
                sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }

    /// <summary>
    /// CPython namereplace: '\N{NAME}', or a hex escape for the code points
    /// without a name. .NET has no Unicode name lookup API, so embed the
    /// names for the range most relevant to ASCII-encoding namereplace
    /// (Basic Latin + Latin-1 Supplement, U+0000-U+00FF; generated from
    /// UnicodeData); beyond that the name is unknown here.
    /// </summary>
    private static string GetNameReplacement(int codePoint)
    {
        if (_unicodeNames.TryGetValue(codePoint, out var name))
            return $"\\N{{{name}}}";
        return BackslashEscape(codePoint);
    }

    private static readonly Dictionary<int, string> _unicodeNames = new()
    {
        [0x20] = "SPACE",
        [0x21] = "EXCLAMATION MARK",
        [0x22] = "QUOTATION MARK",
        [0x23] = "NUMBER SIGN",
        [0x24] = "DOLLAR SIGN",
        [0x25] = "PERCENT SIGN",
        [0x26] = "AMPERSAND",
        [0x27] = "APOSTROPHE",
        [0x28] = "LEFT PARENTHESIS",
        [0x29] = "RIGHT PARENTHESIS",
        [0x2A] = "ASTERISK",
        [0x2B] = "PLUS SIGN",
        [0x2C] = "COMMA",
        [0x2D] = "HYPHEN-MINUS",
        [0x2E] = "FULL STOP",
        [0x2F] = "SOLIDUS",
        [0x30] = "DIGIT ZERO",
        [0x31] = "DIGIT ONE",
        [0x32] = "DIGIT TWO",
        [0x33] = "DIGIT THREE",
        [0x34] = "DIGIT FOUR",
        [0x35] = "DIGIT FIVE",
        [0x36] = "DIGIT SIX",
        [0x37] = "DIGIT SEVEN",
        [0x38] = "DIGIT EIGHT",
        [0x39] = "DIGIT NINE",
        [0x3A] = "COLON",
        [0x3B] = "SEMICOLON",
        [0x3C] = "LESS-THAN SIGN",
        [0x3D] = "EQUALS SIGN",
        [0x3E] = "GREATER-THAN SIGN",
        [0x3F] = "QUESTION MARK",
        [0x40] = "COMMERCIAL AT",
        [0x41] = "LATIN CAPITAL LETTER A",
        [0x42] = "LATIN CAPITAL LETTER B",
        [0x43] = "LATIN CAPITAL LETTER C",
        [0x44] = "LATIN CAPITAL LETTER D",
        [0x45] = "LATIN CAPITAL LETTER E",
        [0x46] = "LATIN CAPITAL LETTER F",
        [0x47] = "LATIN CAPITAL LETTER G",
        [0x48] = "LATIN CAPITAL LETTER H",
        [0x49] = "LATIN CAPITAL LETTER I",
        [0x4A] = "LATIN CAPITAL LETTER J",
        [0x4B] = "LATIN CAPITAL LETTER K",
        [0x4C] = "LATIN CAPITAL LETTER L",
        [0x4D] = "LATIN CAPITAL LETTER M",
        [0x4E] = "LATIN CAPITAL LETTER N",
        [0x4F] = "LATIN CAPITAL LETTER O",
        [0x50] = "LATIN CAPITAL LETTER P",
        [0x51] = "LATIN CAPITAL LETTER Q",
        [0x52] = "LATIN CAPITAL LETTER R",
        [0x53] = "LATIN CAPITAL LETTER S",
        [0x54] = "LATIN CAPITAL LETTER T",
        [0x55] = "LATIN CAPITAL LETTER U",
        [0x56] = "LATIN CAPITAL LETTER V",
        [0x57] = "LATIN CAPITAL LETTER W",
        [0x58] = "LATIN CAPITAL LETTER X",
        [0x59] = "LATIN CAPITAL LETTER Y",
        [0x5A] = "LATIN CAPITAL LETTER Z",
        [0x5B] = "LEFT SQUARE BRACKET",
        [0x5C] = "REVERSE SOLIDUS",
        [0x5D] = "RIGHT SQUARE BRACKET",
        [0x5E] = "CIRCUMFLEX ACCENT",
        [0x5F] = "LOW LINE",
        [0x60] = "GRAVE ACCENT",
        [0x61] = "LATIN SMALL LETTER A",
        [0x62] = "LATIN SMALL LETTER B",
        [0x63] = "LATIN SMALL LETTER C",
        [0x64] = "LATIN SMALL LETTER D",
        [0x65] = "LATIN SMALL LETTER E",
        [0x66] = "LATIN SMALL LETTER F",
        [0x67] = "LATIN SMALL LETTER G",
        [0x68] = "LATIN SMALL LETTER H",
        [0x69] = "LATIN SMALL LETTER I",
        [0x6A] = "LATIN SMALL LETTER J",
        [0x6B] = "LATIN SMALL LETTER K",
        [0x6C] = "LATIN SMALL LETTER L",
        [0x6D] = "LATIN SMALL LETTER M",
        [0x6E] = "LATIN SMALL LETTER N",
        [0x6F] = "LATIN SMALL LETTER O",
        [0x70] = "LATIN SMALL LETTER P",
        [0x71] = "LATIN SMALL LETTER Q",
        [0x72] = "LATIN SMALL LETTER R",
        [0x73] = "LATIN SMALL LETTER S",
        [0x74] = "LATIN SMALL LETTER T",
        [0x75] = "LATIN SMALL LETTER U",
        [0x76] = "LATIN SMALL LETTER V",
        [0x77] = "LATIN SMALL LETTER W",
        [0x78] = "LATIN SMALL LETTER X",
        [0x79] = "LATIN SMALL LETTER Y",
        [0x7A] = "LATIN SMALL LETTER Z",
        [0x7B] = "LEFT CURLY BRACKET",
        [0x7C] = "VERTICAL LINE",
        [0x7D] = "RIGHT CURLY BRACKET",
        [0x7E] = "TILDE",
        [0xA0] = "NO-BREAK SPACE",
        [0xA1] = "INVERTED EXCLAMATION MARK",
        [0xA2] = "CENT SIGN",
        [0xA3] = "POUND SIGN",
        [0xA4] = "CURRENCY SIGN",
        [0xA5] = "YEN SIGN",
        [0xA6] = "BROKEN BAR",
        [0xA7] = "SECTION SIGN",
        [0xA8] = "DIAERESIS",
        [0xA9] = "COPYRIGHT SIGN",
        [0xAA] = "FEMININE ORDINAL INDICATOR",
        [0xAB] = "LEFT-POINTING DOUBLE ANGLE QUOTATION MARK",
        [0xAC] = "NOT SIGN",
        [0xAD] = "SOFT HYPHEN",
        [0xAE] = "REGISTERED SIGN",
        [0xAF] = "MACRON",
        [0xB0] = "DEGREE SIGN",
        [0xB1] = "PLUS-MINUS SIGN",
        [0xB2] = "SUPERSCRIPT TWO",
        [0xB3] = "SUPERSCRIPT THREE",
        [0xB4] = "ACUTE ACCENT",
        [0xB5] = "MICRO SIGN",
        [0xB6] = "PILCROW SIGN",
        [0xB7] = "MIDDLE DOT",
        [0xB8] = "CEDILLA",
        [0xB9] = "SUPERSCRIPT ONE",
        [0xBA] = "MASCULINE ORDINAL INDICATOR",
        [0xBB] = "RIGHT-POINTING DOUBLE ANGLE QUOTATION MARK",
        [0xBC] = "VULGAR FRACTION ONE QUARTER",
        [0xBD] = "VULGAR FRACTION ONE HALF",
        [0xBE] = "VULGAR FRACTION THREE QUARTERS",
        [0xBF] = "INVERTED QUESTION MARK",
        [0xC0] = "LATIN CAPITAL LETTER A WITH GRAVE",
        [0xC1] = "LATIN CAPITAL LETTER A WITH ACUTE",
        [0xC2] = "LATIN CAPITAL LETTER A WITH CIRCUMFLEX",
        [0xC3] = "LATIN CAPITAL LETTER A WITH TILDE",
        [0xC4] = "LATIN CAPITAL LETTER A WITH DIAERESIS",
        [0xC5] = "LATIN CAPITAL LETTER A WITH RING ABOVE",
        [0xC6] = "LATIN CAPITAL LETTER AE",
        [0xC7] = "LATIN CAPITAL LETTER C WITH CEDILLA",
        [0xC8] = "LATIN CAPITAL LETTER E WITH GRAVE",
        [0xC9] = "LATIN CAPITAL LETTER E WITH ACUTE",
        [0xCA] = "LATIN CAPITAL LETTER E WITH CIRCUMFLEX",
        [0xCB] = "LATIN CAPITAL LETTER E WITH DIAERESIS",
        [0xCC] = "LATIN CAPITAL LETTER I WITH GRAVE",
        [0xCD] = "LATIN CAPITAL LETTER I WITH ACUTE",
        [0xCE] = "LATIN CAPITAL LETTER I WITH CIRCUMFLEX",
        [0xCF] = "LATIN CAPITAL LETTER I WITH DIAERESIS",
        [0xD0] = "LATIN CAPITAL LETTER ETH",
        [0xD1] = "LATIN CAPITAL LETTER N WITH TILDE",
        [0xD2] = "LATIN CAPITAL LETTER O WITH GRAVE",
        [0xD3] = "LATIN CAPITAL LETTER O WITH ACUTE",
        [0xD4] = "LATIN CAPITAL LETTER O WITH CIRCUMFLEX",
        [0xD5] = "LATIN CAPITAL LETTER O WITH TILDE",
        [0xD6] = "LATIN CAPITAL LETTER O WITH DIAERESIS",
        [0xD7] = "MULTIPLICATION SIGN",
        [0xD8] = "LATIN CAPITAL LETTER O WITH STROKE",
        [0xD9] = "LATIN CAPITAL LETTER U WITH GRAVE",
        [0xDA] = "LATIN CAPITAL LETTER U WITH ACUTE",
        [0xDB] = "LATIN CAPITAL LETTER U WITH CIRCUMFLEX",
        [0xDC] = "LATIN CAPITAL LETTER U WITH DIAERESIS",
        [0xDD] = "LATIN CAPITAL LETTER Y WITH ACUTE",
        [0xDE] = "LATIN CAPITAL LETTER THORN",
        [0xDF] = "LATIN SMALL LETTER SHARP S",
        [0xE0] = "LATIN SMALL LETTER A WITH GRAVE",
        [0xE1] = "LATIN SMALL LETTER A WITH ACUTE",
        [0xE2] = "LATIN SMALL LETTER A WITH CIRCUMFLEX",
        [0xE3] = "LATIN SMALL LETTER A WITH TILDE",
        [0xE4] = "LATIN SMALL LETTER A WITH DIAERESIS",
        [0xE5] = "LATIN SMALL LETTER A WITH RING ABOVE",
        [0xE6] = "LATIN SMALL LETTER AE",
        [0xE7] = "LATIN SMALL LETTER C WITH CEDILLA",
        [0xE8] = "LATIN SMALL LETTER E WITH GRAVE",
        [0xE9] = "LATIN SMALL LETTER E WITH ACUTE",
        [0xEA] = "LATIN SMALL LETTER E WITH CIRCUMFLEX",
        [0xEB] = "LATIN SMALL LETTER E WITH DIAERESIS",
        [0xEC] = "LATIN SMALL LETTER I WITH GRAVE",
        [0xED] = "LATIN SMALL LETTER I WITH ACUTE",
        [0xEE] = "LATIN SMALL LETTER I WITH CIRCUMFLEX",
        [0xEF] = "LATIN SMALL LETTER I WITH DIAERESIS",
        [0xF0] = "LATIN SMALL LETTER ETH",
        [0xF1] = "LATIN SMALL LETTER N WITH TILDE",
        [0xF2] = "LATIN SMALL LETTER O WITH GRAVE",
        [0xF3] = "LATIN SMALL LETTER O WITH ACUTE",
        [0xF4] = "LATIN SMALL LETTER O WITH CIRCUMFLEX",
        [0xF5] = "LATIN SMALL LETTER O WITH TILDE",
        [0xF6] = "LATIN SMALL LETTER O WITH DIAERESIS",
        [0xF7] = "DIVISION SIGN",
        [0xF8] = "LATIN SMALL LETTER O WITH STROKE",
        [0xF9] = "LATIN SMALL LETTER U WITH GRAVE",
        [0xFA] = "LATIN SMALL LETTER U WITH ACUTE",
        [0xFB] = "LATIN SMALL LETTER U WITH CIRCUMFLEX",
        [0xFC] = "LATIN SMALL LETTER U WITH DIAERESIS",
        [0xFD] = "LATIN SMALL LETTER Y WITH ACUTE",
        [0xFE] = "LATIN SMALL LETTER THORN",
        [0xFF] = "LATIN SMALL LETTER Y WITH DIAERESIS",
    };

    protected override PyResult Repr(PyCallContext context, PyStrObject self)
    {
        return PyStrObject.FromString(self.Repr());
    }
    protected override PyResult Str(PyCallContext context, PyStrObject self)
    {
        // CPython unicode_result_unchanged: an exact str returns itself, a
        // subclass instance converts to a fresh exact str
        if (self.PyType == PyStrObjectType.Shared)
            return self;
        return PyStrObject.FromString(self.Value);
    }

    protected override PyResult Hash(PyCallContext context, PyStrObject self)
    {
        return PyIntObject.FromInteger(self.GetHashCode());
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

            var codePoints = new List<int>(self.PyLength);
            var enumerator = self.EnumerateCodePoints();
            while (enumerator.MoveNext())
                codePoints.Add(enumerator.Current);

            var sb = new StringBuilder(length);
            for (int i = start, ri = 0; ri < length; i += step, ri++)
                PyStrObject.AppendCodePoint(sb, codePoints[i]);
            return PyStrObject.FromString(sb.ToString());
        }

        var result = PySpecialMethods.Index(context, item);
        if (result.IsError)
            return result;
        if (!result.Value.IsInt32)
            return PyResult.IndexError(PySR.Runtime_String_IndexOutOfRange);
        var index = result.Value.Int32Value;
        index = PyUtils.MapIndex(index, self.PyLength);
        if (index < 0 || index >= self.PyLength)
            return PyResult.IndexError(PySR.Runtime_String_IndexOutOfRange);
        return PyStrObject.FromCodePoint(self.PyCharAt(index));
    }
    // CPython's reflected wrappers cover as_number and sq_repeat slots
    // only; sq_concat has no reflected variant (str.__radd__ does not
    // exist). __mul__ keeps its __rmul__ entry with the non-swapping
    // self*value order of wrap_indexargfunc.
    protected override bool SynthesizeReflectedAdd => false;
    protected override bool ReflectedMulSwapsOperands => false;

    protected override PyResult Add(PyCallContext context, PyStrObject self, PyObject other)
    {
        if (other is PyStrObject strObj)
            return PyStrObject.FromString(self.Value + strObj.Value);
        // CPython's str has no nb_add: the right operand's reflected
        // __radd__ runs first, and the concat TypeError is the last resort
        // (the sq_concat fallback) when it declines or does not exist.
        if (other.PyType.Slots.RAdd is not null)
        {
            var reflected = other.PyType.Slots.RAdd(context, other, self);
            if (!reflected.IsNotImplemented)
                return reflected;
        }
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
        // CPython unicode_compare order; string.CompareTo would use culture
        // rules ('a' < 'B' incorrectly) and CompareOrdinal code units.
        return PyBoolObject.FromBoolean(PyStrObject.CompareCodePoints(self.Value, strObj.Value) < 0);
    }
    protected override PyResult Le(PyCallContext context, PyStrObject self, PyObject other)
    {
        if (other is not PyStrObject strObj)
            return PyNotImplementedObject.NotImplemented;
        return PyBoolObject.FromBoolean(PyStrObject.CompareCodePoints(self.Value, strObj.Value) <= 0);
    }
    protected override PyResult Gt(PyCallContext context, PyStrObject self, PyObject other)
    {
        if (other is not PyStrObject strObj)
            return PyNotImplementedObject.NotImplemented;
        return PyBoolObject.FromBoolean(PyStrObject.CompareCodePoints(self.Value, strObj.Value) > 0);
    }
    protected override PyResult Ge(PyCallContext context, PyStrObject self, PyObject other)
    {
        if (other is not PyStrObject strObj)
            return PyNotImplementedObject.NotImplemented;
        return PyBoolObject.FromBoolean(PyStrObject.CompareCodePoints(self.Value, strObj.Value) >= 0);
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
            return PyResult.OverflowError(PySR.Runtime_Index_CannotFitInt);
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

        // PyUnicode_Format: a tuple is an argument list; any other object
        // with __getitem__ (except str) is a mapping candidate, and the
        // rest is a single value consumed by exactly one conversion
        IReadOnlyList<PyObject>? listArgs;
        PyObject singleArg;
        PyObject? mapping;
        if (other is PyTupleObject tuple)
        {
            listArgs = tuple;
            singleArg = null!;
            mapping = null;
        }
        else
        {
            listArgs = null;
            singleArg = other;
            mapping = other is PyStrObject || other.PyType.Slots.GetItem is null ? null : other;
        }

        // getnextarg: the single-value mode starts at -2 and yields its
        // value exactly once before reporting missing arguments
        int argIndex = listArgs is not null ? 0 : -2;

        PyResult<PyObject> NextArg()
        {
            if (listArgs is not null)
            {
                if (argIndex < listArgs.Count)
                    return listArgs[argIndex++];
            }
            else if (argIndex < -1)
            {
                argIndex++;
                return singleArg;
            }
            return PyResult.TypeError("not enough arguments for format string");
        }

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

            // Handle dict-based: %(name)format
            if (formatStr[i] is '(')
            {
                if (mapping is null)
                    return PyResult.TypeError("format requires a mapping");

                // CPython skips balanced parentheses inside the key
                int keyStart = i + 1;
                int depth = 1;
                int closeParen = -1;
                for (int j = keyStart; j < formatStr.Length; j++)
                {
                    if (formatStr[j] is '(')
                    {
                        depth++;
                    }
                    else if (formatStr[j] is ')')
                    {
                        depth--;
                        if (depth is 0)
                        {
                            closeParen = j;
                            break;
                        }
                    }
                }
                if (closeParen < 0)
                    return PyResult.ValueError("incomplete format key");

                string key = formatStr[keyStart..closeParen];
                i = closeParen + 1;

                var itemResult = PySpecialMethods.GetItem(context, mapping, PyStrObject.FromString(key));
                if (itemResult.IsError)
                    return itemResult;
                // the mapping value becomes the single-value argument source
                singleArg = itemResult.Value;
                listArgs = null;
                argIndex = -2;
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
                // the width comes from the argument tuple before the value
                var widthResult = NextArg();
                if (widthResult.IsError)
                    return widthResult;
                if (widthResult.Value is not PyIntObject widthObj)
                    return PyResult.TypeError("* wants int");
                width = widthObj.Int32Value;
                // CPython: a negative width flips to left alignment
                if (width < 0)
                {
                    flagLeftAlign = true;
                    width = -width;
                }
                i++;
            }
            else
            {
                var widthStr = string.Empty;
                while (i < formatStr.Length && char.IsDigit(formatStr[i]))
                    widthStr += formatStr[i++];
                if (widthStr.Length > 0)
                    width = int.Parse(widthStr, CultureInfo.InvariantCulture);
            }

            // Parse precision
            int precision = -1;
            if (i < formatStr.Length && formatStr[i] is '.')
            {
                i++;
                if (i < formatStr.Length && formatStr[i] is '*')
                {
                    var precResult = NextArg();
                    if (precResult.IsError)
                        return precResult;
                    if (precResult.Value is not PyIntObject precObj)
                        return PyResult.TypeError("* wants int");
                    precision = precObj.Int32Value;
                    // CPython: a negative dynamic precision clamps to zero
                    if (precision < 0)
                        precision = 0;
                    i++;
                }
                else
                {
                    var precStr = string.Empty;
                    while (i < formatStr.Length && char.IsDigit(formatStr[i]))
                        precStr += formatStr[i++];
                    // CPython sets the precision to 0 as soon as the dot
                    // is consumed, so an empty precision field is valid
                    precision = precStr.Length > 0 ? int.Parse(precStr, CultureInfo.InvariantCulture) : 0;
                }
            }

            // Skip 'h', 'l', 'L' length modifiers (C style, ignored by Python)
            if (i < formatStr.Length && (formatStr[i] is 'h' or 'l' or 'L'))
                i++;

            // Parse format type
            if (i >= formatStr.Length)
                return PyResult.ValueError("incomplete format");
            char fmtType = formatStr[i];

            // the value itself is taken only after the whole spec (including
            // any '*' width/precision) has consumed its arguments, matching
            // the CPython ordering
            var valueResult = NextArg();
            if (valueResult.IsError)
                return valueResult;
            PyObject value = valueResult.Value;

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
                        if (precision >= 0)
                            formatted = PyStrObject.CodePointPrefix(formatted, precision);
                        break;
                    }
                case 'r':
                    {
                        var reprResult = PySpecialMethods.Repr(context, value);
                        if (reprResult.IsError)
                            return reprResult;
                        formatted = reprResult.Value.Value;
                        if (precision >= 0)
                            formatted = PyStrObject.CodePointPrefix(formatted, precision);
                        break;
                    }
                case 'a':
                    {
                        var asciiResult = PyBuiltinFunctions.Ascii.Call(context, [value]);
                        if (asciiResult.IsError)
                            return asciiResult;
                        formatted = ((PyStrObject)asciiResult.Value).Value;
                        if (precision >= 0)
                            formatted = PyStrObject.CodePointPrefix(formatted, precision);
                        break;
                    }
                case 'd':
                case 'i':
                case 'u':
                    {
                        PyIntObject intObj;
                        if (value is PyIntObject directInt)
                        {
                            intObj = directInt;
                        }
                        else
                        {
                            // PyNumber_Long: any number converts (__int__ or
                            // __index__, a float truncating toward zero);
                            // other types get the %-type specific TypeError
                            var intResult = PySpecialMethods.Int(context, value);
                            if (intResult.IsError)
                                return PyResult.TypeError(PySR.Runtime_Str_PctFormatRealNumberRequired, fmtType, value.PyType.FullName);
                            intObj = intResult.Value;
                        }
                        var intVal = intObj.Value;
                        string intStr;
                        if (precision >= 0)
                            intStr = BigInteger.Abs(intVal).ToString($"D{precision}", CultureInfo.InvariantCulture);
                        else
                            intStr = BigInteger.Abs(intVal).ToString(CultureInfo.InvariantCulture);
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
                            return PyResult.TypeError(PySR.Runtime_Str_PctFormatIntegerRequired, fmtType, value.PyType.FullName);
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
                            return PyResult.TypeError(PySR.Runtime_Str_PctFormatIntegerRequired, fmtType, value.PyType.FullName);
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
                            return PyResult.TypeError(PySR.Runtime_Str_PctFormatIntegerRequired, fmtType, value.PyType.FullName);
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

            // Apply width and alignment; CPython unicode_format pads the
            // code point sequence, so an astral character counts once
            var formattedLength = PyStrObject.CountCodePoints(formatted);
            if (width > formattedLength)
            {
                var pad = width - formattedLength;
                // CPython zero-fills only numeric conversions: F_ZERO is gated
                // on arg->sign in unicode_format_arg_output, which s/r/a/c never set
                bool zeroPad = flagZeroPad && !flagLeftAlign
                    && fmtType is 'd' or 'i' or 'u' or 'o' or 'x' or 'X'
                               or 'e' or 'E' or 'f' or 'F' or 'g' or 'G';
                char padChar = zeroPad ? '0' : ' ';
                if (flagLeftAlign)
                {
                    formatted += new string(padChar, pad);
                }
                else if (zeroPad && formatted.Length > 0)
                {
                    // Zero-padding: zeros go after any sign and any
                    // '0x'/'0o'/'0X' alternate prefix ('%#08x' % -16 -> '-0x00010').
                    string head = string.Empty;
                    string tail = formatted;
                    if (tail[0] is '+' or '-' or ' ')
                    {
                        head = tail[..1];
                        tail = tail[1..];
                    }
                    if (tail.Length >= 2 && tail[0] is '0' && tail[1] is 'x' or 'o' or 'X')
                    {
                        head += tail[..2];
                        tail = tail[2..];
                    }
                    formatted = head + new string(padChar, pad) + tail;
                }
                else
                {
                    formatted = new string(padChar, pad) + formatted;
                }
            }

            sb.Append(formatted);
        }

        // Check for unused arguments: leftover tuple entries, or an
        // unconsumed single value that is not a mapping candidate
        if (listArgs is not null)
        {
            if (argIndex < listArgs.Count)
                return PyResult.TypeError("not all arguments converted during string formatting");
        }
        else if (mapping is null && argIndex < -1)
        {
            return PyResult.TypeError("not all arguments converted during string formatting");
        }

        return PyStrObject.FromString(sb.ToString());
    }

    protected override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        // CPython str_new: a missing object is the empty string regardless
        // of encoding; without encoding/errors the object converts via
        // PyObject_Str, otherwise a bytes-like object decodes
        if (args.Count + kwargs.Count > 3)
            return PyResult.TypeError(PySR.Runtime_Str_ExpectedAtMostThree, args.Count + kwargs.Count);

        PyObject? source = args.Count > 0 ? args[0] : null;
        PyObject? encoding = args.Count > 1 ? args[1] : null;
        PyObject? errors = args.Count > 2 ? args[2] : null;
        foreach (var (name, value) in kwargs)
        {
            switch (name)
            {
                case "object":
                    if (source is not null)
                        return PyResult.TypeError(PySR.Runtime_Codec_MultipleValues, "str", name, 1);
                    source = value;
                    break;
                case "encoding":
                    if (encoding is not null)
                        return PyResult.TypeError(PySR.Runtime_Codec_MultipleValues, "str", name, 2);
                    encoding = value;
                    break;
                case "errors":
                    if (errors is not null)
                        return PyResult.TypeError(PySR.Runtime_Codec_MultipleValues, "str", name, 3);
                    errors = value;
                    break;
                default:
                    return PyResult.TypeError(PySR.Runtime_Str_UnexpectedKeyword, name);
            }
        }

        PyResult result;
        if (source is null)
        {
            result = PyStrObject.Empty;
        }
        else
        {
            // the clinic 'str' converters reject non-str before any conversion;
            // None reports as "NoneType" here (unlike bytes())
            if (encoding is not null and not PyStrObject)
                return PyResult.TypeError(PySR.Runtime_Str_ArgMustBeStr, "encoding", encoding.PyType.Name);
            if (errors is not null and not PyStrObject)
                return PyResult.TypeError(PySR.Runtime_Str_ArgMustBeStr, "errors", errors.PyType.Name);

            if (encoding is null && errors is null)
            {
                result = PySpecialMethods.Str(context, source);
                if (result.IsError)
                    return result;
            }
            else
            {
                if (!PyBytesObjectType.TryGetBytesLikeSpan(source, out var data))
                    return PyResult.TypeError(source is PyStrObject ? PySR.Runtime_Str_DecodingStrNotSupported : PySR.Runtime_Str_DecodingNeedBytesLike, source.PyType.FullName);

                var encodingName = encoding is PyStrObject encStr ? encStr.Value : "utf-8";
                var errorsName = errors is PyStrObject errStr ? errStr.Value : "strict";
                result = PyBytesObjectType.DecodeCore(context, data, encodingName, errorsName, source);
                if (result.IsError)
                    return result;
            }
        }

        // CPython str_new hands subtypes a fresh copy (unicode_subtype_new):
        // the empty string, character-pool entries and PyObject_Str results
        // are shared, so retagging one in place would retype it everywhere
        var strObj = (PyStrObject)result.Value!;
        if (strObj.PyType != cls)
        {
            strObj = ReferenceEquals(cls, this)
                ? PyStrObject.FromString(strObj.Value)
                : PyStrObject.FromStringNoCache(strObj.Value);
            strObj._pyType = cls;
        }
        return strObj;
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