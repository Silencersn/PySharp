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

    internal static PyRuntimeException ThrowIndentationError(string? arg = null)
    {
        return ThrowException(PyStandardExceptionTypes.IndentationError, arg);
    }
}
