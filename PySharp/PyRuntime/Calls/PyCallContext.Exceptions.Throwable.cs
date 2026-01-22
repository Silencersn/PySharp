using PySharp.PyModules.Builtins;
using PySharp.Resources;

namespace PySharp.PyRuntime.Calls;

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

    internal PyRuntimeException ThrowableIndexError(string? arg = null)
    {
        return ThrowableException(PyStandardExceptionTypes.IndexError, arg);
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

    internal PyRuntimeException ThrowableKeyError(string? arg = null)
    {
        return ThrowableException(PyStandardExceptionTypes.KeyError, arg);
    }

    internal PyRuntimeException ThrowableKeyError(PyObject? arg)
    {
        return ThrowableException(PyStandardExceptionTypes.KeyError, arg);
    }

    internal PyRuntimeException AssertionError(string? format, params ReadOnlySpan<object?> args)
    {
        return CreateException(PyAssertionErrorObjectType.Shared, format, args);
    }

    internal PyRuntimeException ThrowableAssertionError(PyObject? arg)
    {
        return ThrowableException(PyStandardExceptionTypes.AssertionError, arg);
    }

    internal PyRuntimeException ThrowableZeroDivisionError(string? arg = null)
    {
        return ThrowableException(PyStandardExceptionTypes.ZeroDivisionError, arg);
    }

    internal PyRuntimeException ThrowableAttributeError(string? arg = null)
    {
        return ThrowableException(PyStandardExceptionTypes.AttributeError, arg);
    }

    internal PyRuntimeException SystemExit(string? format, params ReadOnlySpan<object?> args)
    {
        return CreateException(PySystemExitObjectType.Shared, format, args);
    }

    internal PyRuntimeException ThrowableStopIteration(string? arg = null)
    {
        return ThrowableException(PyStandardExceptionTypes.StopIteration, arg);
    }

    internal PyRuntimeException ThrowableRuntimeError(string? arg = null)
    {
        return ThrowableException(PyStandardExceptionTypes.RuntimeError, arg);
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

    internal PyRuntimeException ThrowableUnboundLocalError(string? arg = null)
    {
        return ThrowableException(PyStandardExceptionTypes.UnboundLocalError, arg);
    }
    internal PyRuntimeException ThrowableNameError(string? arg = null)
    {
        return ThrowableException(PyStandardExceptionTypes.NameError, arg);
    }

    internal PyRuntimeException ThrowablePySharpException(string arg)
    {
        return ThrowableException(PyResult.PySharpException.Shared, arg);
    }
}