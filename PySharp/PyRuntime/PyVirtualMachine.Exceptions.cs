using PySharp.PyObjects.Builtins;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace PySharp.PyRuntime;

partial class PyVirtualMachine
{
    [MemberNotNull(nameof(CurrentException))]
    internal static PyObject? RaiseException(PyExceptionType exceptionType)
    {
        return RaiseException(exceptionType, null as PyObject);
    }
    [MemberNotNull(nameof(CurrentException))]
    internal static PyObject? RaiseException(PyExceptionType exceptionType, string? arg)
    {
        return RaiseException(exceptionType, arg is not null ? PyStrObject.FromString(arg) : null);
    }
    [MemberNotNull(nameof(CurrentException))]
    internal static PyObject? RaiseException(PyExceptionType exceptionType, PyObject? arg)
    {
        CurrentException = exceptionType.Create(arg);
        return null;
    }
    internal static void ClearException()
    {
        CurrentException = null;
    }

    internal static bool IsExceptionRaised()
    {
        return CurrentException is not null;
    }

    internal static bool IsExceptionOfTypeRaised(PyExceptionType exceptionType)
    {
        return CurrentException is { PyType: var type } && type == exceptionType;
    }


    [MemberNotNull(nameof(CurrentException))]
    internal static PyObject? RaiseTypeError(string? arg = null)
    {
        return RaiseException(PyStandardExceptionTypes.TypeError, arg);
    }
    [MemberNotNull(nameof(CurrentException))]
    internal static PyObject? RaiseValueError(string? arg = null)
    {
        return RaiseException(PyStandardExceptionTypes.ValueError, arg);
    }
    [MemberNotNull(nameof(CurrentException))]
    internal static PyObject? RaiseIndexError(string? arg = null)
    {
        return RaiseException(PyStandardExceptionTypes.IndexError, arg);
    }
    [MemberNotNull(nameof(CurrentException))]
    internal static PyObject? RaiseSyntaxError(string? arg = null)
    {
        return RaiseException(PyStandardExceptionTypes.SyntaxError, arg);
    }
    [MemberNotNull(nameof(CurrentException))]
    internal static PyObject? RaiseIndentationError(string? arg = null)
    {
        return RaiseException(PyStandardExceptionTypes.IndentationError, arg);
    }
    [MemberNotNull(nameof(CurrentException))]
    internal static PyObject? RaiseKeyError(string? arg = null)
    {
        return RaiseException(PyStandardExceptionTypes.KeyError, arg);
    }
    [MemberNotNull(nameof(CurrentException))]
    internal static PyObject? RaiseKeyError(PyObject? arg)
    {
        return RaiseException(PyStandardExceptionTypes.KeyError, arg);
    }
    [MemberNotNull(nameof(CurrentException))]
    internal static PyObject? RaiseAssertionError(string? arg = null)
    {
        return RaiseException(PyStandardExceptionTypes.AssertionError, arg);
    }
    [MemberNotNull(nameof(CurrentException))]
    internal static PyObject? RaiseAssertionError(PyObject? arg)
    {
        return RaiseException(PyStandardExceptionTypes.AssertionError, arg);
    }
    [MemberNotNull(nameof(CurrentException))]
    internal static PyObject? RaiseZeroDivisionError(string? arg = null)
    {
        return RaiseException(PyStandardExceptionTypes.ZeroDivisionError, arg);
    }
    [MemberNotNull(nameof(CurrentException))]
    internal static PyObject? RaiseAttributeError(string? arg = null)
    {
        return RaiseException(PyStandardExceptionTypes.AttributeError, arg);
    }
    [MemberNotNull(nameof(CurrentException))]
    internal static PyObject? RaiseSystemExit(string? arg = null)
    {
        return RaiseException(PyStandardExceptionTypes.SystemExit, arg);
    }
    [MemberNotNull(nameof(CurrentException))]
    internal static PyObject? RaiseStopIteration(string? arg = null)
    {
        return RaiseException(PyStandardExceptionTypes.StopIteration, arg);
    }
}
