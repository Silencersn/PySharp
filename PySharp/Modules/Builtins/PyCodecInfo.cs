using System.Collections.Frozen;

namespace PySharp.Modules.Builtins;

/// <summary>
/// The codec classes UnicodeError messages depend on. Each codec reports
/// itself under a fixed name (the generic single byte codecs as 'charmap',
/// the CJK C codecs under their registry names), words an unmappable
/// character in its own way, and decides how far a failure reaches: the
/// utf-8, ascii, latin-1 and charmap encoders extend it over the whole run
/// of consecutive unmappable code points, the utf-16, utf-32 and multibyte
/// encoders report a single unit.
/// </summary>
internal static class PyCodecInfo
{
    internal enum CodecKind
    {
        Utf8,
        Utf16,
        Utf16Le,
        Utf16Be,
        Utf32,
        Utf32Le,
        Utf32Be,
        Latin1,
        Ascii,
        Charmap,
        Multibyte,
        Mbcs,
        Other,
    }

    internal readonly struct Codec(CodecKind kind, string errorName, string reason, bool collectsRun, bool emitsBom = false)
    {
        internal CodecKind Kind { get; } = kind;

        /// <summary>Name the codec reports in its error messages.</summary>
        internal string ErrorName { get; } = errorName;

        /// <summary>Wording of an unmappable character in this codec's errors.</summary>
        internal string Reason { get; } = reason;

        internal bool CollectsRun { get; } = collectsRun;

        /// <summary>Native-order BOM ahead of the encoded data.</summary>
        internal bool EmitsBom { get; } = emitsBom;

        // CPython's surrogatepass handler knows the five standard encodings
        // and hands the original error back for every other codec
        internal bool SupportsSurrogatePass => Kind
            is CodecKind.Utf8 or CodecKind.Utf16 or CodecKind.Utf16Le or CodecKind.Utf16Be
            or CodecKind.Utf32 or CodecKind.Utf32Le or CodecKind.Utf32Be;

        /// <summary>
        /// Bytes one code point occupies in the fixed-width codecs, which
        /// require an error handler's replacement to fill whole units.
        /// </summary>
        internal int UnitSize => Kind
            is CodecKind.Utf16 or CodecKind.Utf16Le or CodecKind.Utf16Be ? 2
            : Kind is CodecKind.Utf32 or CodecKind.Utf32Le or CodecKind.Utf32Be ? 4
            : 1;
    }

    internal const string SurrogatesNotAllowed = "surrogates not allowed";
    internal const string CharmapReason = "character maps to <undefined>";
    internal const string MultibyteReason = "illegal multibyte sequence";

