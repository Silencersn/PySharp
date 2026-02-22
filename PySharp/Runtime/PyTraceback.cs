using PySharp.Compilation.CodeAnalysis;
using PySharp.Modules.Builtins;
using PySharp.Runtime.Calls;
using PySharp.Utility;
using System.Diagnostics;

namespace PySharp.Runtime;

public sealed class TrackbackInfo
{
    public IReadOnlyList<(CodeMetaInfo? Info, string CallerName)> Frames { get; }
    public string? ThreadInfo { get; }

    public TrackbackInfo(IReadOnlyList<(CodeMetaInfo? Info, string CallerName)> frames, string? threadInfo = null)
    {
        Frames = frames;
        ThreadInfo = threadInfo;
    }

    internal void Print(IndentedStringBuilder builder)
    {
        foreach (var (info, callerName) in Frames)
        {
            if (info is null)
                continue;

            Print(builder, info, callerName);
        }
    }

    internal static void Print(IndentedStringBuilder builder, CodeMetaInfo info, string callerName)
    {
        using (builder.Indent())
        {
            builder
                .AppendFormat("File \"{0}\", line {1}, in {2}", info.Source?.Name ?? "<unknown>", info.Start.Line, callerName)
                .AppendLine();

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
                Debug.Assert(end > start);

                if (!info.HasCrucialRange || info.CrucialStart.Line != info.Start.Line)
                {
                    if (start >= 0 && end - start < line.Length)
                        // if the line is full of '^', do not draw
                        builder
                            .Append(' ', start)
                            .Append('^', end - start)
                            .AppendLine();
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
        var frame = context.CurrentFrame;
        var provider = frame.MetaInfoProvider;
        var info = provider?.MetaInfo;
        return new PyTracebackObject(frame, info, provider);
    }

    public static TrackbackInfo GetTracebackInfo(PyCallContext context)
    {
        Stack<(CodeMetaInfo? Info, string CallerName)> stack = [];
        string? threadInfo = null;
        var frame = context.CurrentFrame;
        while (frame is not null)
        {
            if (frame.FrameType is FrameType.ThreadRoot)
                threadInfo = $"Exception in thread Thread-{Environment.CurrentManagedThreadId} ({frame.CallerName}):";

            var provider = frame.MetaInfoProvider;

            if (provider is not null)
                stack.Push((provider.MetaInfo, frame.CallerName));

            frame = frame.Back;
        }
        return new TrackbackInfo([.. stack], threadInfo);
    }
}
