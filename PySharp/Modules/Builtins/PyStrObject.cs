using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;
using PySharp.Runtime.Unicode;
using PySharp.Utility;
using System.Collections.Frozen;
using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Runtime.InteropServices;
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
    // CPython's str is a code-point sequence where adjacent lone surrogates
    // stay separate ('\ud800\udc00' is two code points); .NET stores UTF-16
    // units, and those two units form a legal pair indistinguishable from
    // '\U00010000'. A producer that knows the code-point sequence (concat,
    // join, %-format, literals, ...) keeps it here whenever the sequence
    // contains an adjacent lone high+low surrogate; null everywhere else,
    // where the plain UTF-16 reading is already the correct sequence.
    private readonly int[]? _codePoints;
    public int PyLength
    {
        get
        {
            if (field is not -1)
                return field;
            return field = _codePoints?.Length ?? CountCodePoints(Value);
        }
    }

    // 0 = not scanned yet, 1 = free of surrogate pairs, 2 = contains surrogate pairs
    private int _surrogatePairState;

    /// <summary>
    /// True when the payload has no surrogate pair, so code-point indices and
    /// UTF-16 char indices coincide and convert in O(1); lazily computed with
    /// one full scan per instance. CPython stores code points natively
    /// (PEP 393) while .NET stores UTF-16 units — only astral content forces
    /// the O(n) index walks.
    /// </summary>
    internal bool IsSurrogatePairFree
    {
        get
        {
            // the authoritative sequence and the UTF-16 indices never agree
            // on a lone-surrogate string: it always takes the array path
            if (_codePoints is not null)
                return false;
            var state = _surrogatePairState;
            if (state is 0)
                _surrogatePairState = state = ContainsSurrogatePair(Value) ? 2 : 1;
            return state is 1;
        }
    }

    // a lone surrogate is itself a code point of width 1, so only a high unit
    // directly followed by a low one breaks the index identity
    internal static bool ContainsSurrogatePair(ReadOnlySpan<char> value)
    {
        if (!value.ContainsAnyInRange('\uD800', '\uDFFF'))
            return false;
        for (int i = 0; i < value.Length - 1; i++)
        {
            if (char.IsHighSurrogate(value[i]) && char.IsLowSurrogate(value[i + 1]))
                return true;
        }
        return false;
    }

    public static PyStrObject Empty { get; } = new PyStrObject(string.Empty);
    public override PyTypeObject DefaultPyType => PyStrObjectType.Shared;
    private PyStrObject(string value)
    {
        Value = value;
        PyLength = -1;
    }

    private PyStrObject(string value, int[] codePoints)
    {
        Value = value;
        _codePoints = codePoints;
        PyLength = -1;
    }
    internal static PyStrObject FromLiteral(ReadOnlySpan<char> literal)
    {
        if (!PyStrConverter.TryFromLiteralToString(literal, out var str, out var codePoints, out _))
            throw new ArgumentException($"failed to parse {literal}");
        return codePoints is null ? FromString(str) : FromCodePoints(codePoints);
    }
    internal static PyStrObject FromLiteralContent(ReadOnlySpan<char> text)
    {
        if (!PyStrConverter.TryFromTextToString(text, out var str, out var codePoints, out _))
            throw new ArgumentException($"failed to parse {text}");
        return codePoints is null ? FromString(str) : FromCodePoints(codePoints);
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

    internal bool HasAmbiguousCodePoints => _codePoints is not null;

    // the string's code points: the authoritative sequence when present,
    // otherwise the UTF-16 reading (which is the correct sequence for every
    // string without adjacent lone surrogates)
    internal int[] GetCodePointArray() => _codePoints ?? ToCodePointArray(Value);

    // the authoritative sequence itself, or null when the UTF-16 reading is
    // already correct — consumers that only need to differ stay allocation-free
    internal int[]? GetCodePointArrayOrNull() => _codePoints;

    internal static bool ContainsAdjacentLoneSurrogates(ReadOnlySpan<int> codePoints)
    {
        for (var i = 0; i + 1 < codePoints.Length; i++)
        {
            if (codePoints[i] is >= 0xD800 and <= 0xDBFF && codePoints[i + 1] is >= 0xDC00 and <= 0xDFFF)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Build a str from a producer-known code-point sequence. Adjacent lone
    /// surrogates must survive as separate code points, which a UTF-16
    /// payload cannot express on its own — such strings carry the sequence
    /// as their authoritative view; every other sequence maps to a plain
    /// string through the pooled factory.
    /// </summary>
    internal static PyStrObject FromCodePoints(int[] codePoints)
    {
        var builder = new StringBuilder(codePoints.Length);
        foreach (var codePoint in codePoints)
            AppendCodePoint(builder, codePoint);
        if (!ContainsAdjacentLoneSurrogates(codePoints))
            return FromString(builder.ToString());
        return new PyStrObject(builder.ToString(), codePoints);
    }

    /// <summary>
    /// A fresh exact str with the same payload and code-point view, the
    /// conversion <see cref="FromString"/> cannot express for a string
    /// carrying lone surrogates (subclass-to-exact-str and friends).
    /// </summary>
    internal PyStrObject ToExactStr() => _codePoints is null ? FromString(Value) : new PyStrObject(Value, _codePoints);

    internal string Repr()
    {
        return PyStrConverter.FromStringToLiteral(Value, _codePoints);
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

    /// <summary>
    /// The per-instance code-point enumeration: strings carrying lone
    /// surrogates step over their authoritative sequence, everything else
    /// over the UTF-16 units (where pairs mean astral characters).
    /// </summary>
    internal CodePointsViewEnumerator EnumerateCodePoints() => new(Value, _codePoints);

    internal ref struct CodePointsViewEnumerator(ReadOnlySpan<char> value, int[]? codePoints)
    {
        // not readonly: MoveNext advances the wrapped enumerator in place —
        // a readonly field would silently run on defensive copies and spin
        private CodePointEnumerator _utf16 = new(value);
        private readonly int[]? _codePoints = codePoints;
        private int _index = -1;

        public int Current { get; private set; } = -1;

        public bool MoveNext()
        {
            if (_codePoints is not null)
            {
                var next = _index + 1;
                if (next >= _codePoints.Length)
                    return false;
                _index = next;
                Current = _codePoints[next];
                return true;
            }
            if (!_utf16.MoveNext())
                return false;
            Current = _utf16.Current;
            return true;
        }
    }

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

    // callers clamp the index into [0, PyLength) before calling
    internal int PyCharAt(int index)
    {
        if (_codePoints is not null)
            return _codePoints[index];
        if (IsSurrogatePairFree)
            return Value[index];
        return CodePointAt(Value, index);
    }

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

    /// <summary>
    /// The %-formatting precision cut over the code-point view: a prefix of
    /// an ambiguous string may keep adjacent lone surrogates.
    /// </summary>
    internal PyStrObject CodePointPrefixStr(int count)
    {
        if (_codePoints is not null)
            return _codePoints.Length <= count ? this : SubstringByCodePointRangeStr(0, count);
        return FromString(CodePointPrefix(Value, count));
    }

    /// <summary>Convert a code-point index to a char index in the string.</summary>
    internal int CodePointIndexToCharIndex(int codePointIndex)
    {
        if (_codePoints is not null)
        {
            // sum the UTF-16 widths of the authoritative sequence's prefix
            codePointIndex = Math.Clamp(codePointIndex, 0, _codePoints.Length);
            var chars = 0;
            for (var i = 0; i < codePointIndex; i++)
                chars += _codePoints[i] >= 0x10000 ? 2 : 1;
            return chars;
        }
        // without surrogate pairs the indices coincide; clamp keeps the
        // below-zero and past-end contract of the scanning path
        if (IsSurrogatePairFree)
            return Math.Clamp(codePointIndex, 0, Value.Length);
        return CodePointIndexToCharIndex(Value, codePointIndex);
    }

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
    internal int CharIndexToCodePointIndex(int charIndex)
    {
        if (_codePoints is not null)
        {
            // count the authoritative code points fully inside [0, charIndex)
            var count = 0;
            var chars = 0;
            foreach (var codePoint in _codePoints)
            {
                if (chars >= charIndex)
                    break;
                chars += codePoint >= 0x10000 ? 2 : 1;
                count++;
            }
            return count;
        }
        if (IsSurrogatePairFree)
            return Math.Clamp(charIndex, 0, Value.Length);
        return CharIndexToCodePointIndex(Value, charIndex);
    }

    internal static int CharIndexToCodePointIndex(string value, int charIndex)
    {
        int count = 0;
        for (int i = 0; i < value.Length && i < charIndex; i += CharWidthAt(value, i))
            count++;
        return count;
    }

    /// <summary>
    /// The code points of the range [start, end) as a fresh string. Callers
    /// that feed the result back into a str must use
    /// <see cref="SubstringByCodePointRangeStr"/> instead: a range may keep
    /// adjacent lone surrogates, which <see cref="FromString(string)"/> would
    /// silently pair.
    /// </summary>
    internal string SubstringByCodePointRange(int start, int end)
    {
        if (_codePoints is not null)
        {
            start = Math.Clamp(start, 0, _codePoints.Length);
            end = Math.Clamp(end, start, _codePoints.Length);
            var builder = new StringBuilder(end - start);
            for (var i = start; i < end; i++)
                AppendCodePoint(builder, _codePoints[i]);
            return builder.ToString();
        }
        if (IsSurrogatePairFree)
            return Value[Math.Clamp(start, 0, Value.Length)..Math.Clamp(end, 0, Value.Length)];
        int startChar = CodePointIndexToCharIndex(Value, start);
        int endChar = CodePointIndexToCharIndex(Value, end);
        return Value[startChar..endChar];
    }

    /// <summary>The substring over the code-point range, as a str.</summary>
    internal PyStrObject SubstringByCodePointRangeStr(int start, int end)
    {
        if (_codePoints is not null)
        {
            start = Math.Clamp(start, 0, _codePoints.Length);
            end = Math.Clamp(end, start, _codePoints.Length);
            if (end - start is 0)
                return Empty;
            var codePoints = _codePoints[start..end];
            var builder = new StringBuilder(end - start);
            foreach (var codePoint in codePoints)
                AppendCodePoint(builder, codePoint);
            return ContainsAdjacentLoneSurrogates(codePoints)
                ? new PyStrObject(builder.ToString(), codePoints)
                : FromString(builder.ToString());
        }
        return FromString(SubstringByCodePointRange(start, end));
    }

    /// <summary>First code point of the string, or -1 when empty.</summary>
    internal int FirstCodePoint() => _codePoints is not null ? _codePoints[0]
        : Value.Length is 0 ? -1 : CodePointAt(Value, 0);

    // CPython's unicode_upper/lower/casefold shape: map every code point and
    // rebuild the string
    internal static PyResult MapCodePoints(PyStrObject self, Func<int, string> map)
    {
        // the rebuilt sequence can only need an authoritative view when the
        // input has one: mappings of a plain string read back through UTF-16
        // unchanged, so the fast path stays allocation-free
        if (self._codePoints is null)
        {
            var builder = new StringBuilder(self.Value.Length);
            var enumerator = self.EnumerateCodePoints();
            while (enumerator.MoveNext())
                builder.Append(map(enumerator.Current));
            return PyStrObject.FromString(builder.ToString());
        }
        var mapped = new List<int>(self._codePoints.Length);
        var enumerator2 = self.EnumerateCodePoints();
        while (enumerator2.MoveNext())
            mapped.AddRange(ToCodePointArray(map(enumerator2.Current)));
        return PyStrObject.FromCodePoints([.. mapped]);
    }

    internal static int[] ToCodePointArray(PyStrObject self) => self.GetCodePointArray();

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

    /// <summary>
    /// The code points of the literal segment covering the char range
    /// [charStart, charEnd), read from the authoritative sequence.
    /// </summary>
    internal int[] LiteralCodePointRange(int charStart, int charEnd)
    {
        Debug.Assert(_codePoints is not null);
        var segment = new List<int>();
        var position = 0;
        foreach (var codePoint in _codePoints)
        {
            var width = codePoint >= 0x10000 ? 2 : 1;
            if (position >= charStart && position + width <= charEnd)
                segment.Add(codePoint);
            position += width;
            if (position >= charEnd)
                break;
        }
        return [.. segment];
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

    // CPython unicode_compare_eq: equality is over code-point sequences, so
    // two strings with equal UTF-16 payloads can differ when one carries
    // adjacent lone surrogates ('\ud800\udc00' != '\U00010000'). A sequence
    // with adjacent lone surrogates can never equal a plain UTF-16 reading:
    // equal sequences expand to equal payloads, and the lone-surrogate
    // payload would then read back as its own sequence — contradiction.
    internal bool PyStrEquals(PyStrObject other)
    {
        if (_codePoints is null && other._codePoints is null)
            return Value == other.Value;
        if (_codePoints is null || other._codePoints is null)
            return false;
        return _codePoints.AsSpan().SequenceEqual(other._codePoints);
    }

    // CPython unicode_compare: ordering over code-point sequences
    internal int PyStrCompare(PyStrObject other)
    {
        if (_codePoints is null && other._codePoints is null)
            return CompareCodePoints(Value, other.Value);
        var left = GetCodePointArray();
        var right = other.GetCodePointArray();
        var shared = Math.Min(left.Length, right.Length);
        for (var i = 0; i < shared; i++)
        {
            if (left[i] != right[i])
                return left[i] < right[i] ? -1 : 1;
        }
        return left.Length.CompareTo(right.Length);
    }

    // the Python-level hash: sequences with lone surrogates hash over their
    // authoritative view (never equal to a plain string, see PyStrEquals),
    // everything else keeps the payload hash. The -1 sentinel never escapes
    // a Python hash.
    internal int PyStrHash()
    {
        if (_codePoints is null)
            return GetHashCode();
        var accumulator = new HashCode();
        accumulator.AddBytes(MemoryMarshal.AsBytes(_codePoints.AsSpan()));
        var code = accumulator.ToHashCode();
        return code is -1 ? -2 : code;
    }

    // CPython unicode_contains: the substring test is over code-point
    // sequences; UTF-16 substring search is only valid when both sides are
    // plain readings
    internal bool PyStrContains(PyStrObject sub)
    {
        if (_codePoints is null && sub._codePoints is null)
            return Value.Contains(sub.Value);
        var haystack = GetCodePointArray();
        var needle = sub.GetCodePointArray();
        if (needle.Length is 0)
            return true;
        for (var i = 0; i + needle.Length <= haystack.Length; i++)
        {
            var j = 0;
            while (j < needle.Length && haystack[i + j] == needle[j])
                j++;
            if (j == needle.Length)
                return true;
        }
        return false;
    }

    // sq_concat with code-point semantics: the common case stays a plain
    // UTF-16 concat; a boundary that would pair lone surrogates across the
    // operands (or an operand already carrying them) goes through the
    // authoritative sequence
    internal static PyStrObject ConcatValues(PyStrObject left, PyStrObject right)
    {
        if (left._codePoints is null && right._codePoints is null
            && !PairsLoneSurrogatesAcross(left.Value, right.Value))
            return FromString(left.Value + right.Value);
        var codePoints = new int[left.PyLength + right.PyLength];
        left.GetCodePointArray().AsSpan().CopyTo(codePoints);
        right.GetCodePointArray().AsSpan().CopyTo(codePoints.AsSpan(left.PyLength));
        return FromCodePoints(codePoints);
    }

    // the payloads' boundary chars decide: a trailing high unit is necessarily
    // a lone high-surrogate code point in its own string (nothing follows to
    // pair with), and a leading low unit likewise
    internal static bool PairsLoneSurrogatesAcross(string left, string right) =>
        left.Length is not 0 && right.Length is not 0
        && char.IsHighSurrogate(left[^1]) && char.IsLowSurrogate(right[0]);
}

/// <summary>
/// Segment-wise string building that preserves code-point semantics: when a
/// segment boundary brings a lone high surrogate next to a lone low one, or a
/// segment itself carries lone surrogates, the builder additionally collects
/// the authoritative code-point sequence so the result keeps them separate —
/// CPython concatenates code-point sequences, while a plain UTF-16 buffer
/// would silently pair them. Unaffected concatenations build a plain string
/// exactly as before.
/// </summary>
internal sealed class PyStrConcatBuilder
{
    private readonly StringBuilder _sb = new();
    private List<int>? _codePoints;
    private bool _tailLoneHigh;

    public void Append(char ch)
    {
        if (_codePoints is null && _tailLoneHigh && char.IsLowSurrogate(ch))
            CollectPending();
        _codePoints?.Add(ch);
        _sb.Append(ch);
        _tailLoneHigh = char.IsHighSurrogate(ch);
    }

    public void Append(string segment)
    {
        if (segment.Length is 0)
            return;
        if (_codePoints is null && _tailLoneHigh && char.IsLowSurrogate(segment[0]))
            CollectPending();
        _codePoints?.AddRange(PyStrObject.ToCodePointArray(segment));
        _sb.Append(segment);
        _tailLoneHigh = char.IsHighSurrogate(segment[^1]);
    }

    public void Append(PyStrObject segment)
    {
        var value = segment.Value;
        if (value.Length is 0)
            return;
        // a segment with its own lone surrogates must carry them even
        // before any boundary pairing
        if (_codePoints is null && (segment.HasAmbiguousCodePoints
            || (_tailLoneHigh && char.IsLowSurrogate(value[0]))))
            CollectPending();
        _codePoints?.AddRange(segment.GetCodePointArray());
        _sb.Append(value);
        _tailLoneHigh = char.IsHighSurrogate(value[^1]);
    }

    /// <summary>
    /// Append the format-string literal segment <paramref name="source"/>
    /// [charStart, charEnd). A source with an authoritative sequence reads
    /// the segment from it — the UTF-16 range view could not tell its lone
    /// surrogates from astral pairs.
    /// </summary>
    public void AppendLiteral(PyStrObject source, int charStart, int charEnd)
    {
        if (!source.HasAmbiguousCodePoints)
        {
            Append(source.Value[charStart..charEnd]);
            return;
        }

        var segment = source.LiteralCodePointRange(charStart, charEnd);
        // the segment belongs to an ambiguous source, so the collected view
        // is authoritative regardless of any boundary pairing
        var collected = CollectPending();
        collected.AddRange(segment);
        _sb.Append(source.Value[charStart..charEnd]);
        _tailLoneHigh = segment is [.., var last] && last is >= 0xD800 and <= 0xDBFF;
    }

    public PyStrObject ToStr() => _codePoints is null
        ? PyStrObject.FromString(_sb.ToString())
        : PyStrObject.FromCodePoints([.. _codePoints]);

    // start authoritative collection over what is already built and return
    // the collecting list: the content became ambiguous only now, so the
    // UTF-16 reading of the pending prefix is its correct code-point sequence
    private List<int> CollectPending()
    {
        if (_codePoints is not null)
            return _codePoints;
        _codePoints = new List<int>(_sb.Length);
        if (_sb.Length is not 0)
            _codePoints.AddRange(PyStrObject.ToCodePointArray(_sb.ToString()));
        return _codePoints;
    }
}

[PyType("str")]
public sealed partial class PyStrObjectType : PyTypeObject<PyStrObject>
{
    internal override bool ReleasesWithFreeList => true;

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
        // compare the window edge in place; the code-point window fit does
        // not imply the UTF-16 window fits an astral needle, so check again
        int charStart = self.CodePointIndexToCharIndex(start);
        int charEnd = self.CodePointIndexToCharIndex(end);
        int needleChars = needle.Value.Length;
        if (charEnd - charStart < needleChars)
            return false;
        return startswith
            ? self.Value.AsSpan(charStart, needleChars).SequenceEqual(needle.Value)
            : self.Value.AsSpan(charEnd - needleChars, needleChars).SequenceEqual(needle.Value);
    }

    [PyMethod("replace")]
    [AIGenerated]
    [PyFunctionParameters("old", "new", "/", "count=-1")]
    private static PyResult Replace(PyCallContext context, PyStrObject self, PyArguments arguments)
    {
        if (arguments[0] is not PyStrObject oldStr)
            return PyResult.TypeError(PySR.Runtime_Str_MethodArgMustBeStr, "replace", 1, PyUtils.ArgumentTypeName(arguments[0]));
        if (arguments[1] is not PyStrObject newStr)
            return PyResult.TypeError(PySR.Runtime_Str_MethodArgMustBeStr, "replace", 2, PyUtils.ArgumentTypeName(arguments[1]));

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
            return PyResult.TypeError(PySR.Runtime_Str_MethodArgMustBeStr, methodName, 1, PyUtils.ArgumentTypeName(arguments[0]));
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
        // search the window in place: repeated offset calls (tokenizer loops)
        // must stay linear overall, so no window copy
        int charStart = self.CodePointIndexToCharIndex(start);
        int charEnd = self.CodePointIndexToCharIndex(end);
        int charIdx = self.Value.IndexOf(subStr.Value, charStart, charEnd - charStart, StringComparison.Ordinal);
        if (charIdx < 0)
            return PyIntObject.MinusOne;
        return PyIntObject.FromInteger(self.CharIndexToCodePointIndex(charIdx));
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
            return PyResult.TypeError(PySR.Runtime_Str_MethodArgMustBeStr, methodName, 1, PyUtils.ArgumentTypeName(arguments[0]));
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
        // search the window in place (no copy), anchored at the window end
        int charStart = self.CodePointIndexToCharIndex(start);
        int charEnd = self.CodePointIndexToCharIndex(end);
        int charIdx = self.Value.LastIndexOf(subStr.Value, charEnd - 1, charEnd - charStart, StringComparison.Ordinal);
        if (charIdx < 0)
            return PyIntObject.MinusOne;
        return PyIntObject.FromInteger(self.CharIndexToCodePointIndex(charIdx));
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
            return PyResult.TypeError(PySR.Runtime_Str_MethodArgMustBeStr, "count", 1, PyUtils.ArgumentTypeName(arguments[0]));

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
        // count inside the window in place: the needle chain walks forward,
        // so repeated calls and large windows stay linear (no window copy)
        int charStart = self.CodePointIndexToCharIndex(start);
        int charEnd = self.CodePointIndexToCharIndex(end);

        int count = 0;
        int index = charStart;
        while ((index = self.Value.IndexOf(subStr.Value, index, charEnd - index, StringComparison.Ordinal)) is not -1)
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
            return PyResult.TypeError(PySR.Runtime_Object_FormatArg2NonString, formatSpec.PyType.TpName);

        // CPython _PyUnicode_FormatAdvancedWriter: a zero-length spec makes
        // __format__ equivalent to str(obj)
        if (specStr.Value.Length is 0)
            return PySpecialMethods.Str(context, self);

        if (!PyFormatSpec.TryParse(specStr.Value, out var spec, out var bothSeparators))
            return PyFormatSpec.ParseError(specStr.Value, self.PyType.TpName);

        // CPython validates in this order: the parse-level grouping check,
        // the presentation-type dispatch, then format_string_internal's
        // flag checks (',c' -> Cannot specify, '+d' -> Unknown format code,
        // '+5' -> Sign not allowed)
        var groupingError = PyFormatSpec.ValidateGrouping(spec, bothSeparators, spec.Type ?? 's');
        if (groupingError.IsError)
            return groupingError;

        if (spec.Type is not (null or 's'))
            return PyFormatSpec.UnknownCode(spec.Type.Value, self.PyType.TpName);

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
        // CPython stringlib/partition.h: a missing separator yields
        // (self, "", "") — partition has no error path there
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
    // CPython's clinic signature is splitlines(self, /, keepends=False): the
    // slash sits after self, so keepends also binds by keyword
    [PyFunctionParameters("keepends=False")]
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
            return PyResult.TypeError(PySR.Runtime_StrEncode_ArgMustBeStr, "encoding", PyUtils.ArgumentTypeName(arguments[0]));
        if (arguments[1] is not PyStrObject errorsArg)
            return PyResult.TypeError(PySR.Runtime_StrEncode_ArgMustBeStr, "errors", PyUtils.ArgumentTypeName(arguments[1]));

        return EncodeCore(context, self.Value, encodingArg.Value, errorsArg.Value, authoritativeCodePoints: self.GetCodePointArrayOrNull());
    }

    // The str.encode core shared with the bytes()/bytearray()
    // string+encoding constructors: codec resolution, error handlers and
    // the bare utf-16/32 BOM all match unicode_encode here. .NET's encoders
    // substitute a replacement character instead of failing, so the whole
    // string goes through an encoder that reports the first unmappable
    // code point, and the CPython handler for errors= decides what happens
    // from there. The authoritative code points, when the payload carries
    // adjacent lone surrogates, override the UTF-16 reading — the .NET
    // encoder would encode the accidental pair as an astral character.
    internal static PyResult EncodeCore(PyCallContext context, string value, string encoding, string errors,
        bool emitPreamble = true, int[]? authoritativeCodePoints = null)
    {
        // utf-7, hz and big5hkscs have no BCL backing; their stateful encoders
        // are self-contained and carry their own errors= handling
        switch (NormalizeEncodingName(encoding))
        {
            case "utf7":
                return PyUtf7Codec.Encode(value);
            case "hz" or "hzgb" or "hzgb2312":
                return PyHzCodec.Encode(value, errors);
            case "big5hkscs" or "hkscs":
                return PyBig5HkscsCodec.Encode(value, errors);
        }

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
        // charmap codecs refuse the characters their tables leave undefined
        // (the .NET tables encode them silently); CPython extends the error
        // over the run of consecutive unmappable code points
        FrozenSet<char>? undefined = null;
        if (codec.Kind is PyCodecInfo.CodecKind.Charmap
            && PyCharmapUndefined.TryGet(NormalizeEncodingName(encoding), out var undef))
            undefined = undef.Chars;
        var strict = CreateReportingEncoding(enc);
        var codePoints = authoritativeCodePoints ?? PyStrObject.ToCodePointArray(value);
        var bytes = new List<byte>(value.Length);

        var position = 0;
        // UTF-16 cursor kept in lockstep with the code-point cursor, so the
        // per-event window is a zero-copy span instead of a tail slice
        var charPos = 0;
        while (position < codePoints.Length)
        {
            var rest = value.AsSpan(charPos);
            // an authoritative view may hold surrogates the payload shows as
            // legal pairs, which the .NET encoder would happily encode: stop
            // before the next surrogate code point and raise the codec's own
            // error event instead (utf-8: 'surrogates not allowed')
            if (authoritativeCodePoints is not null)
            {
                var surrogateAt = IndexOfSurrogate(codePoints, position);
                if (surrogateAt is 0)
                {
                    var surrogateRunEnd = codec.CollectsRun ? UnmappableRunEnd(strict, codePoints, position) : position + 1;
                    var surrogateError = ApplyEncodeErrorHandler(codec, codePoints, position, surrogateRunEnd, errors, bytes, strict, value);
                    if (surrogateError is not null)
                        return PyResult.FromException(surrogateError);
                    charPos += CharsForCodePoints(codePoints, position, surrogateRunEnd);
                    position = surrogateRunEnd;
                    continue;
                }
                if (surrogateAt > 0)
                    rest = rest[..CharsForCodePoints(codePoints, position, surrogateAt)];
            }
            // stop the .NET encoder before the first undefined character; a
            // run opening there is its own error event
            var undefinedAt = -1;
            if (undefined is not null)
            {
                undefinedAt = IndexOfUndefined(rest, undefined);
                if (undefinedAt is 0)
                {
                    var undefinedRunEnd = codec.CollectsRun
                        ? UnmappableRunEnd(strict, codePoints, position, undefined)
                        : position + 1;
                    var undefinedError = ApplyEncodeErrorHandler(codec, codePoints, position, undefinedRunEnd, errors, bytes, strict, value);
                    if (undefinedError is not null)
                        return PyResult.FromException(undefinedError);
                    charPos += CharsForCodePoints(codePoints, position, undefinedRunEnd);
                    position = undefinedRunEnd;
                    continue;
                }
                if (undefinedAt > 0)
                    rest = rest[..undefinedAt];
            }
            // GetByteCount would trip the strict fallback before any bytes
            // exist, so size the buffer with the arithmetic worst case and
            // let GetBytes itself report the first unmappable code point
            var buffer = new byte[strict.GetMaxByteCount(rest.Length)];
            int written;
            try
            {
                written = strict.GetBytes(rest, buffer);
            }
            catch (EncoderFallbackException ex)
            {
                // the failed call discards what it had encoded so far
                if (ex.Index > 0)
                {
                    var prefix = rest[..ex.Index];
                    var prefixBuffer = new byte[strict.GetMaxByteCount(prefix.Length)];
                    var prefixWritten = strict.GetBytes(prefix, prefixBuffer);
                    bytes.AddRange(prefixBuffer.AsSpan(0, prefixWritten));
                }
                var failed = position + PyStrObject.CountCodePoints(rest[..ex.Index]);
                var runEnd = codec.CollectsRun ? UnmappableRunEnd(strict, codePoints, failed) : failed + 1;
                var error = ApplyEncodeErrorHandler(codec, codePoints, failed, runEnd, errors, bytes, strict, value);
                if (error is not null)
                    return PyResult.FromException(error);
                charPos += CharsForCodePoints(codePoints, position, runEnd);
                position = runEnd;
                continue;
            }
            bytes.AddRange(buffer.AsSpan(0, written));
            if (undefinedAt > 0)
            {
                charPos += undefinedAt;
                position += PyStrObject.CountCodePoints(rest);
                continue;
            }
            break;
        }

        if (codec.EmitsBom && emitPreamble)
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
    private static int UnmappableRunEnd(Encoding strict, int[] codePoints, int start, FrozenSet<char>? undefined = null)
    {
        var end = start + 1;
        while (end < codePoints.Length && (!CanEncode(strict, codePoints[end])
                || undefined is not null && undefined.Contains((char)codePoints[end])))
            end++;
        return end;
    }

    // the char index of the first code point CPython's charmap table
    // leaves undefined, or -1 when the text encodes cleanly
    private static int IndexOfUndefined(ReadOnlySpan<char> text, FrozenSet<char> undefined)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (undefined.Contains(text[i]))
                return i;
        }
        return -1;
    }

    // the code-point offset of the next surrogate relative to start, or -1
    // when the rest of the sequence holds none
    private static int IndexOfSurrogate(int[] codePoints, int start)
    {
        for (var i = start; i < codePoints.Length; i++)
        {
            if (codePoints[i] is >= 0xD800 and <= 0xDFFF)
                return i - start;
        }
        return -1;
    }

    // UTF-16 code units spanned by the code points [start, end)
    private static int CharsForCodePoints(int[] codePoints, int start, int end)
    {
        var chars = 0;
        for (var i = start; i < end; i++)
            chars += codePoints[i] >= 0x10000 ? 2 : 1;
        return chars;
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
                if (errors is not "strict")
                    return UnknownErrorHandler(errors);
                // mbcs reports every strict failure at position 0-0; the
                // code page converters never locate the character, so the
                // start+1 != end wording of the message applies too
                return codec.Kind is PyCodecInfo.CodecKind.Mbcs
                    ? UnicodeEncodeError(codec, value, 0, 0)
                    : UnicodeEncodeError(codec, value, start, end);
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
    internal static string EncodeErrorReplacement(string errors, int codePoint)
    {
        return errors switch
        {
            "xmlcharrefreplace" => $"&#{codePoint};",
            "namereplace" => GetNameReplacement(codePoint),
            _ => BackslashEscape(codePoint),
        };
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
            // the BOM-emitting variant; the plain codec shares the encoder
            case "utf8sig":
                return new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
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
            // CJK and euro-variant codec names the BCL knows by codepage;
            // the byte streams match CPython's codecs on common text
            case "eucjp":
                return Encoding.GetEncoding(51932);
            case "euckr":
                return Encoding.GetEncoding(51949);
            case "iso2022jp":
                return Encoding.GetEncoding(50220);
            case "iso2022kr":
                return Encoding.GetEncoding(50225);
            case "tis620":
                return Encoding.GetEncoding(874);
            case "kz1048":
                return Encoding.GetEncoding(21866);
            case "eucjis2004" or "jisx0213":
                return Encoding.GetEncoding(20932);
            case "iso885915" or "latin9" or "l9":
                return Encoding.GetEncoding(28605);
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

    // decoder factory for the incremental file layer's generic-codec path:
    // the decoder raises, and the CPython errors= handler decides at the
    // first failing event
    internal static Decoder MakeStrictDecoder(Encoding encoding)
    {
        try
        {
            return Encoding.GetEncoding(encoding.CodePage, new EncoderExceptionFallback(), new DecoderExceptionFallback()).GetDecoder();
        }
        catch (ArgumentException)
        {
            return encoding.GetDecoder();
        }
    }

    /// CPython namereplace: the character's Unicode name inside \N{...}
    /// (formal and algorithmic names alike), or a hex escape for the code
    /// points without a name.
    /// </summary>
    private static string GetNameReplacement(int codePoint)
    {
        if (PyUnicodeNameTable.TryGetName(codePoint, out var name))
            return $"\\N{{{name}}}";
        return BackslashEscape(codePoint);
    }

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
        return self.ToExactStr();
    }

    protected override PyResult Hash(PyCallContext context, PyStrObject self)
    {
        // the Python-level hash follows the code-point sequence, unlike the
        // C#-level GetHashCode which stays on the payload for reference use
        return PyIntObject.FromInteger(self.PyStrHash());
    }
    protected override PyResult Len(PyCallContext context, PyStrObject self)
    {
        return PyIntObject.FromInteger(self.PyLength);
    }
    protected override PyResult Iter(PyCallContext context, PyStrObject self)
    {
        // CPython str_iter names the iterator after the string's storage
        // kind: UCS-1 ascii strings report str_ascii_iterator; the iterator
        // steps over the authoritative code-point view when present
        return new PyStrIteratorObject(self.Value, HasOnlyAscii(self.Value), self.GetCodePointArrayOrNull());
    }

    private static bool HasOnlyAscii(string value)
    {
        foreach (var c in value)
        {
            if (c > 127)
                return false;
        }
        return true;
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

            if (self.IsSurrogatePairFree)
            {
                // code-point and char indices coincide: pick the chars
                // directly at O(1) each instead of materializing the whole
                // code-point array first (repeated slicing stays linear)
                if (step is 1)
                    return PyStrObject.FromString(self.Value[start..(start + length)]);
                var fast = new StringBuilder(length);
                for (int i = start, ri = 0; ri < length; i += step, ri++)
                    fast.Append(self.Value[i]);
                return PyStrObject.FromString(fast.ToString());
            }

            var codePoints = new List<int>(self.PyLength);
            var enumerator = self.EnumerateCodePoints();
            while (enumerator.MoveNext())
                codePoints.Add(enumerator.Current);

            // rebuild from the picked code points: a slice may keep adjacent
            // lone surrogates that a plain string would silently pair
            var picked = new int[length];
            for (int i = start, ri = 0; ri < length; i += step, ri++)
                picked[ri] = codePoints[i];
            return PyStrObject.FromCodePoints(picked);
        }

        // unicode_subscript branches on PyIndex_Check before any conversion;
        // the str wording is its own sentence and quotes the type name
        if (item.PyType.Slots.Index is null)
            return PyResult.TypeError(PySR.Runtime_String_IndicesMustBeIntegers, item.PyType.TpName);

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
    // sq_concat (modeled on the Sequence family slot, not Number.Add): no
    // reflected variant exists (str.__radd__ does not exist), and __rmul__
    // keeps the non-swapping self*value order of wrap_indexargfunc
    protected override PyResult Concat(PyCallContext context, PyStrObject self, PyObject other)
    {
        if (other is PyStrObject strObj)
            return PyStrObject.ConcatValues(self, strObj);
        // the right operand's reflected __radd__ (if its type synthesized
        // one) runs first on the dispatch layer; the concat TypeError is
        // the last resort here
        return PyResult.TypeError(PySR.Runtime_String_AddNonStr, other.PyType.TpName);
    }
    protected override PyResult Eq(PyCallContext context, PyStrObject self, PyObject other)
    {
        if (other is PyStrObject strObj)
            return PyBoolObject.FromBoolean(self.PyStrEquals(strObj));
        return PyNotImplementedObject.NotImplemented;
    }
    protected override PyResult Lt(PyCallContext context, PyStrObject self, PyObject other)
    {
        if (other is not PyStrObject strObj)
            return PyNotImplementedObject.NotImplemented;
        // CPython unicode_compare order; string.CompareTo would use culture
        // rules ('a' < 'B' incorrectly) and CompareOrdinal code units.
        return PyBoolObject.FromBoolean(self.PyStrCompare(strObj) < 0);
    }
    protected override PyResult Le(PyCallContext context, PyStrObject self, PyObject other)
    {
        if (other is not PyStrObject strObj)
            return PyNotImplementedObject.NotImplemented;
        return PyBoolObject.FromBoolean(self.PyStrCompare(strObj) <= 0);
    }
    protected override PyResult Gt(PyCallContext context, PyStrObject self, PyObject other)
    {
        if (other is not PyStrObject strObj)
            return PyNotImplementedObject.NotImplemented;
        return PyBoolObject.FromBoolean(self.PyStrCompare(strObj) > 0);
    }
    protected override PyResult Ge(PyCallContext context, PyStrObject self, PyObject other)
    {
        if (other is not PyStrObject strObj)
            return PyNotImplementedObject.NotImplemented;
        return PyBoolObject.FromBoolean(self.PyStrCompare(strObj) >= 0);
    }
    protected override PyResult Repeat(PyCallContext context, PyStrObject self, PyObject other)
    {
        // sequence_repeat: the repeat count must speak the index protocol
        // before the conversion runs, or the non-int shape wins the message
        if (other.PyType.Slots.Index is null)
            return PyResult.TypeError(PySR.Runtime_Object_CantMultiplySequenceByNonInt, other.PyType.TpName);
        var result = PySpecialMethods.Index(context, other);
        if (result.IsError)
            return result;
        var count = result.Value;
        if (count.Value < 0)
            return PyStrObject.Empty;   // CPython: 'x' * -1 == ''
        if (!count.IsInt32)
            return PyResult.OverflowError(PySR.Runtime_Index_CannotFitInt, other.PyType.TpName);
        // sequence_repeat over code points: only a repeat count above one
        // can pair a lone trailing surrogate with a lone leading one
        if (self.HasAmbiguousCodePoints
            || (count.Int32Value > 1 && PyStrObject.PairsLoneSurrogatesAcross(self.Value, self.Value)))
        {
            var unit = self.GetCodePointArray();
            var repeated = new int[unit.Length * count.Int32Value];
            for (var i = 0; i < count.Int32Value; i++)
                unit.AsSpan().CopyTo(repeated.AsSpan(i * unit.Length));
            return PyStrObject.FromCodePoints(repeated);
        }
        return PyStrObject.FromString(string.Concat(Enumerable.Repeat(self.Value, count.Int32Value)));
    }
    [PySlot]
    protected override PyResult RMul(PyCallContext context, PyStrObject self, PyObject other)
    {
        return Repeat(context, self, other);
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

        // PyUnicode_Format assembles code-point sequences: literal runs,
        // conversion results and pad segments must not pair lone surrogates
        // across their boundaries
        var builder = new PyStrConcatBuilder();
        int literalStart = 0;

        for (int i = 0; i < formatStr.Length; i++)
        {
            if (formatStr[i] is not '%')
                continue;

            if (i > literalStart)
                builder.AppendLiteral(self, literalStart, i);

            i++; // skip '%'
            if (i >= formatStr.Length)
                return PyResult.ValueError("incomplete format");

            if (formatStr[i] is '%')
            {
                builder.Append('%');
                literalStart = i + 1;
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

            // Format the value; s/r/a keep the str object around so its
            // code-point view survives into the assembled result
            string formatted;
            PyStrObject? formattedObj = null;
            switch (fmtType)
            {
                case 's':
                    {
                        var strResult = PySpecialMethods.Str(context, value);
                        if (strResult.IsError)
                            return strResult;
                        var strObj = strResult.Value;
                        formattedObj = precision >= 0 ? strObj.CodePointPrefixStr(precision) : strObj;
                        formatted = formattedObj.Value;
                        break;
                    }
                case 'r':
                    {
                        var reprResult = PySpecialMethods.Repr(context, value);
                        if (reprResult.IsError)
                            return reprResult;
                        var reprObj = reprResult.Value;
                        formattedObj = precision >= 0 ? reprObj.CodePointPrefixStr(precision) : reprObj;
                        formatted = formattedObj.Value;
                        break;
                    }
                case 'a':
                    {
                        var asciiResult = PyBuiltinFunctions.Ascii.Call(context, [value]);
                        if (asciiResult.IsError)
                            return asciiResult;
                        var asciiObj = (PyStrObject)asciiResult.Value;
                        formattedObj = precision >= 0 ? asciiObj.CodePointPrefixStr(precision) : asciiObj;
                        formatted = formattedObj.Value;
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
                                return PyResult.TypeError(PySR.Runtime_Str_PctFormatRealNumberRequired, fmtType, value.PyType.TpName);
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
                            return PyResult.TypeError(PySR.Runtime_Str_PctFormatIntegerRequired, fmtType, value.PyType.TpName);
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
                            return PyResult.TypeError(PySR.Runtime_Str_PctFormatIntegerRequired, fmtType, value.PyType.TpName);
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
                            return PyResult.TypeError(PySR.Runtime_Str_PctFormatIntegerRequired, fmtType, value.PyType.TpName);
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
                            return AsPctFormatDouble(value, floatResult);
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
                            return AsPctFormatDouble(value, floatResult);
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
                            return AsPctFormatDouble(value, floatResult);
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
                        else if (value is PyStrObject wrongLength)
                        {
                            // formatchar (Objects/unicodeobject.c) names the
                            // offending length for a longer string
                            return PyResult.TypeError(PySR.Runtime_Str_PctFormatCharRequiresIntOrUnicodeLength, wrongLength.Value.Length);
                        }
                        else
                        {
                            var indexResult = PySpecialMethods.Index(context, value);
                            if (indexResult.IsError)
                                return PyResult.TypeError(PySR.Runtime_Str_PctFormatCharRequiresIntOrUnicode, value.PyType.TpName);
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
                    // CPython prints the character only when printable ASCII
                    // and points at its position in the format string itself
                    var displayChar = fmtType is >= (char)31 and <= (char)126 ? fmtType : '?';
                    return PyResult.ValueError($"unsupported format character '{displayChar}' (0x{(int)fmtType:x}) at index {i}");
            }

            // Apply # flag for float formats that already handled precision decimal point
            // (additional # handling for f/e/g already done above)

            // Override width if the # flag added extra characters for octal/hex

            // Apply width and alignment; CPython unicode_format pads the
            // code point sequence, so an astral character counts once. Pad
            // characters are ASCII, so they append as their own segments
            // around a conversion that kept its str object.
            string? padPrefix = null;
            string? padSuffix = null;
            var formattedLength = formattedObj?.PyLength ?? PyStrObject.CountCodePoints(formatted);
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
                    padSuffix = new string(padChar, pad);
                    formatted += padSuffix;
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
                    padPrefix = new string(padChar, pad);
                    formatted = padPrefix + formatted;
                }
            }

            if (formattedObj is not null)
            {
                if (padPrefix is not null)
                    builder.Append(padPrefix);
                builder.Append(formattedObj);
                if (padSuffix is not null)
                    builder.Append(padSuffix);
            }
            else
            {
                builder.Append(formatted);
            }
            literalStart = i + 1;
        }

        if (literalStart < formatStr.Length)
            builder.AppendLiteral(self, literalStart, formatStr.Length);

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

        return builder.ToStr();
    }

    // the CPython None split by entry path (getargs.c): positional
    // _PyArg_BadArgument prints tp_name, the keyword converterr prints
    // "None"; every other type keeps its plain type name
    private static string ArgumentNameForRejected(PyObject value, bool fromKwargs) =>
        value is PyNoneObject && !fromKwargs ? "NoneType" : PyUtils.ArgumentTypeName(value);

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
        // CPython names a rejected None differently by entry path: the
        // positional _PyArg_BadArgument prints tp_name ("NoneType") while
        // the keyword converterr prints "None"
        bool encodingFromKwargs = false;
        bool errorsFromKwargs = false;
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
                    encodingFromKwargs = true;
                    break;
                case "errors":
                    if (errors is not null)
                        return PyResult.TypeError(PySR.Runtime_Codec_MultipleValues, "str", name, 3);
                    errors = value;
                    errorsFromKwargs = true;
                    break;
                default:
                    return PyResult.TypeError(PySR.Runtime_Str_UnexpectedKeyword, name);
            }
        }

        // the clinic 'str' converters reject non-str before any conversion —
        // including before the missing-object shortcut, which stays the
        // empty string
        if (encoding is not null and not PyStrObject)
            return PyResult.TypeError(PySR.Runtime_Str_ArgMustBeStr, "encoding", ArgumentNameForRejected(encoding, encodingFromKwargs));
        if (errors is not null and not PyStrObject)
            return PyResult.TypeError(PySR.Runtime_Str_ArgMustBeStr, "errors", ArgumentNameForRejected(errors, errorsFromKwargs));

        PyResult result;
        if (source is null)
        {
            result = PyStrObject.Empty;
        }
        else
        {
            if (encoding is null && errors is null)
            {
                result = PySpecialMethods.Str(context, source);
                if (result.IsError)
                    return result;
            }
            else
            {
                if (!PyBytesObjectType.TryGetBytesLikeSpan(source, out var data))
                    return PyResult.TypeError(source is PyStrObject ? PySR.Runtime_Str_DecodingStrNotSupported : PySR.Runtime_Str_DecodingNeedBytesLike, source.PyType.TpName);

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

    // PyFloat_AsDouble's formatting-context sentence replaces the float()
    // constructor one; a custom __float__ still propagates unchanged
    private static PyResult AsPctFormatDouble(PyObject value, PyResult floatResult)
    {
        if (value.PyType.Slots.Float is null)
            return PyResult.TypeError(PySR.Runtime_Str_PctFormatMustBeRealNumber, value.PyType.TpName);
        return floatResult;
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
        // CPython unicode_contains (Objects/unicodeobject.c): the left
        // operand must be a str, and the rejection names its tp_name —
        // None included, unlike the getargs converter path
        if (item is not PyStrObject sub)
            return PyResult.TypeError(PySR.Runtime_Str_ContainsLeftOperandMustBeStr, item.PyType.Name);

        return PyBoolObject.FromBoolean(self.PyStrContains(sub));
    }
}
