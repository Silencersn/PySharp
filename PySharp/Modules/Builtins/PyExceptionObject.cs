using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Utility;
using System.Collections.Frozen;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;

namespace PySharp.Modules.Builtins;

public sealed class PyExceptionObject : PyObjectManagedDict
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

    internal static PyResult<PyExceptionObject> Create(PyCallContext context, PyTypeObject exceptionType, IEnumerable<PyObject> args, ExceptionGroupInfo? asGroup = null)
    {
        var exc = new PyExceptionObject(exceptionType, args, asGroup);
        var initResult = PyTypeObjectType.CallInit(context, exceptionType, exc,
            exc.Args, FrozenDictionary<string, PyObject>.Empty);
        if (initResult.IsError)
            return initResult.ExceptionResult;
        return exc;
    }

    internal static PyExceptionObject UnsafeCreate(PyTypeObject exceptionType, IEnumerable<PyObject> args, ExceptionGroupInfo? asGroup = null)
    {
        var exc = new PyExceptionObject(exceptionType, args, asGroup);
        var initResult = PyTypeObjectType.CallInit(PyCallContext.NonContextDependency, exceptionType, exc,
            exc.Args, FrozenDictionary<string, PyObject>.Empty);
        Debug.Assert(initResult.IsSuccessful);
        return exc;
    }

    public bool SuppressContext { get; internal set; }
    public PyExceptionObject? Cause { get; internal set; }
    public PyExceptionObject? Context { get; internal set; }
    // Set by machinery whose exceptions are settled with PyErr_Restore-style
    // semantics (generator throw-in/close injection, except* settlement):
    // later propagation hops through deferred error results must not chain
    // again
    internal bool ContextSettled { get; set; }
    internal string? CauseReason { get; set; }
    // Rebindable per CPython BaseException_init: when __new__ is overridden
    // but __init__ is not, the inherited __init__ re-binds e.args to the
    // original instantiation arguments.
    public IReadOnlyList<PyObject> Args { get; internal set; }
    public PyTracebackObject? Traceback { get; internal set; }
    // CPython prints the "Exception in thread ..." header from the threading
    // layer, not from the traceback object, so the header stays on the
    // exception and survives __traceback__ reassignment.
    internal string? TracebackThreadInfo { get; set; }
    internal PyObject? ExtraValue { get; set; }

    [MemberNotNullWhen(true, nameof(AsGroup))]
    internal bool IsGroup => AsGroup is not null;
    internal ExceptionGroupInfo? AsGroup { get; }

    // CPython accumulates a traceback as the exception travels: every frame the
    // exception leaves prepends its own entry (PyTraceBack_Here, run from the
    // interpreter's error label), and an exception's traceback is never replaced
    // by a fresh snapshot of the active stack. A new head node is built per
    // entry, so the reference changes exactly when the traceback grew —
    // PrepReraiseStar reads that to tell an explicit re-raise from a bare one.
    internal PyExceptionObject RecordFrame(PyCallContext context)
    {
        var node = PyTraceback.CaptureFrameNode(context);
        if (node is null)
            return this;

        node._next = Traceback;
        Traceback = node;
        TracebackThreadInfo ??= PyTraceback.CaptureThreadInfo(context);

        return this;
    }

    internal string ToMessage(PyCallContext context)
    {
        var builder = new IndentedStringBuilder();
        // CPython traceback._seen: a __context__/__cause__ cycle built via
        // the user-facing setters is legal and must not recurse forever
        var seen = new HashSet<PyExceptionObject>(ReferenceEqualityComparer.Instance);
        if (IsGroup)
        {
            using (builder.Indent())
                PrintMessage(builder, context, seen);
        }
        else
        {
            PrintMessage(builder, context, seen);
        }
        return builder.ToString();
    }

    // CPython write_unraisable_exc prints the traceback and a single
    // "module.qualname: str(value)" line; unlike the unhandled-exception path
    // it never renders the __context__/__cause__ chain.
    internal string ToUnraisableMessage(PyCallContext context)
    {
        var builder = new IndentedStringBuilder();
        PrintTrailer(builder, context, new HashSet<PyExceptionObject>(ReferenceEqualityComparer.Instance) { this });
        return builder.ToString();
    }

    internal void PrintMessage(IndentedStringBuilder builder, PyCallContext context, HashSet<PyExceptionObject> seen)
    {
        if (!seen.Add(this))
        {
            // this exception's chain segment already rendered; print the
            // header alone, exactly like the CPython seen-set break
            PrintTrailer(builder, context, seen);
            return;
        }

        if (Cause is not null && !seen.Contains(Cause))
        {
            Cause.PrintMessage(builder, context, seen);
            builder
                .AppendLine()
                .AppendLine(CauseReason)
                .AppendLine();
        }
        else if (!SuppressContext && Context is not null && !seen.Contains(Context))
        {
            Context.PrintMessage(builder, context, seen);
            builder
                .AppendLine()
                .AppendLine("During handling of the above exception, another exception occurred:")
                .AppendLine();
        }

        PrintTrailer(builder, context, seen);
    }

    private void PrintTrailer(IndentedStringBuilder builder, PyCallContext context, HashSet<PyExceptionObject> seen)
    {
        if (TracebackThreadInfo is not null)
        {
            builder
                .AppendLine(TracebackThreadInfo);
        }

        if (IsGroup)
        {
            PrintExceptionGroupMessage(builder, context, seen);
            return;
        }

        // CPython gates the header on the stack summary being non-empty
        // (traceback.py format: "if exc.stack:"), so a parse-time
        // SyntaxError with no frames prints no header at all
        if (Traceback is not null)
        {
            builder.AppendLine("Traceback (most recent call last):");
            PyTraceback.Print(builder, Traceback);
        }

        if (PySyntaxErrorObjectType.Shared.IsInstance(this))
            PrintSyntaxErrorMessage(builder, context);
        else
            PrintSimpleMessage(builder, context);
        builder.AppendLine();
    }

    private void PrintSimpleMessage(IndentedStringBuilder builder, PyCallContext context)
    {
        // traceback.TracebackException.format_exception_only names the type as
        // module.qualname, dropping "builtins" and "__main__"
        builder.Append(PyType.FullyQualifiedName);
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

    // CPython traceback.TracebackException._format_syntax_error: for the
    // SyntaxError family the location block comes from the exception's own
    // attributes rather than any traceback frame, and the final line uses
    // the msg attribute so str(exc)'s "msg (filename, line N)" suffix is
    // never duplicated here
    private void PrintSyntaxErrorMessage(IndentedStringBuilder builder, PyCallContext context)
    {
        var attrs = PyAttributes;
        attrs.TryGetValue("filename", out var filenameValue);
        attrs.TryGetValue("lineno", out var linenoValue);
        attrs.TryGetValue("offset", out var offsetValue);
        attrs.TryGetValue("text", out var textValue);
        attrs.TryGetValue("end_lineno", out var endLinenoValue);
        attrs.TryGetValue("end_offset", out var endOffsetValue);

        string filenameSuffix = string.Empty;
        if (linenoValue is PyIntObject linenoInt && linenoValue is not PyBoolObject)
        {
            var name = filenameValue is PyStrObject nameStr ? nameStr.Value : "<string>";
            builder.AppendLine($"  File \"{name}\", line {linenoInt.Value}");
        }
        else if (filenameValue is PyStrObject filenameStr)
        {
            filenameSuffix = $" ({filenameStr.Value})";
        }

        if (textValue is PyStrObject text)
        {
            string rtext = text.Value.TrimEnd('\n');
            string ltext = rtext.TrimStart(' ', '\n', '\f');
            int spaces = rtext.Length - ltext.Length;

            if (offsetValue is PyIntObject offsetInt)
            {
                // info-tuple values come from user code too, so the math
                // stays in arbitrary precision exactly like CPython
                BigInteger offset = offsetInt.Value;
                // CPython's fallback order: an invalid/zero/missing end
                // offset degenerates to the start offset on the same line,
                // out-of-line ranges caret to the line end, and a non-range
                // becomes a single caret
                BigInteger endOffset;
                if (linenoValue is PyIntObject sameLine && endLinenoValue is PyIntObject endLinenoInt && endLinenoInt.Value == sameLine.Value)
                {
                    endOffset = endOffsetValue is PyIntObject endOffsetInt && endOffsetInt.Value != 0
                        ? endOffsetInt.Value
                        : offset;
                }
                else
                {
                    endOffset = rtext.Length + 1;
                }

                if (text.Value.Length is not 0 && offset > text.Value.Length)
                    offset = rtext.Length + 1;
                if (text.Value.Length is not 0 && endOffset > text.Value.Length)
                    endOffset = rtext.Length + 1;
                if (offset >= endOffset || endOffset < 0)
                    endOffset = offset + 1;

                BigInteger colno = offset - 1 - spaces;
                BigInteger endColno = endOffset - 1 - spaces;
                if (colno >= 0)
                {
                    builder.AppendLine($"    {ltext}");
                    // whitespace characters of the caret prefix are kept
                    // for tab alignment (CPython caretspace)
                    int prefixLength = (int)BigInteger.Min(colno, ltext.Length);
                    string caretspace = string.Create(prefixLength, ltext, static (span, source) =>
                    {
                        for (int i = 0; i < span.Length; i++)
                            span[i] = char.IsWhiteSpace(source[i]) ? source[i] : ' ';
                    });
                    int caretCount = (int)BigInteger.Min(BigInteger.Max(0, endColno - colno), rtext.Length + 1);
                    builder.AppendLine($"    {caretspace}{new string('^', caretCount)}");
                }
                else
                {
                    builder.AppendLine($"    {ltext}");
                }
            }
            else
            {
                builder.AppendLine($"    {ltext}");
            }
        }

        // the syntax-error line follows the same naming rule as
        // PrintSimpleMessage (format_exception_only)
        builder.Append(PyType.FullyQualifiedName).Append(": ").Append(ResolveSyntaxErrorMessage(context));
        if (filenameSuffix.Length is not 0)
            builder.Append(filenameSuffix);
    }

    private string ResolveSyntaxErrorMessage(PyCallContext context)
    {
        var msg = PyAttributes.TryGetValue("msg", out var msgValue) ? msgValue : PyNoneObject.None;
        // CPython "self.msg or '<no detail available>'"
        if (msg is PyNoneObject || (msg is PyStrObject str && str.Value.Length is 0))
            return "<no detail available>";

        var result = PySpecialMethods.Str(context, msg);
        return result.IsSuccessful ? result.Value.Value : "<exception str() failed>";
    }

    private void PrintExceptionGroupMessage(IndentedStringBuilder builder, PyCallContext context, HashSet<PyExceptionObject> seen)
    {
        Debug.Assert(AsGroup is not null);

        if (Traceback is not null)
        {
            builder.AppendLine("+ Exception Group Traceback (most recent call last):");
            using (builder.Indent("| "))
                PyTraceback.Print(builder, Traceback);
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
                    subExc.PrintMessage(builder, context, seen);
                }
                else
                {
                    using (builder.Indent("| "))
                        subExc.PrintMessage(builder, context, seen);
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
