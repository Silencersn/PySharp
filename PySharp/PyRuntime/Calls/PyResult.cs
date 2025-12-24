using PySharp.PyModules.Builtins;
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

    [MemberNotNullWhen(true, nameof(Value))]
    public bool IsNotImplemented => _value is PyNotImplementedObject;

    [MemberNotNullWhen(true, nameof(Exception))]
    public bool IsStopIteration => _exception?.PyType is PyStopIterationObjectType;

    [MemberNotNullWhen(true, nameof(Exception))]
    public bool IsAttributeError => _exception?.PyType is PyAttributeErrorObjectType;

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
}