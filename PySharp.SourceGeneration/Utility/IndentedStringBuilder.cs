using System.Text;

namespace PySharp.SourceGeneration.Utility;

internal class IndentedStringBuilder
{
    private readonly StringBuilder _builder = new();
    private readonly int _indentSize;
    private readonly char _indentChar;
    private int _indentLevel = 0;
    private bool _isNewLine = true;

    public IndentedStringBuilder(int indentSize = 4, char indentChar = ' ')
    {
        _indentSize = indentSize;
        _indentChar = indentChar;
    }

    public IndentedStringBuilder Indent()
    {
        _indentLevel++;
        return this;
    }

    public IndentedStringBuilder Dedent()
    {
        if (_indentLevel > 0)
            _indentLevel--;
        return this;
    }

    public void EnsureIndent()
    {
        if (!_isNewLine)
            return;

        _isNewLine = false;
        _builder.Append(_indentChar, _indentLevel * _indentSize);
    }

    public IndentedStringBuilder Append(string value)
    {
        if (string.IsNullOrEmpty(value))
            return this;

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

    public IndentedStringBuilder AppendLine(string value)
    {
        Append(value);
        return AppendLine();
    }

    public IndentedStringBuilder AppendLine(char value)
    {
        Append(value);
        return AppendLine();
    }

    public IndentedStringBuilder AppendLine()
    {
        _builder.AppendLine();
        _isNewLine = true;
        return this;
    }

    public override string ToString()
    {
        return _builder.ToString();
    }
}