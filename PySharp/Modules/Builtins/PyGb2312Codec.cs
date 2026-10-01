using System.IO.Compression;

namespace PySharp.Modules.Builtins;

/// <summary>
/// The GB2312 mapping, backed by the generated table in
/// <c>PyGb2312Codec.Tables.cs</c>: every EUC pair in the 0xA1-0xFE grid,
/// with the unassigned holes left as unmapped so codecs layered on top
/// (hz's 7-bit GB sections) reject exactly what CPython rejects.
/// </summary>
internal static partial class PyGb2312Codec
{
    private static readonly int[] DecodeTable = BuildDecodeTable();
    private static readonly Dictionary<int, int> EncodeMap = BuildEncodeMap();

    private static int[] BuildDecodeTable()
    {
        var bytes = new byte[94 * 94 * 3];
        using var compressed = new MemoryStream(Convert.FromBase64String(TableData));
        using var zlib = new ZLibStream(compressed, CompressionMode.Decompress);
        zlib.ReadExactly(bytes);
        var table = new int[94 * 94];
        for (var i = 0; i < table.Length; i++)
            table[i] = (bytes[i * 3 + 2] << 16) | (bytes[i * 3 + 1] << 8) | bytes[i * 3];
        return table;
    }

    private static Dictionary<int, int> BuildEncodeMap()
    {
        var map = new Dictionary<int, int>(DecodeTable.Length);
        for (var i = 0; i < DecodeTable.Length; i++)
        {
            var codePoint = DecodeTable[i];
            if (codePoint is not Unmapped)
                map.TryAdd(codePoint, (0xA1 + i / 94 << 8) | (0xA1 + i % 94));
        }
        return map;
    }

    /// <summary>The code point for an EUC pair, or -1 when unmapped.</summary>
    internal static int DecodePair(int lead, int trail)
    {
        if (lead is < 0xA1 or > 0xFE || trail is < 0xA1 or > 0xFE)
            return -1;
        var codePoint = DecodeTable[(lead - 0xA1) * 94 + (trail - 0xA1)];
        return codePoint is Unmapped ? -1 : codePoint;
    }

    /// <summary>The EUC byte pair for a code point, or false when unmapped.</summary>
    internal static bool TryEncode(int codePoint, out byte lead, out byte trail)
    {
        if (EncodeMap.TryGetValue(codePoint, out var code))
        {
            lead = (byte)(code >> 8);
            trail = (byte)code;
            return true;
        }
        lead = 0;
        trail = 0;
        return false;
    }
}
