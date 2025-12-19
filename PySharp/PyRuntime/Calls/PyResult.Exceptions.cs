using PySharp.PyModules.Builtins;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace PySharp.PyRuntime.Calls;

partial class PyResult
{
    internal static PyResult RaiseException(PyExceptionType exceptionType)
    {
        return RaiseException(exceptionType, null as PyObject);
    }
    internal static PyResult RaiseException(PyExceptionType exceptionType, string? arg)
    {
        return RaiseException(exceptionType, arg is not null ? PyStrObject.FromString(arg) : null);
    }
    internal static PyResult RaiseException(PyExceptionType exceptionType, PyObject? arg)
    {
        return FromException(exceptionType.Create(arg));
    }

    internal static PyResult RaiseTypeError(string? arg = null)
    {
        return RaiseException(PyStandardExceptionTypes.TypeError, arg);
    }

    internal static PyResult RaiseValueError(string? arg = null)
    {
        return RaiseException(PyStandardExceptionTypes.ValueError, arg);
    }

    internal static PyResult RaiseIndexError(string? arg = null)
    {
        return RaiseException(PyStandardExceptionTypes.IndexError, arg);
    }

    internal static PyResult RaiseSyntaxError(string? arg = null)
    {
        return RaiseException(PyStandardExceptionTypes.SyntaxError, arg);
    }

    internal static PyResult RaiseIndentationError(string? arg = null)
    {
        return RaiseException(PyStandardExceptionTypes.IndentationError, arg);
    }

    internal static PyResult RaiseKeyError(string? arg = null)
    {
        return RaiseException(PyStandardExceptionTypes.KeyError, arg);
    }

    internal static PyResult RaiseKeyError(PyObject? arg)
    {
        return RaiseException(PyStandardExceptionTypes.KeyError, arg);
    }

    internal static PyResult RaiseAssertionError(string? arg = null)
    {
        return RaiseException(PyStandardExceptionTypes.AssertionError, arg);
    }

    internal static PyResult RaiseAssertionError(PyObject? arg)
    {
        return RaiseException(PyStandardExceptionTypes.AssertionError, arg);
    }

    internal static PyResult RaiseZeroDivisionError(string? arg = null)
    {
        return RaiseException(PyStandardExceptionTypes.ZeroDivisionError, arg);
    }

    internal static PyResult RaiseAttributeError(string? arg = null)
    {
        return RaiseException(PyStandardExceptionTypes.AttributeError, arg);
    }

    internal static PyResult RaiseSystemExit(string? arg = null)
    {
        return RaiseException(PyStandardExceptionTypes.SystemExit, arg);
    }

    internal static PyResult RaiseStopIteration(string? arg = null)
    {
        return RaiseException(PyStandardExceptionTypes.StopIteration, arg);
    }

    internal static PyResult RaiseRuntimeError(string? arg = null)
    {
        return RaiseException(PyStandardExceptionTypes.RuntimeError, arg);
    }


    internal sealed class PySharpException : PyExceptionType
    {
        public static PySharpException Shared { get; } = new();
        public override string Name => "PySharpException";
    }


    internal static PyResult RaisePySharpException(string arg)
    {
        return RaiseException(PySharpException.Shared, arg);
    }

}
