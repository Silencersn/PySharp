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

    internal PyRuntimeException SyntaxError(ICodeMetaInfoProvider compiler, string format, params ReadOnlySpan<object?> args)
    {
        var exc = PySyntaxErrorObjectType.Shared.Create(PyStrObject.FromString(PySR.Format(format, args)));
        return new PyRuntimeException(this, exc, compiler);
    }

    internal PyRuntimeException IndentationError(ICodeMetaInfoProvider compiler, string format, params ReadOnlySpan<object?> args)
    {
        var exc = PyIndentationErrorObjectType.Shared.Create(PyStrObject.FromString(PySR.Format(format, args)));
        return new PyRuntimeException(this, exc, compiler);
    }

    internal PyRuntimeException TabError(ICodeMetaInfoProvider compiler, string format, params ReadOnlySpan<object?> args)
    {
        var exc = PyTabErrorObjectType.Shared.Create(PyStrObject.FromString(PySR.Format(format, args)));
        return new PyRuntimeException(this, exc, compiler);
    }

    internal PyRuntimeException PySharpException(string? format, params ReadOnlySpan<object?> args)
    {
        return ThrowableException(Modules.CSharp.PySharpException.Shared, format, args);
    }
}