namespace PySharp.CodeAnalysis;

public readonly record struct CodeTextSpan
{
    public readonly int Start;
    public readonly int Length;

    public CodeTextSpan(int start, int length)
    {
        Start = start;
        Length = length;
    }
}