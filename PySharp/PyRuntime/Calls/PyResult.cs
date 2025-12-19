using PySharp.PyModules.Builtins;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace PySharp.PyRuntime.Calls;

public readonly partial struct PyResult
{
    private readonly PyObject? _value;
    private readonly PyExceptionObject? _exception;

    [MemberNotNullWhen(true, nameof(Value))]
    [MemberNotNullWhen(false, nameof(Exception))]
    public bool IsSuccessful => _value is not null;

    [MemberNotNullWhen(false, nameof(Value))]
    [MemberNotNullWhen(true, nameof(Exception))]
    public bool IsError => _exception is not null;

    public PyObject? Value => _value;
    public PyExceptionObject? Exception => _exception;

    private PyResult(PyObject value)
    {
        _value = value;
        _exception = null;
    }

    private PyResult(PyExceptionObject exception)
    {
        _value = null;
        _exception = exception;
    }

    public static implicit operator PyResult(PyObject value)
    {
        return FromValue(value);
    }

    public static PyResult FromValue(PyObject value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new PyResult(value);
    }

    public static PyResult FromException(PyExceptionObject exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return new PyResult(exception);
    }

    // TODO: This is just a temporary approach and should be removed once the object logic has been fully migrated.
    internal static PyResult CaptureExceptionFromPVM()
    {
        Debug.Assert(PyVirtualMachine.CurrentException is not null);
        return FromException(PyVirtualMachine.CurrentException);
    }
}