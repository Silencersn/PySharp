using System.Text;

namespace PySharp.Utility;

public sealed class IndentedStringBuilder
{
    private readonly StringBuilder _builder;
    private readonly List<string> _indents = [];
    private bool _isNewLine;

    public IndentedStringBuilder()
    {
        _builder = new StringBuilder();
        _isNewLine = true;
        _indents = [];
    }

    public IndentedStringBuilder IncrementIndent(string indent)
    {
        _indents.Add(indent);
        return this;
    }

    public IndentedStringBuilder DecrementIndent()
    {
        if (_indents.Count is 0)
            throw new InvalidOperationException();

        _indents.RemoveAt(_indents.Count - 1);
        return this;
    }

    public Indenter Indent(string indent = "  ")
    {
        return new Indenter(this, indent);
    }

    private void EnsureIndent()
    {
        if (!_isNewLine)
            return;

        _isNewLine = false;
        _builder.AppendJoin(string.Empty, _indents);
    }

    public IndentedStringBuilder Append(ReadOnlySpan<char> value)
    {
        EnsureIndent();
        _builder.Append(value);
        return this;
    }

    public IndentedStringBuilder Append(char value)
    {
        EnsureIndent();
        _builder.Append(value);
        return this;
    }

    public IndentedStringBuilder Append(char value, int repeatCount)
    {
        EnsureIndent();
        _builder.Append(value, repeatCount);
        return this;
    }

    public IndentedStringBuilder AppendFormat(string value, params ReadOnlySpan<object> args)
    {
        EnsureIndent();
        _builder.AppendFormat(value, args);
        return this;
    }

    public IndentedStringBuilder AppendLine()
    {
        EnsureIndent();
        _builder.AppendLine();
        _isNewLine = true;
        return this;
    }

    public IndentedStringBuilder AppendLine(ReadOnlySpan<char> value)
    {
        return Append(value)
            .AppendLine();
    }

    public override string ToString()
    {
        return _builder.ToString();
    }

    public readonly ref struct Indenter : IDisposable
    {
        private readonly IndentedStringBuilder _builder;

        internal Indenter(IndentedStringBuilder builder, string indent)
        {
            _builder = builder;
            _builder.IncrementIndent(indent);
        }

        void IDisposable.Dispose()
        {
            _builder?.DecrementIndent();
        }
    }
}
