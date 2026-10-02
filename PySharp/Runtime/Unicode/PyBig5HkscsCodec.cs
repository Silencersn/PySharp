using PySharp.Modules.Builtins;
using PySharp.Runtime.Calls;
using System.IO.Compression;
using System.Text;

namespace PySharp.Runtime.Unicode;

/// <summary>
/// The big5hkscs codec, backed by the generated table in
/// <c>PyBig5HkscsCodec.Tables.cs</c> (the BCL has no Big5-HKSCS mapping).
/// Mirrors CPython's cjkcodecs decoder error shape: every failure event
/// covers only the lead byte, a truncated tail is the 'incomplete
/// multibyte sequence' event, and handlers ride the shared errors=
/// machinery. The encode side is the inverse of the decode table with the
/// CPython encoder's preferred pairs applied on top.
/// </summary>
internal static partial class PyBig5HkscsCodec
{
    private const string ErrorName = "big5hkscs";
    private const string IllegalReason = "illegal multibyte sequence";
    private const string IncompleteReason = "incomplete multibyte sequence";

    private static readonly int[] DecodeTable = BuildDecodeTable();
    private static readonly Dictionary<int, int> EncodeMap;

    // a static constructor body runs after every field initializer of the
    // partial type, so the overrides from the tables file are ready
    static PyBig5HkscsCodec() => EncodeMap = BuildEncodeMap();

    private static int[] BuildDecodeTable()
    {
        var bytes = new byte[LeadCount * TrailCount * 3];
        using var compressed = new MemoryStream(Convert.FromBase64String(TableData));
        using var zlib = new ZLibStream(compressed, CompressionMode.Decompress);
        zlib.ReadExactly(bytes);
        var table = new int[LeadCount * TrailCount];
        for (var i = 0; i < table.Length; i++)
            table[i] = (bytes[i * 3 + 2] << 16) | (bytes[i * 3 + 1] << 8) | bytes[i * 3];
        return table;
    }

    private static Dictionary<int, int> BuildEncodeMap()
    {
        var map = new Dictionary<int, int>(DecodeTable.Length);
        for (var lead = 0; lead < LeadCount; lead++)
        {
            for (var trail = 0; trail < TrailCount; trail++)
            {
                var codePoint = DecodeTable[lead * TrailCount + trail];
                if (codePoint is Unmapped || map.ContainsKey(codePoint))
                    continue;
                var trailByte = trail < 63 ? 0x40 + trail : 0xA1 + trail - 63;
                map[codePoint] = ((lead + LeadBase) << 8) | trailByte;
            }
        }
        foreach (var (codePoint, code) in EncodeOverrides)
            map[codePoint] = code;
        return map;
    }

    private static bool IsValidTrail(int b) => b is >= 0x40 and <= 0x7E or >= 0xA1 and <= 0xFE;

    internal static PyResult Decode(ReadOnlySpan<byte> data, string errors, PyObject source)
    {
        var sb = new StringBuilder(data.Length);
        int s = 0;
        while (s < data.Length)
        {
            int lead = data[s];
            if (lead < 0x80)
            {
                sb.Append((char)lead);
                s++;
                continue;
            }
            string? reason = lead < LeadBase || lead > 0xFE ? IllegalReason
                : s + 1 >= data.Length ? IncompleteReason
                : !IsValidTrail(data[s + 1]) ? IllegalReason
                : null;
            if (reason is null)
            {
                var codePoint = DecodeTable[(lead - LeadBase) * TrailCount + TrailIndex(data[s + 1])];
                if (codePoint is not Unmapped)
                {
                    if (codePoint < 0x10000)
                    {
                        sb.Append((char)codePoint);
                    }
                    else
                    {
                        sb.Append((char)(0xD800 + ((codePoint - 0x10000) >> 10)));
                        sb.Append((char)(0xDC00 + ((codePoint - 0x10000) & 0x3FF)));
                    }
                    s += 2;
                    continue;
                }
                reason = IllegalReason;
            }
            // every failure event covers only the lead byte, and a handler
            // resumes there, so an invalid trail byte is re-read as ASCII
            if (!PyBytesObjectType.ApplyDecodeHandler(data, s, s + 1, reason, errors, ErrorName, source, sb, out var next, out var error))
                return error!.Value;
            s = next;
        }
        return PyStrObject.FromString(sb.ToString());
    }

    internal static PyResult Encode(string value, string errors)
    {
        var codePoints = PyStrObject.ToCodePointArray(value);
        var bytes = new List<byte>(value.Length);
        for (var i = 0; i < codePoints.Length; i++)
        {
            int codePoint = codePoints[i];
            if (codePoint < 0x80)
            {
                bytes.Add((byte)codePoint);
                continue;
            }
            if (EncodeMap.TryGetValue(codePoint, out var code))
            {
                bytes.Add((byte)(code >> 8));
                bytes.Add((byte)code);
                continue;
            }
            switch (errors)
            {
                case "ignore":
                    break;
                case "replace":
                    bytes.Add((byte)'?');
                    break;
                case "backslashreplace" or "xmlcharrefreplace" or "namereplace":
                    foreach (var c in PyStrObjectType.EncodeErrorReplacement(errors, codePoint))
                        bytes.Add((byte)c);
                    break;
                default:
                    return errors is "strict"
                        ? PyResult.FromException(UnicodeEncodeError(value, i))
                        : PyResult.LookupError(PySR.Runtime_Codec_UnknownErrorHandlerName, errors);
            }
        }
        return PyBytesObject.MoveBytes([.. bytes]);
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

    private static int TrailIndex(int trail) => trail <= 0x7E ? trail - 0x40 : trail - 0xA1 + 63;
}
