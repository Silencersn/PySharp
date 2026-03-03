using System;
using System.Collections.Generic;
using System.Text;

namespace PySharp.Compilation.CodeAnalysis;

public readonly record struct ValueCodeMetaInfo
{
    public static readonly ValueCodeMetaInfo Empty;

    public readonly CodeTextSpan Range;
    public readonly CodeTextSpan CrucialRange;

    public ValueCodeMetaInfo(CodeTextSpan range, CodeTextSpan crucialRange)
    {
        Range = range;
        CrucialRange = crucialRange;
    }

    public bool HasRange => !Range.IsEmpty;
    public bool HasCrucialRange => !CrucialRange.IsEmpty;
    public bool IsEmpty => Range.IsEmpty && CrucialRange.IsEmpty;

    internal static ValueCodeMetaInfo FromPosition(CodeSource source, CodeTextPosition start, CodeTextPosition end, CodeTextPosition crucialStart, CodeTextPosition crucialEnd)
    {
        return new ValueCodeMetaInfo(source.Code.PositionToSpan(start, end), source.Code.PositionToSpan(crucialStart, crucialEnd));
    }
    internal static ValueCodeMetaInfo FromPosition(CodeSource source, CodeTextPosition start, CodeTextPosition end)
    {
        return new ValueCodeMetaInfo(source.Code.PositionToSpan(start, end), CodeTextSpan.Empty);
    }
    internal static ValueCodeMetaInfo FromPosition(CodeSource source, CodeTextPosition start)
    {
        return FromPosition(source, start, CodeTextPosition.Empty);
    }

    internal static ValueCodeMetaInfo FromSpan(CodeTextSpan range, CodeTextSpan crucialRange)
    {
        return new ValueCodeMetaInfo(range, crucialRange);
    }
}
