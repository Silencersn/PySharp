using System.Runtime.InteropServices;

namespace PySharp.Compilation.Tokenization;

public sealed class TokenSequence
{
    private readonly List<Token> _tokens;

    public Token this[int index] => _tokens[index];

    internal TokenSequence(List<Token> tokens)
    {
        _tokens = tokens;
    }
    public ReadOnlySpan<Token> AsSpan()
    {
        return CollectionsMarshal.AsSpan(_tokens);
    }
}