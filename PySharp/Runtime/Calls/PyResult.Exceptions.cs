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
        return new(new(exceptionType, arg is null ? [] : [arg]));
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

    internal sealed class PySharpException : PyExceptionType
    {
        protected override string DefaultModule => "pysharp";
        protected override string DefaultName => "PySharpException";
        private PySharpException() { }
        public static PySharpException Shared { get; } = new PySharpException();
        public sealed override IReadOnlyList<PyTypeObject> Bases => [PyBaseExceptionObjectType.Shared];
    }


    internal static PyExceptionResult RaisePySharpException(string? format, params ReadOnlySpan<object?> args)
    {
        return RaiseException(PySharpException.Shared, format, args);
    }
}
