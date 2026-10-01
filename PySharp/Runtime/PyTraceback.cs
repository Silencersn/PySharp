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

            using (builder.Indent())
            {
                var startLine = info.Start.Line;
                var endLine = info.HasRange ? Math.Max(info.End.Line, startLine) : startLine;
                var lineCount = endLine - startLine + 1;

                // the block of lines the instruction spans, dedented by the
                // leading whitespace every line shares (textwrap.dedent on the
                // block); a single-line instruction keeps its old trim behavior
                var lines = new string[lineCount];
                var source = info.Source;
                if (source is null)
                    return;
                var code = source.Code;
                for (var i = 0; i < lineCount; i++)
                    lines[i] = code.GetLineOrDefault(startLine + i, false).TrimEnd().ToString();
                var dedent = CommonLeadingWhitespace(lines);
                for (var i = 0; i < lineCount; i++)
                    lines[i] = lines[i].Length >= dedent ? lines[i][dedent..] : lines[i].TrimStart();

                var hasContent = false;
                foreach (var line in lines)
                {
                    if (line.Length is not 0)
                    {
                        hasContent = true;
                        break;
                    }
                }
                if (!hasContent)
                    return;

                var startInFirst = Math.Max(0, info.Start.Offset - dedent);
                var endInLast = Math.Max(0, info.End.Offset - dedent);

                // which rows carry caret rows (significant_lines): the first and
                // last line of the block, plus one line around each anchor end
                var significant = new SortedSet<int> { 0, lineCount - 1 };
                var hasCrucial = info.HasCrucialRange;
                if (hasCrucial)
                {
                    AddClippedSignificantLine(significant, info.CrucialStart.Line - startLine, lineCount);
                    AddClippedSignificantLine(significant, info.CrucialEnd.Line - startLine, lineCount);
                }

                var showCarets = false;
                var singleEmptyRange = lineCount is 1 && startInFirst == endInLast;
                if (info.HasRange && !singleEmptyRange)
                {
                    if (StatementShapeSuppressesCarets(lines, startInFirst, endInLast))
                    {
                        // the statement shape rule cuts the caret row before the
                        // anchor pass — see the method's comment
                    }
                    else if (hasCrucial)
                    {
                        showCarets = true;
                    }
                    else
                    {
                        // without anchors only an uncovered part of the block
                        // warrants a caret row; '^' across the whole block would
                        // repeat nothing new
                        showCarets = lines[0][..Math.Min(startInFirst, lines[0].Length)].Trim().Length > 0
                            || lines[^1][Math.Min(endInLast, lines[^1].Length)..].Trim().Length > 0;
                    }
                }

                var crucialStartLine = info.CrucialStart.Line - startLine;
                var crucialEndLine = info.CrucialEnd.Line - startLine;
                var crucialStartCol = Math.Max(0, info.CrucialStart.Offset - dedent);
                var crucialEndCol = Math.Max(0, info.CrucialEnd.Offset - dedent);

                void OutputLine(int index)
                {
                    builder.AppendLine(lines[index]);
                    if (!showCarets)
                        return;

                    var line = lines[index];
                    var leading = line.Length - line.TrimStart().Length;
                    var numCarets = index == lineCount - 1 ? Math.Min(endInLast, line.Length) : line.Length;
                    var carets = new StringBuilder(numCarets);
                    for (var col = 0; col < numCarets; col++)
                    {
                        if (col < leading || (index is 0 && col < startInFirst))
                            carets.Append(' ');
                        else if (hasCrucial
                                 && (index > crucialStartLine || (index == crucialStartLine && col >= crucialStartCol))
                                 && (index < crucialEndLine || (index == crucialEndLine && col < crucialEndCol)))
                            // within the anchors: the operand the error is about
                            carets.Append('^');
                        else
                            // around the anchors: the already-executed part
                            carets.Append('~');
                    }
                    builder.AppendLine(carets.ToString());
                }

                var sigList = significant.ToArray();
                for (var i = 0; i < sigList.Length; i++)
                {
                    if (i > 0)
                    {
                        var diff = sigList[i] - sigList[i - 1];
                        if (diff is 2)
                            OutputLine(sigList[i] - 1);
                        else if (diff > 2)
                            builder.AppendLine($"...<{diff - 1} lines>...");
                    }
                    OutputLine(sigList[i]);
                }
            }
        }
    }

    private static void AddClippedSignificantLine(SortedSet<int> significant, int center, int lineCount)
    {
        for (var n = center - 1; n <= center + 1; n++)
        {
            if (n >= 0 && n < lineCount)
                significant.Add(n);
        }
    }

    private static int CommonLeadingWhitespace(string[] lines)
    {
        var common = int.MaxValue;
        foreach (var line in lines)
        {
            if (line.Trim().Length is 0)
                continue;
            var n = 0;
            while (n < line.Length && line[n] is ' ' or '\t')
                n++;
            common = Math.Min(common, n);
        }
        return common is int.MaxValue ? 0 : common;
    }

    // The first caret rule reads the statement shape before the anchor pass:
    // `return f(...)` and a plain-name `x = f(...)` whose call spans the whole
    // instruction range would only repeat what the source line already shows,
    // so they print no caret row at all — even though the call instruction
    // does carry anchors. Everything else keeps them: tuple/attribute/
    // subscript assignment targets, compound statement heads (`if c: x =`),
    // trailing operands (`x = f() + 1`), and bare expression statements.
    private static bool StatementShapeSuppressesCarets(string[] lines, int startInFirst, int endInLast)
    {
        var first = lines[0];
        var prefix = first[..Math.Min(startInFirst, first.Length)].Trim();
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
        if (lines.Length is 1)
        {
            if (endInLast > first.Length)
                return false;
            // content after the range (`x = f() + 1`) means the right-hand
            // value is a larger expression than the call and keeps its carets
            if (first.AsSpan(endInLast).Trim().Length is not 0)
                return false;
            segment = first[startInFirst..endInLast];
        }
        else
        {
            var last = lines[^1];
            if (endInLast > last.Length || last.AsSpan(endInLast).Trim().Length is not 0)
                return false;

            var builder = new StringBuilder(first[startInFirst..]);
            for (var i = 1; i < lines.Length - 1; i++)
                builder.Append('\n').Append(lines[i]);
            builder.Append('\n').Append(last[..endInLast]);
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
