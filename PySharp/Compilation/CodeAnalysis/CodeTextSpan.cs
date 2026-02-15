namespace PySharp.Compilation.CodeAnalysis;

public readonly record struct CodeTextSpan
{
    public static readonly CodeTextSpan Empty;

    public readonly int Start;
    public readonly int Length;
    public readonly bool IsEmpty => this == Empty;
    public readonly int End => Start + Length;

    public CodeTextSpan(int start, int length)
    {
        Start = start;
        Length = length;
    }
}