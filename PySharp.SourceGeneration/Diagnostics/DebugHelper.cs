using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace PySharp.SourceGeneration.Diagnostics;

internal static class DebugHelper
{
    [Conditional("DEBUG")]
    public static void AssertNotNull([NotNull] object? value)
    {
        Debug.Assert(value is not null);
        if (value is null)
            throw new Exception("Unreachable");
    }
}
