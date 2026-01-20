using System.Diagnostics.CodeAnalysis;

namespace PySharp.Resources;

internal static partial class PySR
{
    public static string Format([StringSyntax(StringSyntaxAttribute.CompositeFormat)] string format, params ReadOnlySpan<object?> args)
    {
        return string.Format(format, args);
    }
}
