using System;
using System.Collections.Generic;
using System.Text;

namespace PySharp.CodeAnalysis;

public readonly record struct CodeTextLineSpan
{
    public static readonly CodeTextLineSpan Empty;

    public readonly int Start;
    public readonly int Length;
    public readonly int LineBreakLength;

    public CodeTextLineSpan(int start, int length, int lineBreakLength)
    {
        Start = start;
        Length = length;
        LineBreakLength = lineBreakLength;
    }
}
