using PySharp.Modules.Builtins;

namespace PySharp.Runtime.Calls;

partial class PyCallContext
{
    internal PyRuntimeException ThrowableException(PyExceptionType exceptionType)
    {
        return ThrowableException(exceptionType, null as PyObject);
    }

    internal PyRuntimeException ThrowableException(PyExceptionType exceptionType, string? arg)
    {
        return ThrowableException(exceptionType, arg is not null ? PyStrObject.FromString(arg) : null);
    }

    internal PyRuntimeException CreateException(PyExceptionType exceptionType, string? format, ReadOnlySpan<object?> args)
    {
        return ThrowableException(exceptionType, PySR.Format(format ?? string.Empty, args));
    }

    internal PyRuntimeException ThrowableException(PyExceptionType exceptionType, PyObject? arg)
    {
        return new PyRuntimeException(this, exceptionType.Create(arg));
    }



    internal PyRuntimeException TypeError(string? format, params ReadOnlySpan<object?> args)
    {
        return CreateException(PyTypeErrorObjectType.Shared, format, args);
    }

    internal PyRuntimeException ValueError(string? format, params ReadOnlySpan<object?> args)
    {
        return CreateException(PyValueErrorObjectType.Shared, format, args);
    }

    internal PyRuntimeException IndexError(string? format, params ReadOnlySpan<object?> args)
    {
        return CreateException(PyIndexErrorObjectType.Shared, format, args);
    }

    internal PyRuntimeException SyntaxError(string? format, params ReadOnlySpan<object?> args)
    {
        return CreateException(PySyntaxErrorObjectType.Shared, format, args);
    }

    internal PyRuntimeException UnicodeEncodeError(string? format, params ReadOnlySpan<object?> args)
    {
        return CreateException(PyUnicodeEncodeErrorObjectType.Shared, format, args);
    }

    internal PyRuntimeException IndentationError(string? format, params ReadOnlySpan<object?> args)
    {
        return CreateException(PyIndentationErrorObjectType.Shared, format, args);
    }

    internal PyRuntimeException KeyError(string? format, params ReadOnlySpan<object?> args)
    {
        return CreateException(PyKeyErrorObjectType.Shared, format, args);
    }

    internal PyRuntimeException AssertionError(string? format, params ReadOnlySpan<object?> args)
    {
        return CreateException(PyAssertionErrorObjectType.Shared, format, args);
    }

    internal PyRuntimeException AssertionError(PyObject? arg)
    {
        return ThrowableException(PyAssertionErrorObjectType.Shared, arg);
    }

    internal PyRuntimeException ZeroDivisionError(string? format, params ReadOnlySpan<object?> args)
    {
        return CreateException(PyZeroDivisionErrorObjectType.Shared, format, args);
    }

    internal PyRuntimeException AttributeError(string? format, params ReadOnlySpan<object?> args)
    {
        return CreateException(PyAttributeErrorObjectType.Shared, format, args);
    }

    internal PyRuntimeException SystemExit(string? format, params ReadOnlySpan<object?> args)
    {
        return CreateException(PySystemExitObjectType.Shared, format, args);
    }

    internal PyRuntimeException StopIteration(string? format, params ReadOnlySpan<object?> args)
    {
        return CreateException(PyStopIterationObjectType.Shared, format, args);
    }

    internal PyRuntimeException RuntimeError(string? format, params ReadOnlySpan<object?> args)
    {
        return CreateException(PyRuntimeErrorObjectType.Shared, format, args);
    }

    internal PyRuntimeException GeneratorExit(string? format, params ReadOnlySpan<object?> args)
    {
        return CreateException(PyGeneratorExitObjectType.Shared, format, args);
    }

    internal PyRuntimeException ModuleNotFoundError(string? format, params ReadOnlySpan<object?> args)
    {
        return CreateException(PyModuleNotFoundErrorObjectType.Shared, format, args);
    }

    internal PyRuntimeException ImportError(string? format, params ReadOnlySpan<object?> args)
    {
        return CreateException(PyImportErrorObjectType.Shared, format, args);
    }

    internal PyRuntimeException UnboundLocalError(string? format, params ReadOnlySpan<object?> args)
    {
        return CreateException(PyUnboundLocalErrorObjectType.Shared, format, args);
    }

    internal PyRuntimeException NameError(string? format, params ReadOnlySpan<object?> args)
    {
        return CreateException(PyNameErrorObjectType.Shared, format, args);
    }

    internal PyRuntimeException PySharpException(string? format, params ReadOnlySpan<object?> args)
    {
        return CreateException(PyResult.PySharpException.Shared, format, args);
    }
}