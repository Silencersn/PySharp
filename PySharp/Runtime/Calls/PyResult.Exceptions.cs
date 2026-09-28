using PySharp.Modules.Builtins;

namespace PySharp.Runtime.Calls;

partial struct PyResult
{
    public readonly ref struct PyExceptionResult
    {
        internal readonly PyExceptionObject? Exception;

        public PyExceptionResult(PyExceptionObject? exception)
        {
            Exception = exception;
        }
    }

    internal static PyExceptionResult RaiseException(PyTypeObject<PyExceptionObject> exceptionType)
    {
        return RaiseException(exceptionType, null as PyObject);
    }
    internal static PyExceptionResult RaiseException(PyTypeObject<PyExceptionObject> exceptionType, string? format, params ReadOnlySpan<object?> args)
    {
        return RaiseException(exceptionType, format is null ? null : PyStrObject.FromString(PySR.Format(format, args)));
    }
    internal static PyExceptionResult RaiseException(PyTypeObject<PyExceptionObject> exceptionType, PyObject? arg)
    {
        return new(PyExceptionObject.UnsafeCreate(exceptionType, arg is null ? [] : [arg]));
    }

    public static PyExceptionResult KeyError(PyObject? arg)
    {
        return RaiseException(PyKeyErrorObjectType.Shared, arg);
    }

    public static PyExceptionResult ZeroDivisionError()
    {
        return ZeroDivisionError(PySR.Runtime_Number_DivisionByZero);
    }

    public static PyExceptionResult StopIteration(PyObject? arg = null)
    {
        return RaiseException(PyStopIterationObjectType.Shared, arg);
    }

    // Known-gap marker for Python-reachable unimplemented/unsupported behavior,
    // never for ordinary user errors — the full contract lives on PySharpException.
    internal static PyExceptionResult PySharpException(string? format, params ReadOnlySpan<object?> args)
    {
        return RaiseException(Modules.CSharp.PySharpException.Shared, format, args);
    }
}
