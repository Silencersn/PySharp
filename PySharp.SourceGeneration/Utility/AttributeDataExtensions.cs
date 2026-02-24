using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PySharp.SourceGeneration.Utility;

internal static class AttributeDataExtensions
{
    public static IEnumerable<AttributeData> GetAttributes(this ISymbol symbol, string fullyQualifiedAttributeName)
    {
        foreach (var attributeData in symbol.GetAttributes())
        {
            if (attributeData.AttributeClass is null)
                continue;

            if (attributeData.AttributeClass.ToDisplayString() == fullyQualifiedAttributeName)
                yield return attributeData;
        }
    }

    public static AttributeData? GetAttribute(this ISymbol symbol, string fullyQualifiedAttributeName)
    {
        foreach (var attributeData in symbol.GetAttributes())
        {
            if (attributeData.AttributeClass is null)
                continue;

            if (attributeData.AttributeClass.ToDisplayString() == fullyQualifiedAttributeName)
                return attributeData;
        }

        return null;
    }

    public static IEnumerable<AttributeData> GetAttributes(this IMethodSymbol symbol, string fullyQualifiedAttributeName, bool inherit)
    {
        foreach (var attributeData in GetAttributes(symbol, fullyQualifiedAttributeName))
            yield return attributeData;

        if (!inherit || symbol.OverriddenMethod is null)
            yield break;

        foreach (var attributeData in GetAttributes(symbol.OverriddenMethod, fullyQualifiedAttributeName, inherit) ?? [])
            yield return attributeData;
    }

    public static bool IsDefined(this IMethodSymbol symbol, string fullyQualifiedAttributeName, bool inherit)
    {
        return GetAttributes(symbol, fullyQualifiedAttributeName, inherit).Any();
    }

    public static T? GetNamedArgumentOrDefault<T>(this AttributeData attributeData, string key, T? defaultValue)
    {
        foreach (var pair in attributeData.NamedArguments)
        {
            if (pair.Key != key)
                continue;

            return (T?)pair.Value.Value;
        }

        return defaultValue;
    }

    public static string GetNamedArgumentLiteralOrDefault(this AttributeData attributeData, string key, string defaultValue)
    {
        foreach (var pair in attributeData.NamedArguments)
        {
            if (pair.Key != key)
                continue;

            return pair.Value.ToCSharpString();
        }

        return defaultValue;
    }
}
