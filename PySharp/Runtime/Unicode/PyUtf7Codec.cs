using PySharp.Modules.Builtins;
using PySharp.Runtime.Calls;
using System.Text;

namespace PySharp.Runtime.Unicode;

/// <summary>
/// The utf-7 codec (RFC 2152), ported from CPython's Objects/unicodeobject.c
/// (PyUnicode_DecodeUTF7Stateful / _PyUnicode_EncodeUTF7 with the default
/// option word: set O and the whitespace set encode directly, set D rides
/// the encoder's own table). The BCL dropped utf-7 in .NET 5, so both
/// directions are implemented here; decode error events go through the
/// shared errors= machinery under the bare 'utf7' name CPython reports.
/// </summary>
internal static class PyUtf7Codec
{
    // category of every ASCII byte: 0 = set D, 1 = set O, 2 = whitespace
    // (sp ht nl cr), 3 = must ride a base64 shift sequence
    private static int Category(int c) => c switch
    {
        >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9'
            or '\'' or '(' or ')' or ',' or '-' or '.' or '/' or ':' or '?' => 0,
        '!' or '"' or '#' or '$' or '%' or '&' or '*' or ';' or '<' or '=' or '>' or '@'
            or '[' or ']' or '^' or '_' or '`' or '{' or '|' or '}' => 1,
        ' ' or '\t' or '\n' or '\r' => 2,
        _ => 3,
    };

    private static bool IsBase64(int c) => (uint)(c - 'A') < 26 || (uint)(c - 'a') < 26
        || (uint)(c - '0') < 10 || c is '+' or '/';

    private static int FromBase64(int c) => c switch
    {
        >= 'A' and <= 'Z' => c - 'A',
        >= 'a' and <= 'z' => c - 'a' + 26,
        >= '0' and <= '9' => c - '0' + 52,
        '+' => 62,
        _ => 63,
    };

    private static byte ToBase64(ulong n) => (byte)"ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/"[(int)(n & 0x3F)];

    private const string ErrorName = "utf7";

    /// <summary>
    /// One-shot utf-7 decode. Mirrors the state machine of
    /// PyUnicode_DecodeUTF7Stateful with a null consumed pointer: a shift
    /// sequence left open at the end of input is the 'unterminated shift
    /// sequence' event, a section left with six or more queued bits is
    /// 'partial character in shift sequence', and non-zero left-over bits
    /// are the padding event. A pending high surrogate joins a following
    /// low one and otherwise rides out as itself.
    /// </summary>
    internal static PyResult Decode(ReadOnlySpan<byte> data, string errors, PyObject source)
    {
        var sb = new StringBuilder(data.Length);
        int s = 0;
        bool inShift = false;
        ulong bits = 0;
        ulong buffer = 0;
        uint surrogate = 0;
        // the '+' that opened the current shift section, where every
        // section-wide error event starts
        int shiftStart = 0;

        while (s < data.Length)
        {
            int ch = data[s];
            if (inShift)
            {
                if (IsBase64(ch))
                {
                    buffer = (buffer << 6) | (uint)FromBase64(ch);
                    bits += 6;
                    s++;
                    if (bits >= 16)
                    {
                        var outCh = (uint)(buffer >> ((int)bits - 16));
                        bits -= 16;
                        buffer &= (1UL << (int)bits) - 1;
                        if (surrogate is not 0)
                        {
                            if (outCh is >= 0xDC00 and <= 0xDFFF)
                            {
                                // the pair lands in the builder as its own
                                // UTF-16 units, which is the astral character
                                sb.Append((char)surrogate);
                                sb.Append((char)outCh);
                                surrogate = 0;
                                continue;
                            }
                            sb.Append((char)surrogate);
                            surrogate = 0;
                        }
                        if (outCh is >= 0xD800 and <= 0xDBFF)
                            surrogate = outCh;
                        else
                            sb.Append((char)outCh);
                    }
                }
                else
                {
                    // leaving the base64 section on a non-base64 byte
                    inShift = false;
                    string? reason = null;
                    if (bits >= 6)
                    {
                        s++;
                        reason = "partial character in shift sequence";
                    }
                    else if (bits > 0 && buffer is not 0)
                    {
                        s++;
                        reason = "non-zero padding bits in shift sequence";
                    }
                    if (reason is not null)
                    {
                        if (!HandleError(data, sb, errors, source, shiftStart, s, reason, out var next, out var error))
                            return error!.Value;
                        s = next;
                        continue;
                    }
                    if (surrogate is not 0 && ch <= 127 && ch is not '+')
                        sb.Append((char)surrogate);
                    surrogate = 0;
                    if (ch is '-')
                        s++;
                }
            }
            else if (ch is '+')
            {
                shiftStart = s;
                s++;
                if (s < data.Length && data[s] is (byte)'-')
                {
                    s++;
                    sb.Append('+');
                }
                else if (s < data.Length && !IsBase64(data[s]))
                {
                    s++;
                    if (!HandleError(data, sb, errors, source, shiftStart, s, "ill-formed sequence", out var next, out var error))
                        return error!.Value;
                    s = next;
                }
                else
                {
                    inShift = true;
                    surrogate = 0;
                    bits = 0;
                    buffer = 0;
                }
            }
            else if (ch <= 127)
            {
                s++;
                sb.Append((char)ch);
            }
            else
            {
                s++;
                if (!HandleError(data, sb, errors, source, s - 1, s, "unexpected special character", out var next, out var error))
                    return error!.Value;
                s = next;
            }
        }

        // a shift section still open at the end of input errors only when
        // its state is inconsistent (a pending surrogate, six or more
        // queued bits, or non-zero bits below six); a bare trailing '+'
        // with no state decodes to nothing
        if (inShift && (surrogate is not 0 || bits >= 6 || (bits > 0 && buffer is not 0)))
        {
            if (!HandleError(data, sb, errors, source, shiftStart, data.Length, "unterminated shift sequence", out _, out var error))
                return error!.Value;
        }
        return PyStrObject.FromString(sb.ToString());
    }

