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
        builder
            .AppendFormat("  File \"{0}\", line {1}, in {2}", info.Source?.Name ?? "<unknown>", info.Start.Line, callerName)
            .AppendLine();

        var origLine = info.FirstLine.TrimEnd();
        var line = origLine.TrimStart();

        if (provider.OnlyStartInfo)
        {
            if (line.Length > 0)
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

        if (info.CrucialStart == default || info.CrucialStart.Line != info.Start.Line)
        {
            if (end - start < line.Length)
                builder
                    .Append(' ', 4 + start)
                    .Append('^', end - start)
                    .AppendLine();
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

            builder
                .Append(' ', 4 + start)
                .Append('~', crucialStart - start)
                .Append('^', crucialEnd - crucialStart)
                .Append('~', end - crucialEnd)
                .AppendLine();
        }
    }
}
