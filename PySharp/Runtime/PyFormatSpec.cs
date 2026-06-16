using System.Diagnostics.CodeAnalysis;

namespace PySharp.Runtime;

internal readonly struct PyFormatSpec
{
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
        formatSpec = default;

        PyFormatSpecData data = default;
        if (!PyFormatSpecData.TryParse(format, ref data))
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

        public static bool TryParse(ReadOnlySpan<char> format, ref PyFormatSpecData formatSpecData)
        {
            formatSpecData = default;
            ParseOptions(ref format, ref formatSpecData);
            ParseWidthAndPrecision(ref format, ref formatSpecData);
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

        private static void ParseWidthAndPrecision(ref ReadOnlySpan<char> format, ref PyFormatSpecData formatSpecData)
        {
            ParseWidthWithGrouping(ref format, ref formatSpecData.Width, ref formatSpecData.WidthGrouping);
            ParsePrecisionWithGrouping(ref format, ref formatSpecData.Precision, ref formatSpecData.PrecisionGrouping);
        }
        private static void ParseWidthWithGrouping(ref ReadOnlySpan<char> format, ref ReadOnlySpan<char> width, ref char? grouping)
        {
            ParseWidthOrPrecision(ref format, ref width);
            ParseGrouping(ref format, ref grouping);
        }
        private static void ParsePrecisionWithGrouping(ref ReadOnlySpan<char> format, ref ReadOnlySpan<char> precision, ref char? grouping)
        {
            if (format.Length > 0 && format[0] is '.')
            {
                format = format[1..];
                precision = default;
                ParseWidthOrPrecision(ref format, ref precision);
                ParseGrouping(ref format, ref grouping);
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
        private static void ParseGrouping(ref ReadOnlySpan<char> format, ref char? grouping)
        {
            if (format.Length > 0 && IsGrouping(format[0]))
            {
                grouping = format[0];
                format = format[1..];
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