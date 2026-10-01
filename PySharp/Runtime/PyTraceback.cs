using PySharp.Compilation.CodeAnalysis;
using PySharp.Modules.Builtins;
using PySharp.Runtime.Calls;
using PySharp.Utility;
using System.Diagnostics;
using System.Text;

namespace PySharp.Runtime;

internal static class PyTraceback
{
    public static PyTracebackObject CaptureCurrentFrame(PyCallContext context)
    {
        ref var frame = ref context.CurrentInternalFrame;
        if (frame.CodeObject is not null)
        {
            var info = frame.CodeObject.Bytecode.LineTable.Read(frame.InstructionIndex);
            return new PyTracebackObject(info, null);
        }
        return new PyTracebackObject(null, null);
    }

    // One node for the frame an exception is leaving. An inlined comprehension
    // (PEP 709) shares its enclosing frame's code object, so its node carries
    // the enclosing frame's name and the comprehension line — the same single
    // entry CPython records, since it has no separate frame for a comprehension.
    public static PyTracebackObject? CaptureFrameNode(PyCallContext context)
    {
        ref var frame = ref context.CurrentInternalFrame;
        if (frame.CodeObject is null)
            return null;

        return new PyTracebackObject(frame.CodeObject.Bytecode.LineTable.Read(frame.InstructionIndex), frame.CallerName);
    }

    // CPython tags a traceback created on a non-main thread with a header
    // naming that thread, taken from the thread-root frame the propagation
    // started in.
    public static string? CaptureThreadInfo(PyCallContext context)
    {
        for (int i = 0; i < context.FrameState.CurrentFrameCount; i++)
        {
            ref var frame = ref context.FrameState.GetFrame(i);
            if (frame.FrameType is FrameType.ThreadRoot)
                return $"Exception in thread Thread-{Environment.CurrentManagedThreadId} ({frame.CallerName}):";
        }

        return null;
    }

    // Prints the chain from the outermost frame towards the raise site — the
    // "most recent call last" order — folding runs of more than three frames
    // that repeat the same source position (CPython tb_printinternal with
    // TB_RECURSIVE_CUTOFF).
    internal static void Print(IndentedStringBuilder builder, PyTracebackObject? traceback)
    {
        CodeMetaInfo? preInfo = null;
        int repeatCount = 0;
        for (var node = traceback; node is not null; node = node._next)
        {
            if (node._info is null)
                continue;

            if (node._info == preInfo)
            {
                repeatCount++;
            }
            else
            {
                if (repeatCount > 3)
                {
                    using (builder.Indent())
                        builder.AppendLine($"[Previous line repeated {repeatCount - 3} more times]");
                }

                repeatCount = 1;
            }

            if (repeatCount <= 3)
                Print(builder, node._info, node._callerName);
            preInfo = node._info;
        }

        if (repeatCount > 3)
        {
            using (builder.Indent())
                builder.AppendLine($"[Previous line repeated {repeatCount - 3} more times]");
        }
    }

    internal static void Print(IndentedStringBuilder builder, CodeMetaInfo info, string? callerName)
    {
        using (builder.Indent())
        {
            builder.AppendFormat("File \"{0}\", line {1}", info.Source?.Name ?? "<unknown>", info.Start.Line);
            if (callerName is not null)
                builder.AppendFormat(", in {0}", callerName);
            builder.AppendLine();

            var origLine = info.FirstLine.TrimEnd();
            var line = origLine.TrimStart();

            if (line.Length is 0)
                return;

            using (builder.Indent())
            {
                builder.AppendLine(line);

                if (!info.HasRange)
                    return;

                var offset = line.Length - origLine.Length;
                var start = info.Start.Offset + offset;
                var end = info.End.Line == info.Start.Line
                    ? info.End.Offset + offset
                    : line.Length;

                if (start == end)
                    return;

                Debug.Assert(end > start);

                if (StatementShapeSuppressesCarets(info, line.ToString(), start, end))
                    return;

                if (!info.HasCrucialRange || info.CrucialStart.Line != info.Start.Line)
                {
                    if (start >= 0 && end - start < line.Length)
                    {
                        // if the line is full of '^', do not draw
                        builder
                            .Append(' ', start)
                            .Append('^', end - start)
                            .AppendLine();
                    }
                    return;
                }

                var crucialStart = info.CrucialStart.Offset + offset;
                var crucialEnd = info.CrucialEnd.Line == info.Start.Line
                    ? info.CrucialEnd.Offset + offset
                    : line.Length;
                Debug.Assert(crucialEnd > crucialStart);
                Debug.Assert(end >= crucialEnd);
                Debug.Assert(crucialStart >= start);

                builder
                    .Append(' ', start)
                    .Append('~', crucialStart - start)
                    .Append('^', crucialEnd - crucialStart)
                    .Append('~', end - crucialEnd)
                    .AppendLine();
            }
        }
    }

