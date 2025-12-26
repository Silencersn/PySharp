using PySharp.PyModules.Builtins;
using System.Diagnostics.CodeAnalysis;

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

    internal PyRuntimeException ThrowableException(PyExceptionType exceptionType, PyObject? arg)
    {
        return new PyRuntimeException(this, exceptionType.Create(arg));
    }


    internal PyRuntimeException ThrowableTypeError(string? arg = null)
    {
        return ThrowableException(PyStandardExceptionTypes.TypeError, arg);
    }

    internal PyRuntimeException ThrowableValueError(string? arg = null)
    {
        return ThrowableException(PyStandardExceptionTypes.ValueError, arg);
    }

    internal PyRuntimeException ThrowableIndexError(string? arg = null)
    {
        return ThrowableException(PyStandardExceptionTypes.IndexError, arg);
    }

    internal PyRuntimeException ThrowableSyntaxError(string? arg = null)
    {
        return ThrowableException(PyStandardExceptionTypes.SyntaxError, arg);
    }

    internal PyRuntimeException ThrowableIndentationError(string? arg = null)
    {
        return ThrowableException(PyStandardExceptionTypes.IndentationError, arg);
    }

    internal PyRuntimeException ThrowableKeyError(string? arg = null)
    {
        return ThrowableException(PyStandardExceptionTypes.KeyError, arg);
    }

    internal PyRuntimeException ThrowableKeyError(PyObject? arg)
    {
        return ThrowableException(PyStandardExceptionTypes.KeyError, arg);
    }

    internal PyRuntimeException ThrowableAssertionError(string? arg = null)
    {
        return ThrowableException(PyStandardExceptionTypes.AssertionError, arg);
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

    internal PyRuntimeException ThrowableSystemExit(string? arg = null)
    {
        return ThrowableException(PyStandardExceptionTypes.SystemExit, arg);
    }

    internal PyRuntimeException ThrowableStopIteration(string? arg = null)
    {
        return ThrowableException(PyStandardExceptionTypes.StopIteration, arg);
    }

    internal PyRuntimeException ThrowableRuntimeError(string? arg = null)
    {
        return ThrowableException(PyStandardExceptionTypes.RuntimeError, arg);
    }

    internal PyRuntimeException ThrowableGeneratorExit(string? arg = null)
    {
        return ThrowableException(PyStandardExceptionTypes.GeneratorExit, arg);
    }

    internal PyRuntimeException ThrowableModuleNotFoundError(string? arg = null)
    {
        return ThrowableException(PyStandardExceptionTypes.ModuleNotFoundError, arg);
    }
    internal PyRuntimeException ThrowableImportError(string? arg = null)
    {
        return ThrowableException(PyStandardExceptionTypes.ImportError, arg);
    }
}
