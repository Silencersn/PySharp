using PySharp.PyModules.Builtins;
using System.Diagnostics.CodeAnalysis;

namespace PySharp.PyRuntime.Calls;

partial class PyCallContext
{
    internal PyExceptionObject? CurrentException
    {
        get => PyEnvironment.CurrentError;
        set => PyEnvironment.CurrentError = value;
    }

    [MemberNotNull(nameof(CurrentException))]
    internal PyObject? RaiseException(PyExceptionType exceptionType)
    {
        return RaiseException(exceptionType, null as PyObject);
    }
    [MemberNotNull(nameof(CurrentException))]
    internal PyObject? RaiseException(PyExceptionType exceptionType, string? arg)
    {
        return RaiseException(exceptionType, arg is not null ? PyStrObject.FromString(arg) : null);
    }
    [MemberNotNull(nameof(CurrentException))]
    internal PyObject? RaiseException(PyExceptionType exceptionType, PyObject? arg)
    {
        CurrentException = exceptionType.Create(arg);
        return null;
    }
    internal void ClearException()
    {
        CurrentException = null;
    }

    internal bool IsExceptionRaised()
    {
        return CurrentException is not null;
    }

    internal bool IsExceptionOfTypeRaised(PyExceptionType exceptionType)
    {
        return CurrentException is { PyType: var type } && type == exceptionType;
    }


    [MemberNotNull(nameof(CurrentException))]
    internal PyObject? RaiseTypeError(string? arg = null)
    {
        return RaiseException(PyStandardExceptionTypes.TypeError, arg);
    }
    [MemberNotNull(nameof(CurrentException))]
    internal PyObject? RaiseValueError(string? arg = null)
    {
        return RaiseException(PyStandardExceptionTypes.ValueError, arg);
    }
    [MemberNotNull(nameof(CurrentException))]
    internal PyObject? RaiseIndexError(string? arg = null)
    {
        return RaiseException(PyStandardExceptionTypes.IndexError, arg);
    }
    [MemberNotNull(nameof(CurrentException))]
    internal PyObject? RaiseSyntaxError(string? arg = null)
    {
        return RaiseException(PyStandardExceptionTypes.SyntaxError, arg);
    }
    [MemberNotNull(nameof(CurrentException))]
    internal PyObject? RaiseIndentationError(string? arg = null)
    {
        return RaiseException(PyStandardExceptionTypes.IndentationError, arg);
    }
    [MemberNotNull(nameof(CurrentException))]
    internal PyObject? RaiseKeyError(string? arg = null)
    {
        return RaiseException(PyStandardExceptionTypes.KeyError, arg);
    }
    [MemberNotNull(nameof(CurrentException))]
    internal PyObject? RaiseKeyError(PyObject? arg)
    {
        return RaiseException(PyStandardExceptionTypes.KeyError, arg);
    }
    [MemberNotNull(nameof(CurrentException))]
    internal PyObject? RaiseAssertionError(string? arg = null)
    {
        return RaiseException(PyStandardExceptionTypes.AssertionError, arg);
    }
    [MemberNotNull(nameof(CurrentException))]
    internal PyObject? RaiseAssertionError(PyObject? arg)
    {
        return RaiseException(PyStandardExceptionTypes.AssertionError, arg);
    }
    [MemberNotNull(nameof(CurrentException))]
    internal PyObject? RaiseZeroDivisionError(string? arg = null)
    {
        return RaiseException(PyStandardExceptionTypes.ZeroDivisionError, arg);
    }
    [MemberNotNull(nameof(CurrentException))]
    internal PyObject? RaiseAttributeError(string? arg = null)
    {
        return RaiseException(PyStandardExceptionTypes.AttributeError, arg);
    }
    [MemberNotNull(nameof(CurrentException))]
    internal PyObject? RaiseSystemExit(string? arg = null)
    {
        return RaiseException(PyStandardExceptionTypes.SystemExit, arg);
    }
    [MemberNotNull(nameof(CurrentException))]
    internal PyObject? RaiseStopIteration(string? arg = null)
    {
        return RaiseException(PyStandardExceptionTypes.StopIteration, arg);
    }
    [MemberNotNull(nameof(CurrentException))]
    internal PyObject? RaiseRuntimeError(string? arg = null)
    {
        return RaiseException(PyStandardExceptionTypes.RuntimeError, arg);
    }


    internal sealed class PySharpException : PyExceptionType
    {
        public static PySharpException Shared { get; } = new();
        public override string Name => "PySharpException";
    }

    [MemberNotNull(nameof(CurrentException))]
    internal PyObject? RaisePySharpException(string arg)
    {
        return RaiseException(PySharpException.Shared, arg);
    }

}
