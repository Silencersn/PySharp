using PySharp.Modules.Builtins;
using System.Diagnostics.CodeAnalysis;

namespace PySharp.Runtime.Calls;

public readonly partial struct PyResult
{
    private readonly PyObject? _value;
    private readonly PyExceptionObject? _exception;

    [MemberNotNullWhen(true, nameof(Value))]
    [MemberNotNullWhen(false, nameof(Exception))]
    public bool IsSuccessful => _exception is null;

    [MemberNotNullWhen(false, nameof(Value))]
    [MemberNotNullWhen(true, nameof(Exception))]
    public bool IsError => _exception is not null;

    [MemberNotNullWhen(true, nameof(Value))]
    public bool IsNotImplemented => _value is PyNotImplementedObject;

    [MemberNotNullWhen(true, nameof(Exception))]
    public bool IsStopIteration => _exception is not null && PyStopIterationObjectType.Shared.IsInstance(_exception);

    [MemberNotNullWhen(true, nameof(Exception))]
    public bool IsAttributeError => _exception is not null && PyAttributeErrorObjectType.Shared.IsInstance(_exception);

    // default(PyResult) is regarded as PyResult.FromValue(PyNoneObject.None)
    public PyObject? Value => _value ?? (IsError ? null : PyNoneObject.None);
    public PyExceptionObject? Exception => _exception;
    public PyExceptionResult ExceptionResult => new(_exception);

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
    public static implicit operator PyResult(PyExceptionResult value)
    {
        if (value.Exception is null)
            return default;

        return FromException(value.Exception);
    }

    public PyResult<TObject> Of<TObject>() where TObject : PyObject
    {
        if (IsError)
            return PyResult<TObject>.FromException(Exception);

        if (Value is TObject objOfT)
            return objOfT;

        throw new InvalidOperationException();
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