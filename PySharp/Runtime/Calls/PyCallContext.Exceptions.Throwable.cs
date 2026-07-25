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
        return new PyRuntimeException(this, new(exceptionType, arg is null ? [] : [arg]));
    }

    internal PyRuntimeException SyntaxError(ICodeMetaInfoProvider compiler, string format, params ReadOnlySpan<object?> args)
    {
        var exc = PySyntaxErrorObjectType.Shared.Create(PyStrObject.FromString(PySR.Format(format, args)));
        return new PyRuntimeException(this, exc, compiler);
    }

    internal PyRuntimeException PySharpException(string? format, params ReadOnlySpan<object?> args)
    {
        return ThrowableException(Modules.CSharp.PySharpException.Shared, format, args);
    }
}