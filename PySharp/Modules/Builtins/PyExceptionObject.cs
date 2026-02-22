using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Utility;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace PySharp.Modules.Builtins;

public sealed class PyExceptionObject : PyObject
{
    public override PyTypeObject DefaultPyType => PyBaseExceptionObjectType.Shared;

    internal PyExceptionObject(PyTypeObject exceptionType, IEnumerable<PyObject> args, ExceptionGroupInfo? asGroup = null)
    {
        Debug.Assert(exceptionType.IsSubclassOf(PyBaseExceptionObjectType.Shared));
        Debug.Assert(asGroup is null || exceptionType.IsSubclassOf(PyBaseExceptionGroupObjectType.Shared));
        
        _pyType = exceptionType;
        Args = [.. args];
        AsGroup = asGroup;
    }

    public bool SuppressContext { get; internal set; }
    public PyExceptionObject? Cause { get; internal set; }
    public PyExceptionObject? Context { get; internal set; }
    internal string? CauseReason { get; set; }
    public IReadOnlyList<PyObject> Args { get; }
    public TrackbackInfo? Traceback { get; internal set; }
    internal string? ThreadTracebackInfo { get; set; }

    [MemberNotNullWhen(true, nameof(AsGroup))]
    internal bool IsGroup => AsGroup is not null;
    internal ExceptionGroupInfo? AsGroup { get; }

    internal PyExceptionObject WithTraceback(PyCallContext context, bool overwriteExisting = false)
    {
        if (Traceback is not null && !overwriteExisting)
            return this;

        Traceback = PyTraceback.GetTracebackInfo(context);
        var frame = context.CurrentFrame;
        while (frame is not null)
        {
            var back = frame.Back;
            if (back is not null && back.FrameType is FrameType.ThreadRoot)
            {
                Debug.Assert(back.IsRoot);
                ThreadTracebackInfo = $"Exception in thread Thread-{Environment.CurrentManagedThreadId} ({frame.CallerName}):";
                break;
            }
            frame = back;
        }

        return this;
    }

    internal string ToMessage(PyCallContext context)
    {
        var builder = new IndentedStringBuilder();
        if (IsGroup)
        {
            using (builder.Indent())
                PrintMessage(builder, context);
        }
        else
        {
            PrintMessage(builder, context);
        }
        return builder.ToString();
    }

    internal void PrintMessage(IndentedStringBuilder builder, PyCallContext context)
    {
        if (Cause is not null)
        {
            Cause.PrintMessage(builder, context);
            builder
                .AppendLine()
                .AppendLine(CauseReason)
                .AppendLine();
        }
        else if (!SuppressContext && Context is not null)
        {
            Context.PrintMessage(builder, context);
            builder
                .AppendLine()
                .AppendLine("During handling of the above exception, another exception occurred:")
                .AppendLine();
        }

        if (ThreadTracebackInfo is not null)
        {
            builder
                .AppendLine(ThreadTracebackInfo);
        }

        if (IsGroup)
        {
            PrintExceptionGroupMessage(builder, context);
            return;
        }

        if (Traceback is not null)
        {
            builder.AppendLine("Traceback (most recent call last):");
            Traceback.Print(builder);
        }

        PrintSimpleMessage(builder, context);
        builder.AppendLine();
    }

    private void PrintSimpleMessage(IndentedStringBuilder builder, PyCallContext context)
    {
        builder.Append(PyType.FullName);
        var result = PySpecialMethods.Str(context, this);
        if (result.IsSuccessful)
        {
            if (result.Value.Value != string.Empty)
                builder.Append(": ").Append(result.Value.Value);
        }
        else
        {
            builder.Append(": ").Append("<exception str() failed>");
        }
    }

    private void PrintExceptionGroupMessage(IndentedStringBuilder builder, PyCallContext context)
    {
        Debug.Assert(AsGroup is not null);

        if (Traceback is not null)
        {
            builder.AppendLine("+ Exception Group Traceback (most recent call last):");
            using (builder.Indent("| "))
                Traceback.Print(builder);
        }

        builder.Append("| ");
        PrintSimpleMessage(builder, context);
        builder.AppendLine();
        builder.Append("+-");

        using (builder.Indent())
        {
            int counter = 0;
            bool isLastGroup = false;
            foreach (var subExc in AsGroup.Exceptions)
            {
                builder.Append('+');
                builder.AppendFormat("---------------- {0} ----------------", ++counter);
                builder.AppendLine();

                if (isLastGroup = subExc.IsGroup)
                {
                    subExc.PrintMessage(builder, context);
                }
                else
                {
                    using (builder.Indent("| "))
                        subExc.PrintMessage(builder, context);
                }
            }
            if (!isLastGroup)
                builder.AppendLine("+------------------------------------");
        }

    }
}

internal sealed class ExceptionGroupInfo
{
    internal ExceptionGroupInfo(string message, IReadOnlyList<PyExceptionObject> exceptions)
    {
        Debug.Assert(exceptions.Count is not 0);

        Message = message;
        Exceptions = exceptions;
    }

    public string Message { get; }
    public IReadOnlyList<PyExceptionObject> Exceptions { get; }
}