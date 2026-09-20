using System.Globalization;
using System.Text;

namespace PySharp.Modules.Builtins;

// CPython's per-code-point predicates (Objects/unicodetype_db.h). Letter and
// decimal membership follows the general category; the tables below hold the
// derived properties (Other_Uppercase, Other_Lowercase, Numeric_Type) that
// System.Globalization does not expose.
internal static class PyUnicodeData
{
    // CPython Py_UNICODE_ISSPACE (29 code points, 10 ranges)
    private static readonly (int Start, int End)[] Spaces =
    [
        (9, 13), (28, 32), (133, 133), (160, 160),
        (5760, 5760), (8192, 8202), (8232, 8233), (8239, 8239),
        (8287, 8287), (12288, 12288),
    ];

    // Numeric_Type=Digit beyond Nd (128 code points, 20 ranges)
    private static readonly (int Start, int End)[] DigitExtras =
    [
        (178, 179), (185, 185), (4969, 4977), (6618, 6618),
        (8304, 8304), (8308, 8313), (8320, 8329), (9312, 9320),
        (9332, 9340), (9352, 9360), (9450, 9450), (9461, 9469),
        (9471, 9471), (10102, 10110), (10112, 10120), (10122, 10130),
        (68160, 68163), (69216, 69224), (69714, 69722), (127232, 127242),
    ];

    // Numeric_Type=Numeric beyond Nd/Nl/No (91 code points, 82 ranges)
    private static readonly (int Start, int End)[] NumericExtras =
    [
        (13317, 13317), (13443, 13443), (14378, 14378), (15181, 15181),
        (19968, 19968), (19971, 19971), (19975, 19975), (19977, 19977),
        (20004, 20004), (20061, 20061), (20108, 20108), (20116, 20116),
        (20118, 20118), (20140, 20140), (20159, 20160), (20191, 20191),
        (20200, 20200), (20237, 20237), (20336, 20336), (20457, 20457),
        (20486, 20486), (20740, 20740), (20806, 20806), (20841, 20841),
        (20843, 20843), (20845, 20845), (21313, 21313), (21315, 21317),
        (21324, 21324), (21441, 21444), (22235, 22235), (22769, 22769),
        (22777, 22777), (24186, 24186), (24318, 24319), (24332, 24334),
        (24336, 24336), (25296, 25296), (25342, 25342), (25420, 25420),
        (26578, 26578), (27934, 27934), (28422, 28422), (29590, 29590),
        (30334, 30334), (30357, 30357), (31213, 31213), (32902, 32902),
        (33836, 33836), (36014, 36014), (36019, 36019), (36144, 36144),
        (37390, 37390), (38057, 38057), (38433, 38433), (38470, 38470),
        (38476, 38476), (38520, 38520), (38646, 38646), (63851, 63851),
        (63859, 63859), (63864, 63864), (63922, 63922), (63953, 63953),
        (63955, 63955), (63997, 63997), (131073, 131073), (131172, 131172),
        (131298, 131298), (131361, 131361), (133418, 133418), (133507, 133507),
        (133516, 133516), (133532, 133532), (133866, 133866), (133885, 133885),
        (133913, 133913), (140176, 140176), (141720, 141720), (146203, 146203),
        (156269, 156269), (194704, 194704),
    ];

    // Other_Uppercase beyond Lu (120 code points, 5 ranges)
    private static readonly (int Start, int End)[] UpperExtras =
    [
        (8544, 8559), (9398, 9423), (127280, 127305), (127312, 127337),
        (127344, 127369),
    ];

    // Other_Lowercase beyond Ll (311 code points, 28 ranges)
    private static readonly (int Start, int End)[] LowerExtras =
    [
        (170, 170), (186, 186), (688, 696), (704, 705),
        (736, 740), (837, 837), (890, 890), (4348, 4348),
        (7468, 7530), (7544, 7544), (7579, 7615), (8305, 8305),
        (8319, 8319), (8336, 8348), (8560, 8575), (9424, 9449),
        (11388, 11389), (42652, 42653), (42864, 42864), (42994, 42996),
        (43000, 43001), (43868, 43871), (43881, 43881), (67456, 67456),
        (67459, 67461), (67463, 67504), (67506, 67514), (122928, 122989),
    ];

    public static bool IsAlpha(int codePoint) => Category(codePoint) is
        UnicodeCategory.UppercaseLetter or UnicodeCategory.LowercaseLetter or
        UnicodeCategory.TitlecaseLetter or UnicodeCategory.ModifierLetter or
        UnicodeCategory.OtherLetter;

    public static bool IsDecimal(int codePoint) => Category(codePoint) is UnicodeCategory.DecimalDigitNumber;

    public static bool IsDigit(int codePoint) => IsDecimal(codePoint) || InRanges(DigitExtras, codePoint);

    public static bool IsNumeric(int codePoint) => IsDecimal(codePoint) || InRanges(NumericExtras, codePoint) ||
        Category(codePoint) is UnicodeCategory.LetterNumber or UnicodeCategory.OtherNumber;

    public static bool IsAlnum(int codePoint) => IsAlpha(codePoint) || IsDecimal(codePoint) ||
        IsDigit(codePoint) || IsNumeric(codePoint);

    public static bool IsSpace(int codePoint) => InRanges(Spaces, codePoint);

    public static bool IsUpper(int codePoint) =>
        Category(codePoint) is UnicodeCategory.UppercaseLetter || InRanges(UpperExtras, codePoint);

    public static bool IsLower(int codePoint) =>
        Category(codePoint) is UnicodeCategory.LowercaseLetter || InRanges(LowerExtras, codePoint);

    public static bool IsTitle(int codePoint) => Category(codePoint) is UnicodeCategory.TitlecaseLetter;

    public static bool IsCased(int codePoint) => IsUpper(codePoint) || IsLower(codePoint) || IsTitle(codePoint);

    // Py_UNICODE_ISPRINTABLE: private-use and unassigned code points are not
    public static bool IsPrintable(int codePoint)
    {
        if (codePoint is 0x20 || (0x1F < codePoint && codePoint < 0x7F))
            return true;

        return Category(codePoint) is not
            (
                UnicodeCategory.Control or
                UnicodeCategory.Format or
                UnicodeCategory.Surrogate or
                UnicodeCategory.PrivateUse or
                UnicodeCategory.OtherNotAssigned or
                UnicodeCategory.LineSeparator or
                UnicodeCategory.ParagraphSeparator or
                UnicodeCategory.SpaceSeparator
            );
    }

    // Rune cannot represent a surrogate, so those are looked up by code unit
    private static UnicodeCategory Category(int codePoint) => codePoint <= 0xFFFF
        ? char.GetUnicodeCategory((char)codePoint)
        : Rune.GetUnicodeCategory(new Rune(codePoint));

    private static bool InRanges((int Start, int End)[] ranges, int codePoint)
    {
        var low = 0;
        var high = ranges.Length - 1;
        while (low <= high)
        {
            var mid = (low + high) >> 1;
            if (codePoint < ranges[mid].Start)
                high = mid - 1;
            else if (codePoint > ranges[mid].End)
                low = mid + 1;
            else
                return true;
        }
        return false;
    }
}