    // Codec name -> the name its C implementation reports. Aliases follow
    // Lib/encodings/aliases.py, in the normalization of
    // PyStrObjectType.NormalizeEncodingName (letters and digits only).
    private static readonly FrozenDictionary<string, string> _multibyteNames = new Dictionary<string, string>
    {
        ["big5"] = "big5",
        ["big5tw"] = "big5",
        ["csbig5"] = "big5",
        ["big5hkscs"] = "big5hkscs",
        ["hkscs"] = "big5hkscs",
        ["cp932"] = "cp932",
        ["932"] = "cp932",
        ["ms932"] = "cp932",
        ["mskanji"] = "cp932",
        ["windows31j"] = "cp932",
        ["cp949"] = "cp949",
        ["949"] = "cp949",
        ["ms949"] = "cp949",
        ["uhc"] = "cp949",
        ["cp950"] = "cp950",
        ["950"] = "cp950",
        ["ms950"] = "cp950",
        ["gbk"] = "gbk",
        ["936"] = "gbk",
        ["cp936"] = "gbk",
        ["ms936"] = "gbk",
        ["gb2312"] = "gb2312",
        ["chinese"] = "gb2312",
        ["csiso58gb231280"] = "gb2312",
        ["euccn"] = "gb2312",
        ["eucgb2312cn"] = "gb2312",
        ["gb23121980"] = "gb2312",
        ["gb231280"] = "gb2312",
        ["isoir58"] = "gb2312",
        ["gb18030"] = "gb18030",
        ["gb180302000"] = "gb18030",
        ["hz"] = "hz",
        ["hzgb"] = "hz",
        ["hzgb2312"] = "hz",
        ["johab"] = "johab",
        ["cp1361"] = "johab",
        ["ms1361"] = "johab",
        ["shiftjis"] = "shift_jis",
        ["csshiftjis"] = "shift_jis",
        ["sjis"] = "shift_jis",
        ["shiftjis2004"] = "shift_jis_2004",
        ["sjis2004"] = "shift_jis_2004",
        ["shiftjisx0213"] = "shift_jisx0213",
        ["sjisx0213"] = "shift_jisx0213",
        ["eucjp"] = "euc_jp",
        ["ujis"] = "euc_jp",
        ["eucjis2004"] = "euc_jis_2004",
        ["jisx0213"] = "euc_jis_2004",
        ["eucjisx0213"] = "euc_jisx0213",
        ["euckr"] = "euc_kr",
        ["korean"] = "euc_kr",
        ["mackorean"] = "euc_kr",
        ["ksc5601"] = "euc_kr",
        ["ksc56011987"] = "euc_kr",
        ["ksx1001"] = "euc_kr",
        ["cseuckr"] = "euc_kr",
        ["iso2022jp"] = "iso2022_jp",
        ["csiso2022jp"] = "iso2022_jp",
        ["iso2022jp1"] = "iso2022_jp_1",
        ["iso2022jp2"] = "iso2022_jp_2",
        ["csiso2022jp2"] = "iso2022_jp_2",
        ["iso2022jp3"] = "iso2022_jp_3",
        ["iso2022jp2004"] = "iso2022_jp_2004",
        ["iso2022jpext"] = "iso2022_jp_ext",
        ["iso2022kr"] = "iso2022_kr",
    }.ToFrozenDictionary();

    internal static Codec Classify(string normalizedName)
    {
        if (IsLatin1(normalizedName))
            return new(CodecKind.Latin1, "latin-1", "ordinal not in range(256)", collectsRun: true);
        if (IsAscii(normalizedName))
            return new(CodecKind.Ascii, "ascii", "ordinal not in range(128)", collectsRun: true);

        switch (normalizedName)
        {
            case "utf8" or "utf8sig":
                return new(CodecKind.Utf8, "utf-8", SurrogatesNotAllowed, collectsRun: true, emitsBom: normalizedName is "utf8sig");
            case "utf16":
                return new(CodecKind.Utf16, "utf-16", SurrogatesNotAllowed, collectsRun: false, emitsBom: true);
            case "utf16le":
                return new(CodecKind.Utf16Le, "utf-16-le", SurrogatesNotAllowed, collectsRun: false);
            case "utf16be":
                return new(CodecKind.Utf16Be, "utf-16-be", SurrogatesNotAllowed, collectsRun: false);
            case "utf32":
                return new(CodecKind.Utf32, "utf-32", SurrogatesNotAllowed, collectsRun: false, emitsBom: true);
            case "utf32le":
                return new(CodecKind.Utf32Le, "utf-32-le", SurrogatesNotAllowed, collectsRun: false);
            case "utf32be":
                return new(CodecKind.Utf32Be, "utf-32-be", SurrogatesNotAllowed, collectsRun: false);
            case "mbcs":
                return new(CodecKind.Mbcs, "mbcs", "invalid character", collectsRun: false);
            case "utf7":
                return new(CodecKind.Other, "utf-7", "invalid character", collectsRun: false);
        }

        if (_multibyteNames.TryGetValue(normalizedName, out var multibyteName))
            return new(CodecKind.Multibyte, multibyteName, MultibyteReason, collectsRun: false);

        return new(CodecKind.Charmap, "charmap", CharmapReason, collectsRun: true);
    }

    // the alias families PyStrObjectType.GetEncoding maps to Latin-1 and ascii
    internal static bool IsLatin1(string normalizedName) => normalizedName
        is "latin1" or "latin" or "l1" or "8859" or "88591" or "iso8859" or "iso88591" or "iso885911987"
        or "isoir100" or "csisolatin1" or "ibm819" or "cp819";

    internal static bool IsAscii(string normalizedName) => normalizedName
        is "ascii" or "usascii" or "us" or "646" or "iso646us" or "ansix341968" or "ansix341986" or "isoir6"
        or "csascii" or "ibm367" or "cp367";
}
