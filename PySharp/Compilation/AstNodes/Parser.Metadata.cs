using PySharp.Compilation.CodeAnalysis;

namespace PySharp.Compilation.AstNodes;

partial class Parser
{
    internal readonly record struct AstMetaInfo
    {
        private readonly Parser _parser;
        public readonly int StartTokenPosition;
        public readonly int EndTokenPosition;
        public readonly int CrucialStartTokenPosition;
        public readonly int CrucialEndTokenPosition;

        public AstMetaInfo(Parser parser, int startTokenPosition, int endTokenPosition, int crucialStartTokenPosition, int crucialEndTokenPosition)
        {
            _parser = parser;
            StartTokenPosition = startTokenPosition;
            EndTokenPosition = endTokenPosition;
            CrucialStartTokenPosition = crucialStartTokenPosition;
            CrucialEndTokenPosition = crucialEndTokenPosition;
        }
        public AstMetaInfo(Parser parser, int startTokenPosition, int endTokenPosition) : this(parser, startTokenPosition, endTokenPosition, 0, 0)
        {
        }
        public AstMetaInfo(Parser parser, int tokenPosition) : this(parser, tokenPosition, tokenPosition)
        {
        }

        public static implicit operator CodeMetaInfo(AstMetaInfo metaInfo) => metaInfo.ToCodeMetaInfo();
        public static implicit operator ValueCodeMetaInfo(AstMetaInfo metaInfo) => metaInfo.ToValueCodeMetaInfo();

        public AstMetaInfo WithEnd()
        {
            return new(_parser, StartTokenPosition, _parser.TokenPosition, CrucialStartTokenPosition, CrucialEndTokenPosition);
        }
        public AstMetaInfo WithCrucial()
        {
            return new(_parser, StartTokenPosition, EndTokenPosition, _parser.TokenPosition, _parser.TokenPosition);
        }
        public AstMetaInfo WithAllEnd()
        {
            return new(_parser, StartTokenPosition, _parser.TokenPosition, CrucialStartTokenPosition, _parser.TokenPosition);
        }
        public AstMetaInfo WithPreviousEnd()
        {
            var position = _parser.TokenPosition - 1;
            var span = _parser._tokenSequence.AsSpan();
            while (IsUselessToken(span[position]))
                position--;
            return new(_parser, StartTokenPosition, position, CrucialStartTokenPosition, CrucialEndTokenPosition);
        }

        public CodeMetaInfo ToCodeMetaInfo()
        {
            var span = _parser._tokenSequence.AsSpan();
            var startToken = span[StartTokenPosition];
            var endToken = span[EndTokenPosition];
            var rangeLength = endToken.StringSpan.End - startToken.StringSpan.Start;
            var source = _parser._codeSource;

            if (CrucialStartTokenPosition is 0 && CrucialEndTokenPosition is 0)
                return CodeMetaInfo.FromPosition(source,
                    startToken.GetStart(source), endToken.GetEnd(source));

            var crucialStartToken = span[CrucialStartTokenPosition];
            var crucialEndToken = span[CrucialEndTokenPosition];
            return CodeMetaInfo.FromSpan(source,
                new(startToken.StringSpan.Start, rangeLength),
                new(crucialStartToken.StringSpan.Start, crucialEndToken.StringSpan.End - crucialStartToken.StringSpan.Start));
        }
        public ValueCodeMetaInfo ToValueCodeMetaInfo()
        {
            var span = _parser._tokenSequence.AsSpan();
            var startToken = span[StartTokenPosition];
            var endToken = span[EndTokenPosition];
            var rangeLength = endToken.StringSpan.End - startToken.StringSpan.Start;

            if (CrucialStartTokenPosition is 0 && CrucialEndTokenPosition is 0)
                return ValueCodeMetaInfo.FromSpan(new(startToken.StringSpan.Start, rangeLength), default);

            var crucialStartToken = span[CrucialStartTokenPosition];
            var crucialEndToken = span[CrucialEndTokenPosition];
            return ValueCodeMetaInfo.FromSpan(
                new(startToken.StringSpan.Start, rangeLength),
                new(crucialStartToken.StringSpan.Start, crucialEndToken.StringSpan.End - crucialStartToken.StringSpan.Start));
        }
    }

    internal AstMetaInfo CreateAstMetaInfo()
    {
        return new AstMetaInfo(this, TokenPosition);
    }
}
