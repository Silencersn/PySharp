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

    public static PyExceptionResult TypeError(string? format, params ReadOnlySpan<object?> args)
    {
        return RaiseException(PyTypeErrorObjectType.Shared, format, args);
    }

    public static PyExceptionResult ValueError(string? format, params ReadOnlySpan<object?> args)
    {
        return RaiseException(PyValueErrorObjectType.Shared, format, args);
    }
    public static PyExceptionResult OverflowError(string? format, params ReadOnlySpan<object?> args)
    {
        return RaiseException(PyOverflowErrorObjectType.Shared, format, args);
    }
    public static PyExceptionResult IndexError(string? format, params ReadOnlySpan<object?> args)
    {
        return RaiseException(PyIndexErrorObjectType.Shared, format, args);
    }

    public static PyExceptionResult SyntaxError(string? format, params ReadOnlySpan<object?> args)
    {
        return RaiseException(PySyntaxErrorObjectType.Shared, format, args);
    }

    public static PyExceptionResult IndentationError(string? format, params ReadOnlySpan<object?> args)
    {
        return RaiseException(PyIndentationErrorObjectType.Shared, format, args);
    }

    public static PyExceptionResult KeyError(string? format, params ReadOnlySpan<object?> args)
    {
        return RaiseException(PyKeyErrorObjectType.Shared, format, args);
    }

    public static PyExceptionResult KeyError(PyObject? arg)
    {
        return RaiseException(PyKeyErrorObjectType.Shared, arg);
    }

    public static PyExceptionResult AssertionError(string? format, params ReadOnlySpan<object?> args)
    {
        return RaiseException(PyAssertionErrorObjectType.Shared, format, args);
    }

    public static PyExceptionResult RaiseAssertionError(string? arg = null)
    {
        return RaiseException(PyAssertionErrorObjectType.Shared, arg);
    }

    public static PyExceptionResult RaiseAssertionError(PyObject? arg)
    {
        return RaiseException(PyAssertionErrorObjectType.Shared, arg);
    }

    public static PyExceptionResult ZeroDivisionError(string? format = PySR.Runtime_Number_DivisionByZero, params ReadOnlySpan<object?> args)
    {
        return RaiseException(PyZeroDivisionErrorObjectType.Shared, format, args);
    }

    public static PyExceptionResult AttributeError(string? format, params ReadOnlySpan<object?> args)
    {
        return RaiseException(PyAttributeErrorObjectType.Shared, format, args);
    }

    public static PyExceptionResult SystemExit(string? format, params ReadOnlySpan<object?> args)
    {
        return RaiseException(PySystemExitObjectType.Shared, format, args);
    }

    public static PyExceptionResult StopIteration(string? format, params ReadOnlySpan<object?> args)
    {
        return RaiseException(PyStopIterationObjectType.Shared, format, args);
    }
    public static PyExceptionResult StopIteration(PyObject? arg = null)
    {
        return RaiseException(PyStopIterationObjectType.Shared, arg);
    }

    public static PyExceptionResult RuntimeError(string? format, params ReadOnlySpan<object?> args)
    {
        return RaiseException(PyRuntimeErrorObjectType.Shared, format, args);
    }

    public static PyExceptionResult GeneratorExit(string? format, params ReadOnlySpan<object?> args)
    {
        return RaiseException(PyGeneratorExitObjectType.Shared, format, args);
    }

    public static PyExceptionResult ModuleNotFoundError(string? format, params ReadOnlySpan<object?> args)
    {
        return RaiseException(PyModuleNotFoundErrorObjectType.Shared, format, args);
    }

    public static PyExceptionResult ImportError(string? format, params ReadOnlySpan<object?> args)
    {
        return RaiseException(PyImportErrorObjectType.Shared, format, args);
    }
    public static PyExceptionResult UnboundLocalError(string? format, params ReadOnlySpan<object?> args)
    {
        return RaiseException(PyUnboundLocalErrorObjectType.Shared, format, args);
    }
    public static PyExceptionResult NameError(string? format, params ReadOnlySpan<object?> args)
    {
        return RaiseException(PyNameErrorObjectType.Shared, format, args);
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
