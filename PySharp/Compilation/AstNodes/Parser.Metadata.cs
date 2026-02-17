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

        public AstMetaInfo WithEnd()
        {
            return new(_parser, StartTokenPosition, _parser.TokenStreamPosition, CrucialStartTokenPosition, CrucialEndTokenPosition);
        }
        public AstMetaInfo WithCrucial()
        {
            return new(_parser, StartTokenPosition, EndTokenPosition, _parser.TokenStreamPosition, _parser.TokenStreamPosition);
        }
        public AstMetaInfo WithAllEnd()
        {
            return new(_parser, StartTokenPosition, _parser.TokenStreamPosition, CrucialStartTokenPosition, _parser.TokenStreamPosition);
        }
        public AstMetaInfo WithPreviousEnd()
        {
            var position = _parser._tokenStream.Position - 1;
            while (IsUselessToken(_parser._tokenStream.GetTokenAt(position)))
                position--;
            return new(_parser, StartTokenPosition, position, CrucialStartTokenPosition, CrucialEndTokenPosition);
        }

        public CodeMetaInfo ToCodeMetaInfo()
        {
            var startToken = _parser._tokenStream.GetTokenAt(StartTokenPosition);
            var endToken = _parser._tokenStream.GetTokenAt(EndTokenPosition);
            var crucialStartToken = _parser._tokenStream.GetTokenAt(CrucialStartTokenPosition);
            var crucialEndToken = _parser._tokenStream.GetTokenAt(CrucialEndTokenPosition);

            return CodeMetaInfo.FromPosition(_parser._codeSource,
                startToken.Start, endToken.End, crucialStartToken.Start, crucialEndToken.End);
        }
    }

    internal AstMetaInfo CreateAstMetaInfo()
    {
        return new AstMetaInfo(this, TokenStreamPosition);
    }
}
