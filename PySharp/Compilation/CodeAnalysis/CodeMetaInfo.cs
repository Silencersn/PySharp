namespace PySharp.Compilation.CodeAnalysis;

public sealed record class CodeMetaInfo
{
    private CodeMetaInfo(CodeSource source, CodeTextSpan range, CodeTextSpan crucialRange)
    {
        Source = source;
        Range = range;
        CrucialRange = crucialRange;
    }

    public readonly CodeSource Source;
    public readonly CodeTextSpan Range;
    public readonly CodeTextSpan CrucialRange;

    public ReadOnlySpan<char> FirstLine => Source.Code.GetLineOrDefault(Source.Code.OffsetToPosition(Range.Start).Line, false);
    public CodeTextPosition Start => Source.Code.OffsetToPosition(Range.Start);
    public CodeTextPosition End => Source.Code.OffsetToPosition(Range.End);
    public CodeTextPosition CrucialStart => Source.Code.OffsetToPosition(CrucialRange.Start);
    public CodeTextPosition CrucialEnd => Source.Code.OffsetToPosition(CrucialRange.End);
    public bool HasRange => !Range.IsEmpty;
    public bool HasCrucialRange => !CrucialRange.IsEmpty;

    internal static CodeMetaInfo FromPosition(CodeSource source, CodeTextPosition start, CodeTextPosition end, CodeTextPosition crucialStart, CodeTextPosition crucialEnd)
    {
        return new CodeMetaInfo(source, source.Code.PositionToSpan(start, end), source.Code.PositionToSpan(crucialStart, crucialEnd));
    }
    internal static CodeMetaInfo FromPosition(CodeSource source, CodeTextPosition start, CodeTextPosition end)
    {
        return new CodeMetaInfo(source, source.Code.PositionToSpan(start, end), CodeTextSpan.Empty);
    }
    internal static CodeMetaInfo FromPosition(CodeSource source, CodeTextPosition start)
    {
        return FromPosition(source, start, CodeTextPosition.Empty);
    }

    internal static CodeMetaInfo FromSpan(CodeSource source, CodeTextSpan range, CodeTextSpan crucialRange)
    {
        return new CodeMetaInfo(source, range, crucialRange);
    }
}