    private static bool HandleError(ReadOnlySpan<byte> data, StringBuilder sb, string errors, PyObject source,
        int start, int end, string reason, out int next, out PyResult? error)
        => PyBytesObjectType.ApplyDecodeHandler(data, start, end, reason, errors, ErrorName, source, sb, out next, out error);

    /// <summary>
    /// One-shot utf-7 encode with CPython's default option word: every code
    /// point outside the direct sets rides a '+base64-' shift sequence, and
    /// a terminating character from the base64 alphabet needs an explicit
    /// '-' to keep it from extending the sequence. The codec cannot fail,
    /// so no errors= handler is ever consulted.
    /// </summary>
    internal static PyResult Encode(string value)
    {
        var codePoints = PyStrObject.ToCodePointArray(value);
        var bytes = new List<byte>(value.Length);
        bool inShift = false;
        ulong bits = 0;
        ulong buffer = 0;

        foreach (var ch in codePoints)
        {
            if (inShift && IsEncodeDirect(ch))
            {
                if (bits is not 0)
                {
                    bytes.Add(ToBase64(buffer << (6 - (int)bits)));
                    bits = 0;
                    buffer = 0;
                }
                inShift = false;
                // a base64 character or '-' would extend the sequence
                if (IsBase64(ch) || ch is '-')
                    bytes.Add((byte)'-');
                bytes.Add((byte)ch);
                continue;
            }
            if (!inShift)
            {
                if (ch is '+')
                {
                    bytes.Add((byte)'+');
                    bytes.Add((byte)'-');
                    continue;
                }
                if (IsEncodeDirect(ch))
                {
                    bytes.Add((byte)ch);
                    continue;
                }
                bytes.Add((byte)'+');
                inShift = true;
            }
            if (ch >= 0x10000)
            {
                Push16(bytes, ref bits, ref buffer, 0xD800 + ((ch - 0x10000) >> 10));
                Push16(bytes, ref bits, ref buffer, 0xDC00 + ((ch - 0x10000) & 0x3FF));
            }
            else
            {
                Push16(bytes, ref bits, ref buffer, ch);
            }
        }
        if (bits is not 0)
            bytes.Add(ToBase64(buffer << (6 - (int)bits)));
        if (inShift)
            bytes.Add((byte)'-');
        return PyBytesObject.MoveBytes([.. bytes]);
    }

    // the encoder's direct set: set D, set O and whitespace; ASCII only and
    // never NUL (ENCODE_DIRECT's c > 0)
    private static bool IsEncodeDirect(int ch) => ch is > 0 and < 128 && Category(ch) is not 3;

    private static void Push16(List<byte> bytes, ref ulong bits, ref ulong buffer, int unit)
    {
        bits += 16;
        buffer = (buffer << 16) | (uint)unit;
        while (bits >= 6)
        {
            bytes.Add(ToBase64(buffer >> ((int)bits - 6)));
            bits -= 6;
        }
    }
}
