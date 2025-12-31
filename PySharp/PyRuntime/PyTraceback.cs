using PySharp.PyRuntime.Calls;
using PySharp.PyRuntime.Metadata;
using System.Diagnostics;
using System.Text;

namespace PySharp.PyRuntime;

internal static class PyTraceback
{
    public static string PrintTraceback(PyCallContext context)
    {
        Stack<(IMetaInfoProvider Provider, string CallerName)> stack = [];
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

    private static string PrintTraceback(IEnumerable<(IMetaInfoProvider Provider, string CallerName)> frames)
    {
        var builder = new StringBuilder();
        foreach (var (provider, callerName) in frames)
        {
            PrintTraceback(builder, provider, callerName);
        }
        return builder.ToString();
    }

    private static void PrintTraceback(StringBuilder builder, IMetaInfoProvider provider, string callerName)
    {
        if (provider.MetaInfo is null)
            return;

        var info = provider.MetaInfo;
        builder.AppendLine($"  File \"{info.Source?.Name ?? "<unknown>"}\", line {info.Start.Line}, in {callerName}");

        if (provider.OnlyStartInfo)
        {
            if (info.FirstLine.Length > 0) // TODO: trimmed
                builder.AppendLine($"    {info.FirstLine.Trim().TrimEnd(['\r', '\n'])}");
            return;
        }

        // TODO: make FirstLine single line
        // actually, info.FirstLine may be multiline
        var lines = info.FirstLine.EnumerateLines();

        // first MoveNext must be true
        lines.MoveNext();

        var line = lines.Current;
        var preLength = line.Length;
        line = line.TrimStart();
        var offset = line.Length - preLength;
        var start = info.Start.Offset + offset;
        var end = info.End.Line == info.Start.Line
            ? info.End.Offset + offset
            : line.Length;
        Debug.Assert(end > start);

        builder.AppendLine($"    {line}");

        if (info.CrucialStart == default || info.CrucialStart.Line != info.Start.Line)
        {
            if (end - start < line.Length)
                builder.AppendLine($"    {new string(' ', start)}{new string('^', end - start)}");
        }
        else
        {
            Debug.Assert(info.CrucialStart.Line == info.Start.Line);

            var crucialStart = info.CrucialStart.Offset + offset;
            var crucialEnd = info.CrucialEnd.Line == info.Start.Line
                ? info.CrucialEnd.Offset + offset
                : line.Length;
            Debug.Assert(crucialEnd > crucialStart);
            Debug.Assert(end >= crucialEnd);
            Debug.Assert(crucialStart >= start);

            builder.AppendLine($"    {new string(' ', start)}{new string('~', crucialStart - start)}{new string('^', crucialEnd - crucialStart)}{new string('~', end - crucialEnd)}");
        }
    }
}
