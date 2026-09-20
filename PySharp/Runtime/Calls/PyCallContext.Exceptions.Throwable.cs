using PySharp.Compilation.CodeAnalysis;
using PySharp.Modules.Builtins;

namespace PySharp.Runtime.Calls;

partial class PyCallContext
{
    private PyRuntimeException ThrowableException(PyTypeObject<PyExceptionObject> exceptionType, string? format, ReadOnlySpan<object?> args)
    {
        return ThrowableException(exceptionType, PyStrObject.FromString(PySR.Format(format ?? string.Empty, args)));
    }

    private PyRuntimeException ThrowableException(PyTypeObject<PyExceptionObject> exceptionType, PyObject? arg)
    {
        var result = PyExceptionObject.Create(this, exceptionType, arg is null ? [] : [arg]);
        return new PyRuntimeException(this, result.Value ?? result.Exception!);
    }

    // CPython chains the handled exception into every newly set one
    // (_PyErr_SetObject reads the thread-wide exc_info slot). Error
    // creation sites funnel through the context-ful PyRuntimeException
    // constructors, which call this: the source-generated exception
    // factories (via ThrowableException), PyUnwrap unwrap sites and
    // PyCore.Raise's interpreter-error paths. The context-LESS
    // constructor never chains — it is reserved for propagation rethrows
    // (PyCore.Raise, dead-generator throw) and machinery-settled
    // exceptions (generator injection, except* settlement), mirroring
    // CPython's PyErr_Restore-style re-raises that bypass _PyErr_SetObject.
    // Unlike _PyErr_SetObject this never overwrites: deferred errors are
    // re-wrapped at each call boundary, so the not-already-chained guard
    // keeps those hops idempotent.
    internal void ChainHandledContext(PyExceptionObject exc)
    {
        var handled = HandledException;
        if (handled is null || exc.ContextSettled || ReferenceEquals(handled, exc) || exc.Context is not null)
            return;

        PyCore.BreakContextLinkTo(handled, exc);
        exc.Context = handled;
    }

    internal PyRuntimeException SyntaxError(ICodeMetaInfoProvider compiler, string format, params ReadOnlySpan<object?> args)
    {
        var exc = PySyntaxErrorObjectType.Shared.Create(PyStrObject.FromString(PySR.Format(format, args)));
        AttachCompilerLocation(exc, compiler.MetaInfo);
        return new PyRuntimeException(this, exc, compiler);
    }

    internal PyRuntimeException IndentationError(ICodeMetaInfoProvider compiler, string format, params ReadOnlySpan<object?> args)
    {
        var exc = PyIndentationErrorObjectType.Shared.Create(PyStrObject.FromString(PySR.Format(format, args)));
        AttachCompilerLocation(exc, compiler.MetaInfo);
        return new PyRuntimeException(this, exc, compiler);
    }

    internal PyRuntimeException TabError(ICodeMetaInfoProvider compiler, string format, params ReadOnlySpan<object?> args)
    {
        var exc = PyTabErrorObjectType.Shared.Create(PyStrObject.FromString(PySR.Format(format, args)));
        AttachCompilerLocation(exc, compiler.MetaInfo);
        return new PyRuntimeException(this, exc, compiler);
    }

    // CPython raises parse-time errors with the full location info tuple
    // (_PyPegen_raise_error_known_location), so the display layer renders
    // the File/caret block from the exception's own attributes
    // (traceback.py _format_syntax_error) instead of a traceback frame
    private static void AttachCompilerLocation(PyExceptionObject exc, CodeMetaInfo? metaInfo)
    {
        if (metaInfo is null)
            return;

        var source = metaInfo.Source;
        PyObject filename = PyNoneObject.None;
        PyObject text = PyNoneObject.None;
        if (source is not null)
        {
            filename = PyStrObject.FromString(source.Name);
            exc.PyAttributes["filename"] = filename;
            // CPython's text keeps the physical line including its line break
            if (source.Code.TryGetLine(metaInfo.Start.Line, true, out var line))
                exc.PyAttributes["text"] = text = PyStrObject.FromString(line.ToString());
        }
        var lineno = PyIntObject.FromInteger(metaInfo.Start.Line);
        var offset = PyIntObject.FromInteger(metaInfo.Start.Offset + 1);
        var endLineno = PyIntObject.FromInteger(metaInfo.End.Line);
        var endOffset = PyIntObject.FromInteger(metaInfo.End.Offset + 1);
        exc.PyAttributes["lineno"] = lineno;
        exc.PyAttributes["offset"] = offset;
        exc.PyAttributes["end_lineno"] = endLineno;
        exc.PyAttributes["end_offset"] = endOffset;

        // CPython's parse errors carry (msg, info-tuple) args
        exc.Args =
        [
            exc.Args is [var msg, ..] ? msg : PyNoneObject.None,
            PyTupleObject.CreateTuple(filename, lineno, offset, text, endLineno, endOffset),
        ];
    }

    internal PyRuntimeException PySharpException(string? format, params ReadOnlySpan<object?> args)
    {
        return ThrowableException(Modules.CSharp.PySharpException.Shared, format, args);
    }
}