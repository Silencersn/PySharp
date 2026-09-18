using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace PySharp.Resources;

internal static partial class PySR
{
    public static string Format([StringSyntax(StringSyntaxAttribute.CompositeFormat)] string format, params ReadOnlySpan<object?> args)
    {
        if (args.IsEmpty)
            return format;

        return string.Format(CultureInfo.InvariantCulture, format, args);
    }
}
