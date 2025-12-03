namespace PySharp.Tokenization;

public class TokenizationException : Exception
{
    public TokenizationException()
    {
    }

    public TokenizationException(string? message) : base(message)
    {
    }

    public TokenizationException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}
