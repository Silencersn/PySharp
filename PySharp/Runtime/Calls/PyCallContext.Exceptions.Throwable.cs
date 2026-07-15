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



    internal PyRuntimeException TypeError(string? format, params ReadOnlySpan<object?> args)
    {
        return ThrowableException(PyTypeErrorObjectType.Shared, format, args);
    }

    internal PyRuntimeException ValueError(string? format, params ReadOnlySpan<object?> args)
    {
        return ThrowableException(PyValueErrorObjectType.Shared, format, args);
    }

    internal PyRuntimeException IndexError(string? format, params ReadOnlySpan<object?> args)
    {
        return ThrowableException(PyIndexErrorObjectType.Shared, format, args);
    }

    internal PyRuntimeException SyntaxError(string? format, params ReadOnlySpan<object?> args)
    {
        return ThrowableException(PySyntaxErrorObjectType.Shared, format, args);
    }

    internal PyRuntimeException SyntaxError(ICodeMetaInfoProvider compiler, string format, params ReadOnlySpan<object?> args)
    {
        var exc = PySyntaxErrorObjectType.Shared.Create(PyStrObject.FromString(PySR.Format(format, args)));
        return new PyRuntimeException(this, exc, compiler);
    }

    internal PyRuntimeException UnicodeEncodeError(string? format, params ReadOnlySpan<object?> args)
    {
        return ThrowableException(PyUnicodeEncodeErrorObjectType.Shared, format, args);
    }

    internal PyRuntimeException IndentationError(string? format, params ReadOnlySpan<object?> args)
    {
        return ThrowableException(PyIndentationErrorObjectType.Shared, format, args);
    }

    internal PyRuntimeException KeyError(string? format, params ReadOnlySpan<object?> args)
    {
        return ThrowableException(PyKeyErrorObjectType.Shared, format, args);
    }

    internal PyRuntimeException AssertionError(string? format, params ReadOnlySpan<object?> args)
    {
        return ThrowableException(PyAssertionErrorObjectType.Shared, format, args);
    }

    internal PyRuntimeException AssertionError(PyObject? arg)
    {
        return ThrowableException(PyAssertionErrorObjectType.Shared, arg);
    }

    internal PyRuntimeException ZeroDivisionError(string? format, params ReadOnlySpan<object?> args)
    {
        return ThrowableException(PyZeroDivisionErrorObjectType.Shared, format, args);
    }

    internal PyRuntimeException AttributeError(string? format, params ReadOnlySpan<object?> args)
    {
        return ThrowableException(PyAttributeErrorObjectType.Shared, format, args);
    }

    internal PyRuntimeException SystemExit(string? format, params ReadOnlySpan<object?> args)
    {
        return ThrowableException(PySystemExitObjectType.Shared, format, args);
    }

    internal PyRuntimeException StopIteration(string? format, params ReadOnlySpan<object?> args)
    {
        return ThrowableException(PyStopIterationObjectType.Shared, format, args);
    }

    internal PyRuntimeException RuntimeError(string? format, params ReadOnlySpan<object?> args)
    {
        return ThrowableException(PyRuntimeErrorObjectType.Shared, format, args);
    }

    internal PyRuntimeException GeneratorExit(string? format, params ReadOnlySpan<object?> args)
    {
        return ThrowableException(PyGeneratorExitObjectType.Shared, format, args);
    }

    internal PyRuntimeException ModuleNotFoundError(string? format, params ReadOnlySpan<object?> args)
    {
        return ThrowableException(PyModuleNotFoundErrorObjectType.Shared, format, args);
    }

    internal PyRuntimeException ImportError(string? format, params ReadOnlySpan<object?> args)
    {
        return ThrowableException(PyImportErrorObjectType.Shared, format, args);
    }

    internal PyRuntimeException UnboundLocalError(string? format, params ReadOnlySpan<object?> args)
    {
        return ThrowableException(PyUnboundLocalErrorObjectType.Shared, format, args);
    }

    internal PyRuntimeException NameError(string? format, params ReadOnlySpan<object?> args)
    {
        return ThrowableException(PyNameErrorObjectType.Shared, format, args);
    }

    internal PyRuntimeException PySharpException(string? format, params ReadOnlySpan<object?> args)
    {
        return ThrowableException(PyResult.PySharpException.Shared, format, args);
    }
}