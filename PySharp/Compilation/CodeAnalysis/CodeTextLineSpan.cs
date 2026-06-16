namespace PySharp.Compilation.CodeAnalysis;

public readonly record struct CodeTextLineSpan
{
    public static readonly CodeTextLineSpan Empty;

    public readonly int Start;
    public readonly int Length;
    public readonly int LineBreakLength;
    public readonly int End => Start + Length;
    public readonly int EndIncludingLineBreak => End + LineBreakLength;

    public CodeTextLineSpan(int start, int length, int lineBreakLength)
    {
        Start = start;
        Length = length;
        LineBreakLength = lineBreakLength;
    }
}
