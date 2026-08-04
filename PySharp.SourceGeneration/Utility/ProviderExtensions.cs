using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace PySharp.SourceGeneration.Utility;

internal static class ProviderExtensions
{
    public static IncrementalValuesProvider<TSource> WhereNotNull<TSource>(this IncrementalValuesProvider<TSource?> source)
    {
        return source.Where(static item => item is not null)!;
    }
}
