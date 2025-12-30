using PySharp.PyRuntime.Calls;

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
}

public sealed class TokenInteractiveStream : TokenStream
{
    private readonly TextReader _in;
    private readonly TextWriter _out;
    private readonly Lexer _lexer;
    private int _position;

    public TokenInteractiveStream(PyCallContext context, TextReader input, TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);

        _in = input;
        _out = output;
        _lexer = new Lexer(context);
        _lexer.InternalStart();
        MoveNextToken();
    }

    public override int Position
    {
        get => _position;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(value, _lexer.Tokens.Count);

            _position = value;
        }
    }

    public override TokenInfo CurrentToken
    {
        get
        {
            while (_position >= _lexer.Tokens.Count)
            {
                TryAppendTokens();
            }
            return _lexer.Tokens[_position];
        }
    }

    public override void MoveNextToken()
    {
        _position++;
    }

    private void TryAppendTokens()
    {
        _out.Write(IsParsingCompoundStmt ? "... " : ">>> ");
        var line = _in.ReadLine() ?? throw new EndOfStreamException();

        if (string.IsNullOrWhiteSpace(line))
        {
            _lexer.InternalClearIndentation();
            if (IsParsingCompoundStmt)
                _lexer.Tokens.Add(new TokenInfo(TokenType.NewLine, string.Empty, default, default, string.Empty));
        }
        else
        {
            _lexer.InternalTokenize(line + Environment.NewLine);
        }
    }
}