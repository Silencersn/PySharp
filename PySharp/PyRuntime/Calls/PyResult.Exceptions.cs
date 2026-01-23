using PySharp.PyModules.Builtins;

namespace PySharp.PyRuntime.Calls;

partial struct PyResult
{
    // TODO: allow user defined error types

    internal static PyResult RaiseExceptionFromTypeOrInstance(PyObject pyObject)
    {
        if (pyObject is PyExceptionType exceptionType)
            return RaiseException(exceptionType);

        else if (pyObject is PyExceptionObject exceptionObject)
            return FromException(exceptionObject);

        return TypeError($"exceptions must be classes or instances deriving from BaseException, not {pyObject.PyType.Name}");
    }

    internal static PyResult RaiseException(PyExceptionType exceptionType)
    {
        return RaiseException(exceptionType, null as PyObject);
    }
    internal static PyResult RaiseException(PyExceptionType exceptionType, string? format, params ReadOnlySpan<object?> args)
    {
        return RaiseException(exceptionType, format is null ? null : PyStrObject.FromString(PySR.Format(format, args)));
    }
    internal static PyResult RaiseException(PyExceptionType exceptionType, PyObject? arg)
    {
        return FromException(exceptionType.Create(arg));
    }

    internal static PyResult TypeError(string? format, params ReadOnlySpan<object?> args)
    {
        return RaiseException(PyTypeErrorObjectType.Shared, format, args);
    }

    internal static PyResult ValueError(string? format, params ReadOnlySpan<object?> args)
    {
        return RaiseException(PyValueErrorObjectType.Shared, format, args);
    }

    internal static PyResult IndexError(string? format, params ReadOnlySpan<object?> args)
    {
        return RaiseException(PyIndexErrorObjectType.Shared, format, args);
    }

    internal static PyResult SyntaxError(string? format, params ReadOnlySpan<object?> args)
    {
        return RaiseException(PySyntaxErrorObjectType.Shared, format, args);
    }

    internal static PyResult IndentationError(string? format, params ReadOnlySpan<object?> args)
    {
        return RaiseException(PyIndentationErrorObjectType.Shared, format, args);
    }

    internal static PyResult KeyError(string? format, params ReadOnlySpan<object?> args)
    {
        return RaiseException(PyKeyErrorObjectType.Shared, format, args);
    }

    internal static PyResult KeyError(PyObject? arg)
    {
        return RaiseException(PyKeyErrorObjectType.Shared, arg);
    }

    internal static PyResult AssertionError(string? format, params ReadOnlySpan<object?> args)
    {
        return RaiseException(PyAssertionErrorObjectType.Shared, format, args);
    }

    internal static PyResult RaiseAssertionError(string? arg = null)
    {
        return RaiseException(PyStandardExceptionTypes.AssertionError, arg);
    }

    internal static PyResult RaiseAssertionError(PyObject? arg)
    {
        return RaiseException(PyStandardExceptionTypes.AssertionError, arg);
    }

    internal static PyResult ZeroDivisionError(string? format = PySR.Runtime_Number_DivisionByZero, params ReadOnlySpan<object?> args)
    {
        return RaiseException(PyZeroDivisionErrorObjectType.Shared, format, args);
    }

    internal static PyResult AttributeError(string? format, params ReadOnlySpan<object?> args)
    {
        return RaiseException(PyAttributeErrorObjectType.Shared, format, args);
    }

    internal static PyResult SystemExit(string? format, params ReadOnlySpan<object?> args)
    {
        return RaiseException(PySystemExitObjectType.Shared, format, args);
    }

    internal static PyResult StopIteration(string? format, params ReadOnlySpan<object?> args)
    {
        return RaiseException(PyStopIterationObjectType.Shared, format, args);
    }
    internal static PyResult StopIteration(PyObject? arg = null)
    {
        return RaiseException(PyStandardExceptionTypes.StopIteration, arg);
    }

    internal static PyResult RuntimeError(string? format, params ReadOnlySpan<object?> args)
    {
        return RaiseException(PyRuntimeErrorObjectType.Shared, format, args);
    }

    internal static PyResult GeneratorExit(string? format, params ReadOnlySpan<object?> args)
    {
        return RaiseException(PyGeneratorExitObjectType.Shared, format, args);
    }

    internal static PyResult ModuleNotFoundError(string? format, params ReadOnlySpan<object?> args)
    {
        return RaiseException(PyModuleNotFoundErrorObjectType.Shared, format, args);
    }

    internal static PyResult ImportError(string? format, params ReadOnlySpan<object?> args)
    {
        return RaiseException(PyImportErrorObjectType.Shared, format, args);
    }
    internal static PyResult UnboundLocalError(string? format, params ReadOnlySpan<object?> args)
    {
        return RaiseException(PyUnboundLocalErrorObjectType.Shared, format, args);
    }
    internal static PyResult NameError(string? format, params ReadOnlySpan<object?> args)
    {
        return RaiseException(PyNameErrorObjectType.Shared, format, args);
    }

    internal sealed class PySharpException : PyExceptionType<PySharpException, PyBaseExceptionObjectType>
    {
        public override string Module => "pysharp";
        public override string Name => "PySharpException";
    }


    internal static PyResult RaisePySharpException(string? format, params ReadOnlySpan<object?> args)
    {
        return RaiseException(PySharpException.Shared, format, args);
    }
}
