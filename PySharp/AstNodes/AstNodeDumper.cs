using System.Text;

namespace PySharp.AstNodes;

internal sealed class AstNodeDumper
{
    private readonly StringBuilder _builder;
    private readonly char _indentCharacter;
    private readonly int _indentSize;
    private int _indentLevel;
    private bool _newLine;

    internal AstNodeDumper()
    {
        _builder = new StringBuilder();
        _indentCharacter = ' ';
        _indentSize = 4;
        _indentLevel = 0;
        _newLine = false;
    }

    public override string ToString()
    {
        return _builder.ToString();
    }

    private void EnsureIndented()
    {
        if (_newLine)
        {
            _builder.Append(_indentCharacter, _indentSize * _indentLevel);
            _newLine = false;
        }
    }

    public AstNodeDumper Forward()
    {
        _indentLevel++;
        return this;
    }

    public AstNodeDumper Backward()
    {
        _indentLevel--;
        return this;
    }

    public AstNodeDumper Append(string value)
    {
        EnsureIndented();
        _builder.Append(value);
        return this;
    }

    public AstNodeDumper Append(object? value)
    {
        EnsureIndented();
        if (value is AstNode node)
            Append(node);
        else if (value is ExprContextType context)
            Append(context);
        else if (value is IEnumerable<AstNode> nodes)
            AppendNodes([.. nodes]);
        else
            _builder.Append(value);
        return this;
    }

    public AstNodeDumper Append(AstNode node)
    {
        EnsureIndented();
        node.Dump(this);
        return this;
    }

    public AstNodeDumper Append(ExprContextType context)
    {
        return AppendFormat("{0}()", context);
    }

    public AstNodeDumper Append(params ReadOnlySpan<object?> values)
    {
        foreach (var value in values)
        {
            Append(value);
        }
        return this;
    }

    public AstNodeDumper AppendFormat(string format, params ReadOnlySpan<object?> args)
    {
        EnsureIndented();
        _builder.AppendFormat(format, args);
        return this;
    }

    public AstNodeDumper AppendLine()
    {
        EnsureIndented();
        _builder.AppendLine();
        _newLine = true;
        return this;
    }

    public AstNodeDumper AppendLine(string value)
    {
        EnsureIndented();
        _builder.AppendLine(value);
        _newLine = true;
        return this;
    }

    public AstNodeDumper AppendLine(params ReadOnlySpan<object?> values)
    {
        return Append(values).AppendLine();
    }

    public AstNodeDumper AppendFormatLine(string format, params ReadOnlySpan<object?> args)
    {
        return AppendFormat(format, args).AppendLine();
    }

    public AstNodeDumper AppendFields(params ReadOnlySpan<(string Field, object? Value)> fields)
    {
        Append('(');
        Forward();

        foreach (var (field, value) in fields)
        {
            if (value is null)
                continue;

            AppendLine();
            Append(field, '=', value, ',');
        }

        if (_builder[^1] is ',')
            _builder.Remove(_builder.Length - 1, 1);
        Backward();
        Append(')');
        return this;
    }

    public AstNodeDumper AppendNodes(params ReadOnlySpan<AstNode> nodes)
    {
        Append('[');
        Forward();

        for (int i = 0; i < nodes.Length; i++)
        {
            var node = nodes[i];
            AppendLine();
            if (i < nodes.Length - 1)
                Append(node, ',');
            else
                Append(node);
        }

        Backward();
        Append(']');
        return this;
    }
}



