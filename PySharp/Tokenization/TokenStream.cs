namespace PySharp.Tokenization;

public abstract class TokenStream
{
    internal int _parsingCompoundStmt;
    internal bool IsParsingCompoundStmt => _parsingCompoundStmt > 0;

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
    private readonly TokenInfo[] _tokens;

    public TokenArrayStream(IEnumerable<TokenInfo> tokens)
    {
        ArgumentNullException.ThrowIfNull(tokens);

        _tokens = [.. tokens];
        _position = 0;
    }

    private int _position;

    public override int Position
    {
        get => _position;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(value, _tokens.Length);

            _position = value;
        }
    }
    public override TokenInfo CurrentToken => _tokens[_position];

    public override void MoveNextToken()
    {
        if (_position >= _tokens.Length)
            throw new EndOfStreamException();

        _position++;
    }

    public override TokenInfo GetTokenAt(int index)
    {
        return _tokens[index];
    }
}