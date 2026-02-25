using System;
using System.Collections.Generic;

namespace PySharp.SourceGeneration.Utility;

internal static class IndentedStringBuilderExtensions
{
    public static IndentedStringBuilder ForEach<T>(this IndentedStringBuilder builder, IEnumerable<T> values, Action<IndentedStringBuilder, T> action)
    {
        foreach (var value in values)
            action(builder, value);
        return builder;
    }

    public static IndentedStringBuilder EnterBlock(this IndentedStringBuilder builder)
    {
        return builder.AppendLine('{').Indent();
    }

    public static IndentedStringBuilder ExitBlock(this IndentedStringBuilder builder)
    {
        return builder.Dedent().AppendLine('}');
    }
}
