using System.Text;

namespace PySharp.Runtime.Calls;

// CPython's "Did you mean ...?" search (Python/suggestions.c
// _Py_CalculateSuggestions): a byte-level Levenshtein distance over UTF-8
// where a substitution costs 2, a case-only flip costs 1, both operands are
// capped at 40 bytes and a candidate is only considered when at most a third
// of the involved characters would have to change.
internal static class PyNameSuggestions
{
    private const int MaxCandidateItems = 750;
    private const int MaxStringSize = 40;
    private const int MoveCost = 2;
    private const int CaseCost = 1;

    internal static string? Calculate(ReadOnlySpan<string> candidates, string name)
    {
        if (candidates.Length >= MaxCandidateItems)
            return null;

        var nameBytes = Encoding.UTF8.GetBytes(name);
        string? suggestion = null;
        var suggestionDistance = int.MaxValue;

        foreach (var candidate in candidates)
        {
            if (string.Equals(candidate, name, StringComparison.Ordinal))
                continue;

            var itemBytes = Encoding.UTF8.GetBytes(candidate);
            var maxDistance = (nameBytes.Length + itemBytes.Length + 3) * MoveCost / 6;

            // don't take matches already beaten by the incumbent
            maxDistance = int.Min(maxDistance, suggestionDistance - 1);

            var distance = LevenshteinDistance(nameBytes, itemBytes, maxDistance);
            if (distance > maxDistance)
                continue;

            if (suggestion is null || distance < suggestionDistance)
            {
                suggestion = candidate;
                suggestionDistance = distance;
            }
        }

        return suggestion;
    }

    // levenshtein_distance (suggestions.c): one row of the distance matrix is
    // updated in place, and maxCost + 1 reports "further away than asked for"
    private static int LevenshteinDistance(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b, int maxCost)
    {
        while (a.Length is not 0 && b.Length is not 0 && a[0] == b[0])
        {
            a = a[1..];
            b = b[1..];
        }

        while (a.Length is not 0 && b.Length is not 0 && a[^1] == b[^1])
        {
            a = a[..^1];
            b = b[..^1];
        }

        if (a.Length is 0 || b.Length is 0)
            return (a.Length + b.Length) * MoveCost;

        if (a.Length > MaxStringSize || b.Length > MaxStringSize)
            return maxCost + 1;

        // the row buffer tracks the shorter operand
        if (b.Length < a.Length)
        {
            var shorter = b;
            b = a;
            a = shorter;
        }

        if ((b.Length - a.Length) * MoveCost > maxCost)
            return maxCost + 1;

        Span<int> row = stackalloc int[MaxStringSize];

        var cost = MoveCost;
        for (var i = 0; i < a.Length; i++)
        {
            row[i] = cost;
            cost += MoveCost;
        }

        var result = 0;
        for (var bIndex = 0; bIndex < b.Length; bIndex++)
        {
            var code = b[bIndex];
            var distance = result = bIndex * MoveCost;
            var minimum = int.MaxValue;

            for (var index = 0; index < a.Length; index++)
            {
                var substitute = distance + SubstitutionCost(code, a[index]);
                distance = row[index];
                result = int.Min(int.Min(result, distance) + MoveCost, substitute);
                row[index] = result;

                if (result < minimum)
                    minimum = result;
            }

            // every cell of this row is already over budget
            if (minimum > maxCost)
                return maxCost + 1;
        }

        return result;
    }

    // substitution_cost (suggestions.c): comparing the low five bits first
    // keeps case flips cheap without a table
    private static int SubstitutionCost(byte a, byte b)
    {
        if ((a & 31) != (b & 31))
            return MoveCost;

        if (a == b)
            return 0;

        if (a is >= (byte)'A' and <= (byte)'Z')
            a += 'a' - 'A';

        if (b is >= (byte)'A' and <= (byte)'Z')
            b += 'a' - 'A';

        return a == b ? CaseCost : MoveCost;
    }
}
