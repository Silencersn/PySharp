using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PySharp.SourceGeneration.Utility;

internal static class AttributeDataExtensions
{
    public static IEnumerable<AttributeData> GetAttributes(this IMethodSymbol symbol, string fullyQualifiedAttributeName, bool inherit)
    {
        foreach (var attributeData in symbol.GetAttributes())
        {
            if (attributeData.AttributeClass is null)
                continue;

            if (attributeData.AttributeClass.ToDisplayString() == fullyQualifiedAttributeName)
                yield return attributeData;
        }

        if (!inherit || symbol.OverriddenMethod is null)
            yield break;

        foreach (var attributeData in GetAttributes(symbol.OverriddenMethod, fullyQualifiedAttributeName, inherit) ?? [])
            yield return attributeData;
    }

    public static bool IsDefined(this IMethodSymbol symbol, string fullyQualifiedAttributeName, bool inherit)
    {
        return GetAttributes(symbol, fullyQualifiedAttributeName, inherit).Any();
    }
}
