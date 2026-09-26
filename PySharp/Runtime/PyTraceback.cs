using PySharp.Compilation.CodeAnalysis;
using PySharp.Modules.Builtins;
using PySharp.Runtime.Calls;
using PySharp.Utility;
using System.Diagnostics;

namespace PySharp.Runtime;

public sealed class TracebackInfo
{
    public IReadOnlyList<(CodeMetaInfo? Info, string? CallerName)> Frames { get; }
    public string? ThreadInfo { get; }

    public TracebackInfo(IReadOnlyList<(CodeMetaInfo? Info, string? CallerName)> frames, string? threadInfo = null)
    {
        Frames = frames;
        ThreadInfo = threadInfo;
    }

    internal void Print(IndentedStringBuilder builder)
    {
        CodeMetaInfo? preInfo = null;
        int repeatCount = 0;
        foreach (var (info, callerName) in Frames)
        {
            if (info is null)
                continue;

            if (info == preInfo)
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
                Print(builder, info, callerName);
            preInfo = info;
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
}

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

    // One entry for the frame an exception is leaving. An inlined comprehension
    // (PEP 709) shares its enclosing frame's code object, so its entry carries
    // the enclosing frame's name and the comprehension line — the same single
    // entry CPython records, since it has no separate frame for a comprehension.
    public static (CodeMetaInfo? Info, string? CallerName)? CaptureFrameEntry(PyCallContext context)
    {
        ref var frame = ref context.CurrentInternalFrame;
        if (frame.CodeObject is null)
            return null;

        return (frame.CodeObject.Bytecode.LineTable.Read(frame.InstructionIndex), frame.CallerName);
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
}
