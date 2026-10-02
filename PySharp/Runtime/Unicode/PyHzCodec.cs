using PySharp.Modules.Builtins;
using PySharp.Runtime.Calls;
using System.Text;

namespace PySharp.Runtime.Unicode;

/// <summary>
/// The HZ-GB-2312 codec (RFC 1843), ported from CPython's cjkcodecs hz
/// state machine: ASCII rides directly, '~{' opens a 7-bit GB2312 section
/// closed by '~}', '~~' and '~~\n' are the escape and line continuation,
/// and every failure event covers a single byte. GB2312 membership and
/// mapping ride the generated GB2312 table, since HZ transmits the EUC
/// bytes with their high bit stripped.
/// </summary>
internal static class PyHzCodec
{
    private const string ErrorName = "hz";
    private const string IllegalReason = "illegal multibyte sequence";
    private const string IncompleteReason = "incomplete multibyte sequence";

    internal static PyResult Decode(ReadOnlySpan<byte> data, string errors, PyObject source)
    {
        var sb = new StringBuilder(data.Length);
        int s = 0;
        bool gbMode = false;
        while (s < data.Length)
        {
            int b = data[s];
            if (b is '~')
            {
                if (s + 1 >= data.Length)
                    return Fail(data, sb, errors, source, s, IncompleteReason);
                int b2 = data[s + 1];
                // the legal escapes: '~}' closes a GB section, '~~' and
                // '~~\n' (escape, line continuation) are ASCII-mode only
                if (gbMode ? b2 is '}' : b2 is '~' or '{' or '\n')
                {
                    if (b2 is '}')
                        gbMode = false;
                    else if (b2 is '{')
                        gbMode = true;
                    else if (b2 is '~')
                        sb.Append('~');
                    s += 2;
                }
                else
                {
                    return Fail(data, sb, errors, source, s, IllegalReason);
                }
            }
            else if (b >= 0x80)
            {
                return Fail(data, sb, errors, source, s, IllegalReason);
            }
            else if (!gbMode)
            {
                sb.Append((char)b);
                s++;
            }
            else
            {
                if (s + 1 >= data.Length)
                    return Fail(data, sb, errors, source, s, IncompleteReason);
                int codePoint = PyGb2312Codec.DecodePair(b | 0x80, data[s + 1] | 0x80);
                if (codePoint < 0)
                    return Fail(data, sb, errors, source, s, IllegalReason);
                sb.Append((char)codePoint);
                s += 2;
            }
        }
        return PyStrObject.FromString(sb.ToString());
    }

    private static PyResult Fail(ReadOnlySpan<byte> data, StringBuilder sb, string errors, PyObject source,
        int start, string reason)
    {
        // every failure event covers a single byte
        PyBytesObjectType.ApplyDecodeHandler(data, start, start + 1, reason, errors, ErrorName, source, sb, out _, out var error);
        return error!.Value;
    }

    internal static PyResult Encode(string value, string errors)
    {
        var codePoints = PyStrObject.ToCodePointArray(value);
        var bytes = new List<byte>(value.Length * 2);
        bool gbMode = false;
        for (var i = 0; i < codePoints.Length; i++)
        {
            int codePoint = codePoints[i];
            if (codePoint < 0x80)
            {
                if (gbMode)
                {
                    bytes.Add((byte)'~');
                    bytes.Add((byte)'}');
                    gbMode = false;
                }
                bytes.Add((byte)codePoint);
                if (codePoint is '~')
                    bytes.Add((byte)'~');
                continue;
            }
            // HZ transmits the EUC bytes with their high bit stripped
            if (PyGb2312Codec.TryEncode(codePoint, out var lead, out var trail))
            {
                if (!gbMode)
                {
                    bytes.Add((byte)'~');
                    bytes.Add((byte)'{');
                    gbMode = true;
                }
                bytes.Add((byte)(lead & 0x7F));
                bytes.Add((byte)(trail & 0x7F));
                continue;
            }
            switch (errors)
            {
                case "ignore":
                    break;
                case "replace":
                    EmitAscii(bytes, "?", ref gbMode);
                    break;
                case "backslashreplace" or "xmlcharrefreplace" or "namereplace":
                    EmitAscii(bytes, PyStrObjectType.EncodeErrorReplacement(errors, codePoint), ref gbMode);
                    break;
                default:
                    return errors is "strict"
                        ? PyResult.FromException(UnicodeEncodeError(value, i))
                        : PyResult.LookupError(PySR.Runtime_Codec_UnknownErrorHandlerName, errors);
            }
        }
        if (gbMode)
        {
            bytes.Add((byte)'~');
            bytes.Add((byte)'}');
        }
        return PyBytesObject.MoveBytes([.. bytes]);
    }

    private static void EmitAscii(List<byte> bytes, string text, ref bool gbMode)
    {
        if (gbMode)
        {
            bytes.Add((byte)'~');
            bytes.Add((byte)'}');
            gbMode = false;
        }
        foreach (var c in text)
        {
            bytes.Add((byte)c);
            if (c is '~')
                bytes.Add((byte)'~');
        }
    }

    private static PyExceptionObject UnicodeEncodeError(string value, int position)
        => PyExceptionObject.UnsafeCreate(PyUnicodeEncodeErrorObjectType.Shared,
        [
            PyStrObject.FromString(ErrorName),
            PyStrObject.FromString(value),
            PyIntObject.FromInteger(position),
            PyIntObject.FromInteger(position + 1),
            PyStrObject.FromString(IllegalReason),
        ]);
}
