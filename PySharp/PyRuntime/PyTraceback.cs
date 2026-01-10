using PySharp.CodeAnalysis;
using PySharp.PyModules.Builtins;
using PySharp.PyRuntime.Calls;
using System.Diagnostics;
using System.Text;

namespace PySharp.PyRuntime;

internal static class PyTraceback
{
    public static PyTracebackObject CaptureCurrentFrame(PyCallContext context)
    {
        var frame = context.CurrentFrame;
        var provider = frame.MetaInfoProvider;
        var info = provider?.MetaInfo;
        return new PyTracebackObject(frame, info, provider);
    }

    public static string PrintTraceback(PyCallContext context)
    {
        Stack<(ICodeMetaInfoProvider Provider, string CallerName)> stack = [];
        var frame = context.CurrentFrame;
        while (frame is not null)
        {
            var provider = frame.MetaInfoProvider;

            if (provider is not null)
                stack.Push((provider, frame.CallerName));

            frame = frame.Back;
        }
        return PrintTraceback(stack);
    }

    private static string PrintTraceback(IEnumerable<(ICodeMetaInfoProvider Provider, string CallerName)> frames)
    {
        var builder = new StringBuilder();
        foreach (var (provider, callerName) in frames)
        {
            PrintTraceback(builder, provider, callerName);
        }
        return builder.ToString();
    }

    private static void PrintTraceback(StringBuilder builder, ICodeMetaInfoProvider provider, string callerName)
    {
        if (provider.MetaInfo is null)
            return;

        var info = provider.MetaInfo;
        builder
            .AppendFormat("  File \"{0}\", line {1}, in {2}", info.Source?.Name ?? "<unknown>", info.Start.Line, callerName)
            .AppendLine();

        var origLine = info.FirstLine.TrimEnd();
        var line = origLine.TrimStart();

        if (line.Length is 0)
            return;

        if (!info.HasRange)
        {
            builder
                .Append(' ', 4)
                .Append(line)
                .AppendLine();
            return;
        }

        var offset = line.Length - origLine.Length;
        var start = info.Start.Offset + offset;
        var end = info.End.Line == info.Start.Line
            ? info.End.Offset + offset
            : line.Length;
        Debug.Assert(end > start);

        builder.AppendLine($"    {line}");

        if (!info.HasCrucialRange || info.CrucialStart.Line != info.Start.Line)
        {
            if (end - start < line.Length)
                // if the line is full of '^', do not draw
                builder
                    .Append(' ', 4 + start)
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
            .Append(' ', 4 + start)
            .Append('~', crucialStart - start)
            .Append('^', crucialEnd - crucialStart)
            .Append('~', end - crucialEnd)
            .AppendLine();
    }
}
