namespace PySharp.Compilation.CodeAnalysis;

public readonly record struct CodeTextPosition
{
    public static readonly CodeTextPosition Empty;

    public readonly int Line;
    public readonly int Offset;
    public readonly bool IsEmpty => this == Empty;

    public CodeTextPosition(int line, int offset)
    {
        Line = line;
        Offset = offset;
    }
}
