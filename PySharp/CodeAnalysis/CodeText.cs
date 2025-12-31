using System;
using System.Collections.Generic;
using System.Text;

namespace PySharp.CodeAnalysis;

public sealed class CodeText
{
    private readonly string _text;
    private readonly CodeTextLineSpan[] _lineSpans;

    public int LineCount => _lineSpans.Length - 1;

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

    private bool ValidateLineNumber(int lineNumber)
    {
        return lineNumber >= 1 && lineNumber < _lineSpans.Length;
    }

    private ReadOnlySpan<char> InternalGetLine(int linenoStartsFromOne, bool includingLineBreak)
    {
        var lineSpan = _lineSpans[linenoStartsFromOne];
        return _text.AsSpan()
            .Slice(lineSpan.Start, includingLineBreak ? (lineSpan.Length + lineSpan.LineBreakLength) : lineSpan.Length);
    }

    public ReadOnlySpan<char> GetLine(int linenoStartsFromOne, bool includingLineBreak)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(linenoStartsFromOne, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(linenoStartsFromOne, _lineSpans.Length);
        return InternalGetLine(linenoStartsFromOne, includingLineBreak);
    }

    public bool TryGetLine(int linenoStartsFromOne, bool includingLineBreak, out ReadOnlySpan<char> line)
    {
        if (!ValidateLineNumber(linenoStartsFromOne))
        {
            line = [];
            return false;
        }
        line = InternalGetLine(linenoStartsFromOne, includingLineBreak);
        return true;
    }

    private ReadOnlySpan<char> InternalGetMultiLines(int startLine, int endLine)
    {
        var startLineSpan = _lineSpans[startLine];
        var endLineSpan = _lineSpans[endLine];
        return _text.AsSpan()[startLineSpan.Start..endLineSpan.EndIncludingLineBreak];
    }

    public bool TryGetMultiLines(int startLine, int endLine, out ReadOnlySpan<char> multiLines)
    {
        if (!ValidateLineNumber(startLine) || !ValidateLineNumber(endLine) || startLine > endLine)
        {
            multiLines = [];
            return false;
        }

        multiLines = InternalGetMultiLines(startLine, endLine);
        return true;
    }

    public ReadOnlySpan<char> GetMultiLines(int startLine, int endLine)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(startLine, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(startLine, _lineSpans.Length);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(endLine, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(endLine, _lineSpans.Length);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(startLine, endLine);

        return InternalGetMultiLines(startLine, endLine);
    }

    private ReadOnlySpan<char> InternalGetRange(CodeTextPosition start, CodeTextPosition end)
    {
        int absStart = _lineSpans[start.Line].Start + start.Offset;
        int absEnd = _lineSpans[end.Line].Start + end.Offset;
        return _text.AsSpan(absStart, absEnd - absStart);
    }

    public ReadOnlySpan<char> GetRange(CodeTextPosition start, CodeTextPosition end)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(start.Line, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(start.Line, _lineSpans.Length);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(end.Line, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(end.Line, _lineSpans.Length);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(start.Line, end.Line);

        var startLineSpan = _lineSpans[start.Line];
        var endLineSpan = _lineSpans[end.Line];
        ArgumentOutOfRangeException.ThrowIfLessThan(start.Offset, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(start.Offset, startLineSpan.Length);
        ArgumentOutOfRangeException.ThrowIfLessThan(end.Offset, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(end.Offset, endLineSpan.Length);
        if (start.Line == end.Line)
            ArgumentOutOfRangeException.ThrowIfGreaterThan(start.Offset, end.Offset);

        return InternalGetRange(start, end);
    }

    public bool TryGetRange(CodeTextPosition start, CodeTextPosition end, out ReadOnlySpan<char> range)
    {
        if (!ValidateLineNumber(start.Line) || !ValidateLineNumber(end.Line))
        {
            range = [];
            return false;
        }
        var startLineSpan = _lineSpans[start.Line];
        var endLineSpan = _lineSpans[end.Line];

        if (start.Offset < 0 || start.Offset > startLineSpan.Length ||
            end.Offset < 0 || end.Offset > endLineSpan.Length)
        {
            range = [];
            return false;
        }

        if (start.Line > end.Line || (start.Line == end.Line && start.Offset > end.Offset))
        {
            range = [];
            return false;
        }

        range = InternalGetRange(start, end);
        return true;
    }
}
