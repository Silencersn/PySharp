using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;

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
