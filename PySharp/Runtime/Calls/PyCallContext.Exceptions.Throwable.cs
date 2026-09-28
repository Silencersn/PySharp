using PySharp.Compilation.CodeAnalysis;
using PySharp.Modules.Builtins;
using System.Diagnostics;

namespace PySharp.Runtime.Calls;

// CPython does not fill parse-error locations with a span uniformly: values
// without a precise endpoint travel as sentinels (pegen's RAISE macro adds
// one to a -1 end, tokenizer-side missing ends stay -1), which is what lets
// editors tell "error at this column" apart from "position unknown".
internal enum SyntaxErrorSpan : byte
{
    Exact = 0,       // (Start+1, End+1): exact token/AST span, open-interval end
    Endless,         // (Start+1, 0):    known start, no endpoint (never-closed, TabError)
    NoPosition,      // (0, 0):          eval/single EOF fallback, no position at all
    EndOfInput,      // (Start+1, Start+2): exec EOF fallback, pinned to a 1-column input end
    OpenEnd,         // (Start+1, -1):   known start, endpoint not reached yet (expected block)
    ExplicitOpenEnd, // (explicit, -1):  indent family, offset computed by the raise site
}

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
        return SyntaxError(compiler, SyntaxErrorSpan.Exact, format, args);
    }

    internal PyRuntimeException SyntaxError(ICodeMetaInfoProvider compiler, SyntaxErrorSpan span, string format, params ReadOnlySpan<object?> args)
    {
        var exc = PySyntaxErrorObjectType.Shared.Create(PyStrObject.FromString(PySR.Format(format, args)));
        AttachCompilerLocation(exc, compiler.MetaInfo, span);
        return new PyRuntimeException(this, exc, compiler);
    }

    internal PyRuntimeException IndentationError(ICodeMetaInfoProvider compiler, string format, params ReadOnlySpan<object?> args)
    {
        return IndentationError(compiler, SyntaxErrorSpan.Exact, format, args);
    }

    internal PyRuntimeException IndentationError(ICodeMetaInfoProvider compiler, SyntaxErrorSpan span, string format, params ReadOnlySpan<object?> args)
    {
        var exc = PyIndentationErrorObjectType.Shared.Create(PyStrObject.FromString(PySR.Format(format, args)));
        AttachCompilerLocation(exc, compiler.MetaInfo, span);
        return new PyRuntimeException(this, exc, compiler);
    }

    // indent errors whose start column follows the tokenizer's own accounting
    // (unexpected indent: first non-blank column; unindent: scan column), not
    // the provider's span — the value lands in e.offset verbatim
    internal PyRuntimeException IndentationError(ICodeMetaInfoProvider compiler, int offset, string format, params ReadOnlySpan<object?> args)
    {
        var exc = PyIndentationErrorObjectType.Shared.Create(PyStrObject.FromString(PySR.Format(format, args)));
        AttachCompilerLocation(exc, compiler.MetaInfo, SyntaxErrorSpan.ExplicitOpenEnd, offset);
        return new PyRuntimeException(this, exc, compiler);
    }

    internal PyRuntimeException TabError(ICodeMetaInfoProvider compiler, string format, params ReadOnlySpan<object?> args)
    {
        return TabError(compiler, SyntaxErrorSpan.Exact, format, args);
    }

    internal PyRuntimeException TabError(ICodeMetaInfoProvider compiler, SyntaxErrorSpan span, string format, params ReadOnlySpan<object?> args)
    {
        var exc = PyTabErrorObjectType.Shared.Create(PyStrObject.FromString(PySR.Format(format, args)));
        AttachCompilerLocation(exc, compiler.MetaInfo, span);
        return new PyRuntimeException(this, exc, compiler);
    }

    // CPython raises parse-time errors with the full location info tuple
    // (_PyPegen_raise_error_known_location), so the display layer renders
    // the File/caret block from the exception's own attributes
    // (traceback.py _format_syntax_error) instead of a traceback frame
    private static void AttachCompilerLocation(PyExceptionObject exc, CodeMetaInfo? metaInfo, SyntaxErrorSpan span = SyntaxErrorSpan.Exact, int explicitOffset = 0)
    {
        if (metaInfo is null)
            return;

        var source = metaInfo.Source;
        PyObject filename = PyNoneObject.None;
        PyObject text = PyNoneObject.None;
        if (source is not null)
        {
            filename = PyStrObject.FromString(source.Name);
            exc.SetMember("filename", filename);
            // CPython's text keeps the physical line including its line break
            if (source.Code.TryGetLine(metaInfo.Start.Line, true, out var line))
                exc.SetMember("text", text = PyStrObject.FromString(line.ToString()));
        }
        var offset = span switch
        {
            SyntaxErrorSpan.NoPosition => 0,
            SyntaxErrorSpan.ExplicitOpenEnd => explicitOffset,
            _ => metaInfo.Start.Offset + 1,
        };
        var endOffset = span switch
        {
            SyntaxErrorSpan.Exact => metaInfo.End.Offset + 1,
            SyntaxErrorSpan.EndOfInput => metaInfo.Start.Offset + 2,
            SyntaxErrorSpan.Endless => 0,
            SyntaxErrorSpan.NoPosition => 0,
            SyntaxErrorSpan.OpenEnd => -1,
            SyntaxErrorSpan.ExplicitOpenEnd => -1,
            _ => throw new UnreachableException(),
        };
        var lineno = PyIntObject.FromInteger(metaInfo.Start.Line);
        var endLineno = PyIntObject.FromInteger(metaInfo.End.Line);
        exc.SetMember("lineno", lineno);
        exc.SetMember("offset", PyIntObject.FromInteger(offset));
        exc.SetMember("end_lineno", endLineno);
        exc.SetMember("end_offset", PyIntObject.FromInteger(endOffset));

        // CPython's parse errors carry (msg, info-tuple) args
        exc.Args =
        [
            exc.Args is [var msg, ..] ? msg : PyNoneObject.None,
            PyTupleObject.CreateTuple(filename, lineno, PyIntObject.FromInteger(offset), text, endLineno, PyIntObject.FromInteger(endOffset)),
        ];
    }

    // Known-gap marker for Python-reachable unimplemented/unsupported behavior,
    // never for ordinary user errors — the full contract lives on PySharpException.
    internal PyRuntimeException PySharpException(string? format, params ReadOnlySpan<object?> args)
    {
        return ThrowableException(Modules.CSharp.PySharpException.Shared, format, args);
    }
}