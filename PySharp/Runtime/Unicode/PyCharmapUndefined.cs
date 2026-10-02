using System.Collections.Frozen;

namespace PySharp.Runtime.Unicode;

/// <summary>
/// CPython's single-byte charmap codecs refuse the bytes their decoding
/// tables leave undefined ('character maps to <undefined>'), while the
/// .NET code pages map them to C1 controls or private-use characters and
/// pass them silently. The table mirrors CPython 3.14's Lib/encodings:
/// the undefined bytes for the decode side, and the characters the .NET
/// tables produce for them for the encode side - all of them outside
/// CPython's tables, so encoding them is undefined there too.
/// </summary>
internal static class PyCharmapUndefined
{
    internal readonly record struct Undefined(FrozenSet<byte> Bytes, FrozenSet<char> Chars);

    /// <summary>
    /// Looks the codec up by its normalized name, resolving the
    /// Lib/encodings aliases to their codec first.
    /// </summary>
    internal static bool TryGet(string normalizedName, out Undefined undefined)
    {
        var key = _aliases.TryGetValue(normalizedName, out var target) ? target : normalizedName;
        return _undefined.TryGetValue(key, out undefined);
    }

// Generated against CPython 3.14.6 Lib/encodings: the bytes each
// single-byte codec's decoding_table marks as undefined (0xFFFE). The
// .NET code pages instead map them to C1 controls or private-use
// characters, which made these positions encode and decode silently
// where CPython raises 'character maps to <undefined>'.

private static readonly FrozenDictionary<string, Undefined> _undefined = new Dictionary<string, Undefined>
{
    ["cp1250"] = new([ 0x81, 0x83, 0x88, 0x90, 0x98 ], [ '\u0081', '\u0083', '\u0088', '\u0090', '\u0098' ]),
    ["cp1251"] = new([ 0x98 ], [ '\u0098' ]),
    ["cp1252"] = new([ 0x81, 0x8D, 0x8F, 0x90, 0x9D ], [ '\u0081', '\u008D', '\u008F', '\u0090', '\u009D' ]),
    ["cp1253"] = new([ 0x81, 0x88, 0x8A, 0x8C, 0x8D, 0x8E, 0x8F, 0x90, 0x98, 0x9A, 0x9C, 0x9D, 0x9E, 0x9F, 0xAA, 0xD2, 0xFF ], [ '\u0081', '\u0088', '\u008A', '\u008C', '\u008D', '\u008E', '\u008F', '\u0090', '\u0098', '\u009A', '\u009C', '\u009D', '\u009E', '\u009F', '\uF8F9', '\uF8FA', '\uF8FB' ]),
    ["cp1254"] = new([ 0x81, 0x8D, 0x8E, 0x8F, 0x90, 0x9D, 0x9E ], [ '\u0081', '\u008D', '\u008E', '\u008F', '\u0090', '\u009D', '\u009E' ]),
    ["cp1255"] = new([ 0x81, 0x8A, 0x8C, 0x8D, 0x8E, 0x8F, 0x90, 0x9A, 0x9C, 0x9D, 0x9E, 0x9F, 0xCA, 0xD9, 0xDA, 0xDB, 0xDC, 0xDD, 0xDE, 0xDF, 0xFB, 0xFC, 0xFF ], [ '\u0081', '\u008A', '\u008C', '\u008D', '\u008E', '\u008F', '\u0090', '\u009A', '\u009C', '\u009D', '\u009E', '\u009F', '\u05BA', '\uF88D', '\uF88E', '\uF88F', '\uF890', '\uF891', '\uF892', '\uF893', '\uF894', '\uF895', '\uF896' ]),
    ["cp1257"] = new([ 0x81, 0x83, 0x88, 0x8A, 0x8C, 0x90, 0x98, 0x9A, 0x9C, 0x9F, 0xA1, 0xA5 ], [ '\u0081', '\u0083', '\u0088', '\u008A', '\u008C', '\u0090', '\u0098', '\u009A', '\u009C', '\u009F', '\uF8FC', '\uF8FD' ]),
    ["cp1258"] = new([ 0x81, 0x8A, 0x8D, 0x8E, 0x8F, 0x90, 0x9A, 0x9D, 0x9E ], [ '\u0081', '\u008A', '\u008D', '\u008E', '\u008F', '\u0090', '\u009A', '\u009D', '\u009E' ]),
    ["cp857"] = new([ 0xD5, 0xE7, 0xF2 ], [ '\uF8BB', '\uF8BC', '\uF8BD' ]),
    ["cp864"] = new([ 0x9B, 0x9C, 0x9F, 0xA6, 0xA7, 0xFF ], [ '\u009B', '\u009C', '\u009F', '\uF8BE', '\uF8BF', '\uF8C0' ]),
    ["cp869"] = new([ 0x80, 0x81, 0x82, 0x83, 0x84, 0x85, 0x87, 0x93, 0x94 ], [ '\u0080', '\u0081', '\u0082', '\u0083', '\u0084', '\u0085', '\u0087', '\u0093', '\u0094' ]),
    ["cp874"] = new([ 0x81, 0x82, 0x83, 0x84, 0x86, 0x87, 0x88, 0x89, 0x8A, 0x8B, 0x8C, 0x8D, 0x8E, 0x8F, 0x90, 0x98, 0x99, 0x9A, 0x9B, 0x9C, 0x9D, 0x9E, 0x9F, 0xDB, 0xDC, 0xDD, 0xDE, 0xFC, 0xFD, 0xFE, 0xFF ], [ '\u0081', '\u0082', '\u0083', '\u0084', '\u0086', '\u0087', '\u0088', '\u0089', '\u008A', '\u008B', '\u008C', '\u008D', '\u008E', '\u008F', '\u0090', '\u0098', '\u0099', '\u009A', '\u009B', '\u009C', '\u009D', '\u009E', '\u009F', '\uF8C1', '\uF8C2', '\uF8C3', '\uF8C4', '\uF8C5', '\uF8C6', '\uF8C7', '\uF8C8' ]),
    ["iso88593"] = new([ 0xA5, 0xAE, 0xBE, 0xC3, 0xD0, 0xE3, 0xF0 ], [ '\uF7F5', '\uF7F6', '\uF7F7', '\uF7F8', '\uF7F9', '\uF7FA', '\uF7FB' ]),
    ["iso88596"] = new([ 0xA1, 0xA2, 0xA3, 0xA5, 0xA6, 0xA7, 0xA8, 0xA9, 0xAA, 0xAB, 0xAE, 0xAF, 0xB0, 0xB1, 0xB2, 0xB3, 0xB4, 0xB5, 0xB6, 0xB7, 0xB8, 0xB9, 0xBA, 0xBC, 0xBD, 0xBE, 0xC0, 0xDB, 0xDC, 0xDD, 0xDE, 0xDF, 0xF3, 0xF4, 0xF5, 0xF6, 0xF7, 0xF8, 0xF9, 0xFA, 0xFB, 0xFC, 0xFD, 0xFE, 0xFF ], [ '\uF7C8', '\uF7C9', '\uF7CA', '\uF7CB', '\uF7CC', '\uF7CD', '\uF7CE', '\uF7CF', '\uF7D0', '\uF7D1', '\uF7D2', '\uF7D3', '\uF7D4', '\uF7D5', '\uF7D6', '\uF7D7', '\uF7D8', '\uF7D9', '\uF7DA', '\uF7DB', '\uF7DC', '\uF7DD', '\uF7DE', '\uF7DF', '\uF7E0', '\uF7E1', '\uF7E2', '\uF7E3', '\uF7E4', '\uF7E5', '\uF7E6', '\uF7E7', '\uF7E8', '\uF7E9', '\uF7EA', '\uF7EB', '\uF7EC', '\uF7ED', '\uF7EE', '\uF7EF', '\uF7F0', '\uF7F1', '\uF7F2', '\uF7F3', '\uF7F4' ]),
    ["iso88597"] = new([ 0xAE, 0xD2, 0xFF ], [ '\uF7C5', '\uF7C6', '\uF7C7' ]),
    ["iso88598"] = new([ 0xA1, 0xBF, 0xC0, 0xC1, 0xC2, 0xC3, 0xC4, 0xC5, 0xC6, 0xC7, 0xC8, 0xC9, 0xCA, 0xCB, 0xCC, 0xCD, 0xCE, 0xCF, 0xD0, 0xD1, 0xD2, 0xD3, 0xD4, 0xD5, 0xD6, 0xD7, 0xD8, 0xD9, 0xDA, 0xDB, 0xDC, 0xDD, 0xDE, 0xFB, 0xFC, 0xFF ], [ '\uF79C', '\uF79D', '\uF79E', '\uF79F', '\uF7A0', '\uF7A1', '\uF7A2', '\uF7A3', '\uF7A4', '\uF7A5', '\uF7A6', '\uF7A7', '\uF7A8', '\uF7A9', '\uF7AA', '\uF7AB', '\uF7AC', '\uF7AD', '\uF7AE', '\uF7AF', '\uF7B0', '\uF7B1', '\uF7B2', '\uF7B3', '\uF7B4', '\uF7B5', '\uF7B6', '\uF7B7', '\uF7B8', '\uF7B9', '\uF7BA', '\uF7BB', '\uF7BC', '\uF7BD', '\uF7BE', '\uF7C1' ]),
    ["kz1048"] = new([ 0x98 ], [ '\u2264' ]),
}.ToFrozenDictionary();

// Lib/encodings/aliases.py entries pointing at those codecs, in the
// normalization of PyStrObjectType.NormalizeEncodingName
private static readonly FrozenDictionary<string, string> _aliases = new Dictionary<string, string>
{
    ["1250"] = "cp1250",
    ["1251"] = "cp1251",
    ["1252"] = "cp1252",
    ["1253"] = "cp1253",
    ["1254"] = "cp1254",
    ["1255"] = "cp1255",
    ["1257"] = "cp1257",
    ["1258"] = "cp1258",
    ["857"] = "cp857",
    ["864"] = "cp864",
    ["869"] = "cp869",
    ["874"] = "cp874",
    ["arabic"] = "iso88596",
    ["asmo708"] = "iso88596",
    ["cpgr"] = "cp869",
    ["csibm857"] = "cp857",
    ["csibm864"] = "cp864",
    ["csibm869"] = "cp869",
    ["csisolatin3"] = "iso88593",
    ["csisolatinarabic"] = "iso88596",
    ["csisolatingreek"] = "iso88597",
    ["csisolatinhebrew"] = "iso88598",
    ["ecma114"] = "iso88596",
    ["ecma118"] = "iso88597",
    ["elot928"] = "iso88597",
    ["greek"] = "iso88597",
    ["greek8"] = "iso88597",
    ["hebrew"] = "iso88598",
    ["ibm857"] = "cp857",
    ["ibm864"] = "cp864",
    ["ibm869"] = "cp869",
    ["iso885931988"] = "iso88593",
    ["iso885961987"] = "iso88596",
    ["iso885971987"] = "iso88597",
    ["iso885981988"] = "iso88598",
    ["iso88598e"] = "iso88598",
    ["iso88598i"] = "iso88598",
    ["isoir109"] = "iso88593",
    ["isoir126"] = "iso88597",
    ["isoir127"] = "iso88596",
    ["isoir138"] = "iso88598",
    ["l3"] = "iso88593",
    ["latin3"] = "iso88593",
    ["ms874"] = "cp874",
    ["rk1048"] = "kz1048",
    ["strk10482002"] = "kz1048",
    ["windows1250"] = "cp1250",
    ["windows1251"] = "cp1251",
    ["windows1252"] = "cp1252",
    ["windows1253"] = "cp1253",
    ["windows1254"] = "cp1254",
    ["windows1255"] = "cp1255",
    ["windows1257"] = "cp1257",
    ["windows1258"] = "cp1258",
    ["windows874"] = "cp874",
}.ToFrozenDictionary();
}
