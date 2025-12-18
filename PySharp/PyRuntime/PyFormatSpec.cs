using System;
using System.Collections.Generic;
using System.Text;

namespace PySharp.PyRuntime;

internal record struct PyFormatSpec
{
    public char Fill;
    public char Align;
    public char Sign;
    public bool CharLetterZ;
    public bool CharNumberSign;
    public bool CharDigitZero;

    public string? Width;
    public char WidthGrouping;

    public string? Precision;
    public char PrecisionGrouping;

    public char Type;

    public static bool TryParse(ReadOnlySpan<char> format, out PyFormatSpec formatSpec)
    {
        formatSpec = default;
        ParseOptions(ref format, ref formatSpec);
        ParseWidthAndPrecision(ref format, ref formatSpec);
        ParseType(ref format, ref formatSpec.Type);
        return format.Length is 0;
    }

    private static void ParseOptions(ref ReadOnlySpan<char> format, ref PyFormatSpec formatSpec)
    {
        ParseFillAlign(ref format, ref formatSpec.Fill, ref formatSpec.Align);
        ParseSign(ref format, ref formatSpec.Sign);
        ParseOptionsFlags(ref format, ref formatSpec.CharLetterZ, ref formatSpec.CharNumberSign, ref formatSpec.CharDigitZero);
    }
    private static void ParseFillAlign(ref ReadOnlySpan<char> format, ref char fill, ref char align)
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
    private static void ParseSign(ref ReadOnlySpan<char> format, ref char sign)
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
    
    private static void ParseWidthAndPrecision(ref ReadOnlySpan<char> format, ref PyFormatSpec formatSpec)
    {
        ParseWidthWithGrouping(ref format, ref formatSpec.Width, ref formatSpec.WidthGrouping);
        ParsePrecisionWithGrouping(ref format, ref formatSpec.Precision, ref formatSpec.PrecisionGrouping);
    }
    private static void ParseWidthWithGrouping(ref ReadOnlySpan<char> format, ref string? width, ref char grouping)
    {
        ParseWidthOrPrecision(ref format, ref width);
        ParseGrouping(ref format, ref grouping);
    }
    private static void ParsePrecisionWithGrouping(ref ReadOnlySpan<char> format, ref string? precision, ref char grouping)
    {
        if (format.Length > 0 && format[0] is '.')
        {
            format = format[1..];
            ParseWidthOrPrecision(ref format, ref precision);
            precision ??= string.Empty;
            ParseGrouping(ref format, ref grouping);
        }
    }
    private static void ParseWidthOrPrecision(ref ReadOnlySpan<char> format, ref string? widthOrPrecision)
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
    private static void ParseGrouping(ref ReadOnlySpan<char> format, ref char grouping)
    {
        if (format.Length > 0 && IsGrouping(format[0]))
        {
            grouping = format[0];
            format = format[1..];
        }
    }
    private static void ParseType(ref ReadOnlySpan<char> format, ref char type)
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
