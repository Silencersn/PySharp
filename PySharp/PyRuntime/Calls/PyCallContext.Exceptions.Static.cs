using PySharp.PyModules.Builtins;

namespace PySharp.PyRuntime.Calls;

partial class PyCallContext
{
    internal static PyRuntimeException ThrowException(PyExceptionType exceptionType)
    {
        return ThrowException(exceptionType, null as PyObject);
    }

    internal static PyRuntimeException ThrowException(PyExceptionType exceptionType, string? arg)
    {
        return ThrowException(exceptionType, arg is not null ? PyStrObject.FromString(arg) : null);
    }

    internal static PyRuntimeException ThrowException(PyExceptionType exceptionType, PyObject? arg)
    {
        return new PyRuntimeException(exceptionType.Create(arg));
    }


    internal static PyRuntimeException ThrowTypeError(string? arg = null)
    {
        return ThrowException(PyStandardExceptionTypes.TypeError, arg);
    }

    internal static PyRuntimeException ThrowValueError(string? arg = null)
    {
        return ThrowException(PyStandardExceptionTypes.ValueError, arg);
    }

    internal static PyRuntimeException ThrowIndexError(string? arg = null)
    {
        return ThrowException(PyStandardExceptionTypes.IndexError, arg);
    }

    internal static PyRuntimeException ThrowSyntaxError(string? arg = null)
    {
        return ThrowException(PyStandardExceptionTypes.SyntaxError, arg);
    }

    internal static PyRuntimeException ThrowIndentationError(string? arg = null)
    {
        return ThrowException(PyStandardExceptionTypes.IndentationError, arg);
    }

    internal static PyRuntimeException ThrowKeyError(string? arg = null)
    {
        return ThrowException(PyStandardExceptionTypes.KeyError, arg);
    }

    internal static PyRuntimeException ThrowKeyError(PyObject? arg)
    {
        return ThrowException(PyStandardExceptionTypes.KeyError, arg);
    }

    internal static PyRuntimeException ThrowAssertionError(string? arg = null)
    {
        return ThrowException(PyStandardExceptionTypes.AssertionError, arg);
    }

    internal static PyRuntimeException ThrowAssertionError(PyObject? arg)
    {
        return ThrowException(PyStandardExceptionTypes.AssertionError, arg);
    }

    internal static PyRuntimeException ThrowZeroDivisionError(string? arg = null)
    {
        return ThrowException(PyStandardExceptionTypes.ZeroDivisionError, arg);
    }

    internal static PyRuntimeException ThrowAttributeError(string? arg = null)
    {
        return ThrowException(PyStandardExceptionTypes.AttributeError, arg);
    }

    internal static PyRuntimeException ThrowSystemExit(string? arg = null)
    {
        return ThrowException(PyStandardExceptionTypes.SystemExit, arg);
    }

    internal static PyRuntimeException ThrowStopIteration(string? arg = null)
    {
        return ThrowException(PyStandardExceptionTypes.StopIteration, arg);
    }

    internal static PyRuntimeException ThrowRuntimeError(string? arg = null)
    {
        return ThrowException(PyStandardExceptionTypes.RuntimeError, arg);
    }


    internal static PyRuntimeException ThrowPySharpException(string arg)
    {
        return ThrowException(PySharpException.Shared, arg);
    }

}