    // The first caret rule reads the statement shape before the anchor pass:
    // `return f(...)` and a plain-name `x = f(...)` whose call spans the whole
    // instruction range would only repeat what the source line already shows,
    // so they print no caret row at all — even though the call instruction
    // does carry anchors. Everything else keeps them: tuple/attribute/
    // subscript assignment targets, compound statement heads (`if c: x =`),
    // trailing operands (`x = f() + 1`), and bare expression statements.
    private static bool StatementShapeSuppressesCarets(CodeMetaInfo info, string line, int start, int end)
    {
        var prefix = line[..Math.Min(start, line.Length)].Trim();
        bool requireNameCall;
        if (prefix is "return")
        {
            // only a named function makes the return-shape cut: `return obj.m()`
            // still shows its anchors
            requireNameCall = true;
        }
        else
        {
            if (prefix.Length < 2 || prefix[^1] is not '=' || !IsPythonIdentifier(prefix[..^1].TrimEnd()))
                return false;
            // the assignment shape accepts any callee: `x = obj.m()` cuts too
            requireNameCall = false;
        }

        string segment;
        if (info.End.Line == info.Start.Line)
        {
            if (end > line.Length)
                return false;
            // content after the range (`x = f() + 1`) means the right-hand
            // value is a larger expression than the call and keeps its carets
            if (line.AsSpan(end).Trim().Length is not 0)
                return false;
            segment = line[start..end];
        }
        else
        {
            var lastRaw = info.Source.Code.GetLineOrDefault(info.End.Line, false);
            var lastTrim = lastRaw.TrimStart();
            // same convention as the first line: the offset shifts by the
            // indent the trimmed display line dropped
            var endInLast = info.End.Offset + (lastTrim.Length - lastRaw.Length);
            if (endInLast > lastTrim.Length || lastTrim[endInLast..].Trim().Length is not 0)
                return false;

            var builder = new StringBuilder(line[start..]);
            for (var l = info.Start.Line + 1; l < info.End.Line; l++)
                builder.Append('\n').Append(info.Source.Code.GetLineOrDefault(l, false));
            builder.Append('\n').Append(lastTrim[..endInLast]);
            segment = builder.ToString();
        }

        // the range must itself be one balanced call expression — an
        // `obj.attr` or `d['k']` right-hand value is not a call and keeps
        // its carets
        return IsBalancedCallSegment(segment, requireNameCall);
    }

    private static bool IsBalancedCallSegment(string segment, bool requireNameCall)
    {
        var i = 0;
        if (requireNameCall)
        {
            while (i < segment.Length && char.IsWhiteSpace(segment[i]))
                i++;
            var nameStart = i;
            while (i < segment.Length && (char.IsAsciiLetterOrDigit(segment[i]) || segment[i] is '_'))
                i++;
            if (i == nameStart)
                return false;
            while (i < segment.Length && char.IsWhiteSpace(segment[i]))
                i++;
            if (i >= segment.Length || segment[i] is not '(')
                return false;
        }

        var depth = 0;
        var lastSignificant = -1;
        while (i < segment.Length)
        {
            var c = segment[i];
            if (c is '\'' or '"')
            {
                i = SkipQuotedString(segment, i);
                continue;
            }
            if (c is '#')
            {
                while (i < segment.Length && segment[i] is not '\n')
                    i++;
                continue;
            }
            if (c is '(' or '[' or '{')
            {
                depth++;
            }
            else if (c is ')' or ']' or '}')
            {
                depth--;
                if (depth < 0)
                    return false;
            }
            if (!char.IsWhiteSpace(c))
                lastSignificant = i;
            i++;
        }

        return depth is 0 && lastSignificant >= 0 && segment[lastSignificant] is ')';
    }

    private static int SkipQuotedString(string segment, int start)
    {
        var quote = segment[start];
        var triple = start + 2 < segment.Length && segment[start + 1] == quote && segment[start + 2] == quote;
        var i = start + (triple ? 3 : 1);
        while (i < segment.Length)
        {
            if (segment[i] is '\\')
            {
                i += 2;
                continue;
            }
            if (segment[i] == quote)
            {
                if (!triple)
                    return i + 1;
                if (i + 2 < segment.Length && segment[i + 1] == quote && segment[i + 2] == quote)
                    return i + 3;
            }
            i++;
        }
        return i;
    }

    private static bool IsPythonIdentifier(string text)
    {
        if (text.Length is 0 || (text[0] is not '_' && !char.IsAsciiLetter(text[0])))
            return false;
        for (var i = 1; i < text.Length; i++)
        {
            if (text[i] is not '_' && !char.IsAsciiLetterOrDigit(text[i]))
                return false;
        }
        return true;
    }
}
