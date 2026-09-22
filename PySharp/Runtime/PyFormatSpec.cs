using PySharp.Runtime.Calls;
using System.Globalization;

namespace PySharp.Runtime;

internal readonly struct PyFormatSpec
{
    /// <summary>
    /// CPython's unknown_presentation_type/invalid_thousands_separator_type:
    /// a code is shown literally only between ' ' and DEL, everything else
    /// (control characters, the space itself, non-ASCII) is escaped as \x%x.
    /// </summary>
    internal static string DescribeType(char type)
    {
        return type > 32 && type < 128
            ? type.ToString()
            : "\\x" + ((int)type).ToString("x", CultureInfo.InvariantCulture);
    }

    internal static PyResult UnknownCode(char type, string typeName)
    {
        return PyResult.ValueError(PySR.Runtime_Object_FormatUnknownCode, DescribeType(type), typeName);
    }

    internal static PyResult GroupingTypeError(char grouping, char type)
    {
        return PyResult.ValueError(PySR.Runtime_Object_FormatGroupingType, grouping, DescribeType(type));
    }

    /// <summary>
    /// CPython's parser treats one leftover character after the grammar
    /// prefix as the presentation type ("Unknown format code"); any other
    /// parse failure is a malformed spec. A grouping separator followed by
    /// an incompatible trailing type reports the grouping error instead.
    /// </summary>
    internal static PyResult ParseError(string spec, string typeName)
    {
        if (TryParse(spec.AsSpan()[..^1], out var prefix, out var prefixBoth) && prefix.Type is null)
        {
            var trailing = spec[^1];

            // a digit can only be the type when it directly follows a
            // grouping separator; otherwise the grammar would have
            // consumed it as part of the width
            var followsGrouping = prefix.WidthGrouping is not null || prefix.PrecisionGrouping is not null;
            if (!char.IsAsciiDigit(trailing) || followsGrouping)
            {
                if (prefixBoth || trailing is ',' or '_')
                    return PyResult.ValueError(PySR.Runtime_Object_FormatGroupingBoth);

                if (prefix.WidthGrouping is char grouping && !IsGroupingCompatibleType(grouping, trailing))
                    return GroupingTypeError(grouping, trailing);

                return UnknownCode(trailing, typeName);
            }
        }

        return PyResult.ValueError(PySR.Runtime_Object_FormatSpecInvalid, spec, typeName);
    }

    internal static bool IsGroupingCompatibleType(char grouping, char type) => type switch
    {
        'd' or 'e' or 'f' or 'g' or 'E' or 'G' or '%' or 'F' => true,
        'b' or 'o' or 'x' or 'X' => grouping is '_',
        _ => false,
    };

    /// <summary>
    /// CPython's post-parse grouping checks, shared by str/int/float: two
    /// separators at the same position (width or precision) cannot both be
    /// given (the parser flags that), and a width grouping must be
    /// compatible with the presentation type (an omitted type is judged
    /// like 'g'). A width grouping and a precision grouping may coexist.
    /// Returns a failed result only when invalid.
    /// </summary>
    internal static PyResult ValidateGrouping(PyFormatSpec spec, bool bothSeparators, char effectiveType)
    {
        if (bothSeparators)
            return PyResult.ValueError(PySR.Runtime_Object_FormatGroupingBoth);

        if (spec.WidthGrouping is not null && !IsGroupingCompatibleType(spec.WidthGrouping.Value, effectiveType))
            return GroupingTypeError(spec.WidthGrouping.Value, effectiveType);

        return default;
    }


    public char? Fill { get; }
    public char? Align { get; }
    public char? Sign { get; }
    public bool CoercePositiveZero { get; }
    public bool AlternateForm { get; }
    public bool SignAwareZeroPadding { get; }

    public int? Width { get; }
    public char? WidthGrouping { get; }

    public int? Precision { get; }
    public char? PrecisionGrouping { get; }

    public char? Type { get; }

    private PyFormatSpec(PyFormatSpecData data, int? width, int? precision)
    {
        Fill = data.Fill;
        Align = data.Align;
        Sign = data.Sign;
        CoercePositiveZero = data.CharLetterZ;
        AlternateForm = data.CharNumberSign;
        SignAwareZeroPadding = data.CharDigitZero;
        Width = width;
        WidthGrouping = data.WidthGrouping;
        Precision = precision;
        PrecisionGrouping = data.PrecisionGrouping;
        Type = data.Type;
    }


    public static bool TryParse(ReadOnlySpan<char> format, out PyFormatSpec formatSpec)
    {
        return TryParse(format, out formatSpec, out _);
    }

    public static bool TryParse(ReadOnlySpan<char> format, out PyFormatSpec formatSpec, out bool bothSeparators)
    {
        formatSpec = default;
        bothSeparators = false;

        PyFormatSpecData data = default;
        if (!PyFormatSpecData.TryParse(format, ref data, out bothSeparators))
            return false;

        int width = -1;
        if (!data.Width.IsEmpty && !int.TryParse(data.Width, out width))
            return false;

        int precision = -1;
        if (!data.Precision.IsEmpty && !int.TryParse(data.Precision, out precision))
            return false;

        formatSpec = new PyFormatSpec(data, width is -1 ? null : width, precision is -1 ? null : precision);
        return true;
    }

    private ref struct PyFormatSpecData
    {
        public char? Fill;
        public char? Align;
        public char? Sign;
        public bool CharLetterZ;
        public bool CharNumberSign;
        public bool CharDigitZero;

        public ReadOnlySpan<char> Width;
        public char? WidthGrouping;

        public ReadOnlySpan<char> Precision;
        public char? PrecisionGrouping;

        public char? Type;

        public static bool TryParse(ReadOnlySpan<char> format, ref PyFormatSpecData formatSpecData, out bool bothSeparators)
        {
            formatSpecData = default;
            bothSeparators = false;
            ParseOptions(ref format, ref formatSpecData);
            ParseWidthAndPrecision(ref format, ref formatSpecData, ref bothSeparators);
            ParseType(ref format, ref formatSpecData.Type);
            return format.Length is 0;
        }

        private static void ParseOptions(ref ReadOnlySpan<char> format, ref PyFormatSpecData formatSpecData)
        {
            ParseFillAlign(ref format, ref formatSpecData.Fill, ref formatSpecData.Align);
            ParseSign(ref format, ref formatSpecData.Sign);
            ParseOptionsFlags(ref format, ref formatSpecData.CharLetterZ, ref formatSpecData.CharNumberSign, ref formatSpecData.CharDigitZero);
        }
        private static void ParseFillAlign(ref ReadOnlySpan<char> format, ref char? fill, ref char? align)
        {
            if (format.Length > 1 && IsAlign(format[1]))
            {
                fill = format[0];
                align = format[1];
                format = format[2..];
            }
            else if (format.Length > 0 && IsAlign(format[0]))
            {
                align = format[0];
                format = format[1..];
            }
        }
        private static void ParseSign(ref ReadOnlySpan<char> format, ref char? sign)
        {
            if (format.Length > 0 && IsSign(format[0]))
            {
                sign = format[0];
                format = format[1..];
            }
        }
        private static void ParseOptionsFlags(ref ReadOnlySpan<char> format, ref bool charLetterZ, ref bool charNumberSign, ref bool charDigitZero)
        {
            if (format.Length > 0 && format[0] is 'z')
            {
                charLetterZ = true;
                format = format[1..];
            }
            if (format.Length > 0 && format[0] is '#')
            {
                charNumberSign = true;
                format = format[1..];
            }
            if (format.Length > 0 && format[0] is '0')
            {
                charDigitZero = true;
                format = format[1..];
            }
        }

        private static void ParseWidthAndPrecision(ref ReadOnlySpan<char> format, ref PyFormatSpecData formatSpecData, ref bool bothSeparators)
        {
            ParseWidthWithGrouping(ref format, ref formatSpecData.Width, ref formatSpecData.WidthGrouping, ref bothSeparators);
            ParsePrecisionWithGrouping(ref format, ref formatSpecData.Precision, ref formatSpecData.PrecisionGrouping, ref bothSeparators);
        }
        private static void ParseWidthWithGrouping(ref ReadOnlySpan<char> format, ref ReadOnlySpan<char> width, ref char? grouping, ref bool bothSeparators)
        {
            ParseWidthOrPrecision(ref format, ref width);
            ParseGrouping(ref format, ref grouping, ref bothSeparators);
        }
        private static void ParsePrecisionWithGrouping(ref ReadOnlySpan<char> format, ref ReadOnlySpan<char> precision, ref char? grouping, ref bool bothSeparators)
        {
            if (format.Length > 0 && format[0] is '.')
            {
                format = format[1..];
                precision = default;
                ParseWidthOrPrecision(ref format, ref precision);
                ParseGrouping(ref format, ref grouping, ref bothSeparators);
            }
        }
        private static void ParseWidthOrPrecision(ref ReadOnlySpan<char> format, ref ReadOnlySpan<char> widthOrPrecision)
        {
            int length = 0;
            for (int i = 0; i < format.Length; i++)
            {
                if (char.IsAsciiDigit(format[i]))
                    length++;
                else
                    break;

            }

            if (length > 0)
            {
                widthOrPrecision = format[..length].ToString();
                format = format[length..];
            }
        }
        private static void ParseGrouping(ref ReadOnlySpan<char> format, ref char? grouping, ref bool bothSeparators)
        {
            if (format.Length > 0 && IsGrouping(format[0]))
            {
                grouping = format[0];
                format = format[1..];

                // CPython reads one more separator: '_' after anything, or
                // a ',' after '_', raises the both-separators error (',,'
                // keeps parsing)
                if (format.Length > 0 && IsGrouping(format[0]) && (format[0] is '_' || grouping is '_'))
                {
                    bothSeparators = true;
                    format = format[1..];
                }
            }
        }
        private static void ParseType(ref ReadOnlySpan<char> format, ref char? type)
        {
            if (format.Length > 0 && IsType(format[0]))
            {
                type = format[0];
                format = format[1..];
            }
        }

        private static bool IsAlign(char c) => c is '<' or '>' or '=' or '^';
        private static bool IsSign(char c) => c is '+' or '-' or ' ';
        private static bool IsGrouping(char c) => c is ',' or '_';
        private static bool IsType(char c) => c is 'b' or 'c' or 'd' or 'e' or 'E' or 'f' or 'F' or 'g' or 'G' or 'n' or 'o' or 's' or 'x' or 'X' or '%';
    }
}
