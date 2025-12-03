namespace PySharp.Tokenization;

public class AstException : Exception
{
    public AstException()
    {
    }

    public AstException(string? message) : base(message)
    {
    }

    public AstException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}
