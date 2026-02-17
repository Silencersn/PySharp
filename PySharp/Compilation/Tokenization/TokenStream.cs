namespace PySharp.Compilation.Tokenization;

public abstract class TokenStream
{
    public abstract int Position { get; set; }
    /// <summary>
    /// CurrentToken should be set after ctor (that means CurrentToken is always available)
    /// </summary>
    public abstract TokenInfo CurrentToken { get; }
    public abstract void MoveNextToken();
    public abstract TokenInfo GetTokenAt(int index);
}

public sealed class TokenArrayStream : TokenStream
{
    private readonly List<TokenInfo> _tokens;

    internal TokenArrayStream(List<TokenInfo> tokens)
    {
        _tokens = tokens;
        _position = 0;
    }

    private int _position;

    public override int Position
    {
        get => _position;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(value, _tokens.Count);

            _position = value;
        }
    }
    public override TokenInfo CurrentToken => _tokens[_position];
    internal int Count => _tokens.Count;

    public override void MoveNextToken()
    {
        if (_position >= _tokens.Count)
            throw new EndOfStreamException();

        _position++;
    }

    public override TokenInfo GetTokenAt(int index)
    {
        return _tokens[index];
    }

    internal void Insert(int value, TokenInfo tokenInfo)
    {
        _tokens.Insert(value, tokenInfo);
    }
}