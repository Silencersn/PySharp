using System;
using System.Collections.Generic;
using System.Text;

namespace PySharp.CodeAnalysis;

public sealed class CodeText
{
    private readonly string _text;
    private readonly CodeTextLineSpan[] _lineSpans;

    public CodeText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        _text = text;

        List<CodeTextLineSpan> lineInfos = [CodeTextLineSpan.Empty];
        int currentIndex = 0;
        while (true)
        {
            int nextIndex = _text.IndexOfAny(['\r', '\n'], currentIndex);
            if (nextIndex is -1)
            {
                lineInfos.Add(new CodeTextLineSpan(currentIndex, _text.Length - currentIndex, 0));
                break;
            }

            int lineBreakLength = 1;
            if (_text[nextIndex] is '\r' && nextIndex + 1 < _text.Length && _text[nextIndex + 1] is '\n')
                lineBreakLength++;
            lineInfos.Add(new CodeTextLineSpan(currentIndex, nextIndex - currentIndex, lineBreakLength));
            currentIndex = nextIndex + lineBreakLength;
        }
        _lineSpans = [.. lineInfos];
    }

    public ReadOnlySpan<char> GetLine(int linenoStartsFromOne, bool includingLineBreak)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(linenoStartsFromOne, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(linenoStartsFromOne, _lineSpans.Length);

        var lineSpan = _lineSpans[linenoStartsFromOne];
        return _text.AsSpan()
            .Slice(lineSpan.Start, includingLineBreak ? (lineSpan.Length + lineSpan.LineBreakLength) : lineSpan.Length);
    }

    public bool TryGetLine(int linenoStartsFromOne, bool includingLineBreak, out ReadOnlySpan<char> line)
    {
        if (linenoStartsFromOne <= 0 || linenoStartsFromOne >= _lineSpans.Length)
        {
            line = [];
            return false;
        }

        var lineSpan = _lineSpans[linenoStartsFromOne];
        line = _text.AsSpan()
            .Slice(lineSpan.Start, includingLineBreak ? (lineSpan.Length + lineSpan.LineBreakLength) : lineSpan.Length);
        return true;
    }
}
