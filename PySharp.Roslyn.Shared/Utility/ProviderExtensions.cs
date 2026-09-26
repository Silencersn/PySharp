using System.Linq;

namespace PySharp.Roslyn.Shared.Utility;

internal static class ProviderExtensions
{
    public static IncrementalValuesProvider<TSource> WhereNotNull<TSource>(this IncrementalValuesProvider<TSource?> source)
    {
        return source.Where(static item => item is not null)!;
    }
}
