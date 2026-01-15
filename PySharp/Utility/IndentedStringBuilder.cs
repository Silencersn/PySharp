using System;
using System.Collections.Generic;
using System.Text;

namespace PySharp.Utility;

public sealed class IndentedStringBuilder
{
    private readonly StringBuilder _builder;
    private readonly char _indentChar;
    private readonly int _indentSize;
    private bool _isNewLine;
    private int _indentLevel;

    public IndentedStringBuilder(char indentChar = ' ', int indentSize = 2)
    {
        _builder = new StringBuilder();
        _isNewLine = true;
        _indentLevel = 0;
        _indentChar = indentChar;
        _indentSize = Math.Max(0, indentSize);
    }

    public IndentedStringBuilder IncrementIndent()
    {
        _indentLevel++;
        return this;
    }

    public IndentedStringBuilder DecrementIndent()
    {
        if (_indentLevel is 0)
            return this;

        _indentLevel--;
        return this;
    }

    public Indenter Indent()
    {
        return new Indenter(this);
    }

    private void EnsureIndent()
    {
        if (!_isNewLine)
            return;

        _isNewLine = false;
        _builder.Append(_indentChar, _indentLevel * _indentSize);
    }

    public IndentedStringBuilder Append(ReadOnlySpan<char> value)
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

        internal Indenter(IndentedStringBuilder builder)
        {
            _builder = builder;
            _builder.IncrementIndent();
        }

        void IDisposable.Dispose()
        {
            _builder?.DecrementIndent();
        }
    }
}
