using System.Linq;

namespace PySharp.SourceGeneration.Utility;

internal static class ProviderExtensions
{
    public static IncrementalValuesProvider<TSource> WhereNotNull<TSource>(this IncrementalValuesProvider<TSource?> source)
    {
        return source.Where(static item => item is not null)!;
    }
}
