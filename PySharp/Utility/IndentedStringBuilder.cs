using System.Diagnostics;
using System.Text;

namespace PySharp.Utility;

public sealed class IndentedStringBuilder
{
    private readonly StringBuilder _builder;
    private readonly char _indentChar;
    private readonly int _indentSize;
    private readonly List<List<Action<IndentedStringBuilder>>> _duringIndentActionChain;
    private bool _isNewLine;
    private int _indentLevel;

    public IndentedStringBuilder(char indentChar = ' ', int indentSize = 2)
    {
        _builder = new StringBuilder();
        _isNewLine = true;
        _indentLevel = 0;
        _indentChar = indentChar;
        _indentSize = Math.Max(0, indentSize);
        _duringIndentActionChain = [];
        _duringIndentActionChain.Add([]);
    }

    public IndentedStringBuilder IncrementIndent()
    {
        _indentLevel++;
        _duringIndentActionChain.Add([]);
        return this;
    }

    public IndentedStringBuilder DecrementIndent()
    {
        if (_indentLevel is 0)
            return this;

        _indentLevel--;
        _duringIndentActionChain.RemoveAt(_duringIndentActionChain.Count - 1);
        return this;
    }

    public Indenter Indent()
    {
        return new Indenter(this);
    }

    public DuringIndentActionAttacher AttachDuringIndentAction(Action<IndentedStringBuilder> action)
    {
        return new DuringIndentActionAttacher(this, action);
    }

    private void EnsureIndent()
    {
        if (!_isNewLine)
            return;

        _isNewLine = false;
        
        Debug.Assert(_duringIndentActionChain.Count == _indentLevel + 1);
        foreach (var action in _duringIndentActionChain[0])
            action(this);
        for (int i = 0; i < _indentLevel; i++)
        {
            _builder.Append(_indentChar, _indentSize);
            foreach (var action in _duringIndentActionChain[i + 1])
                action(this);
        }
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

    public readonly ref struct DuringIndentActionAttacher : IDisposable
    {
        private readonly IndentedStringBuilder _builder;

        internal DuringIndentActionAttacher(IndentedStringBuilder builder, Action<IndentedStringBuilder> action)
        {
            _builder = builder;
            _builder._duringIndentActionChain.Last().Add(action);
        }

        void IDisposable.Dispose()
        {
            if (_builder is null)
                return;

            var last = _builder._duringIndentActionChain.Last();
            last.RemoveAt(last.Count - 1);
        }
    }
}
