using PySharp.PyModules.Builtins;
using System.Diagnostics.CodeAnalysis;
using static PySharp.PyRuntime.Calls.PyResult;

namespace PySharp.PyRuntime.Calls;

public readonly partial struct PyResult<TObject> where TObject : PyObject
{
    private readonly TObject? _value;
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
    public bool IsStopIteration => _exception is not null && PyStopIterationObjectType.Shared.IsInstance(_exception);

    [MemberNotNullWhen(true, nameof(Exception))]
    public bool IsAttributeError => _exception is not null && PyAttributeErrorObjectType.Shared.IsInstance(_exception);

    public TObject? Value => _value;
    public PyExceptionObject? Exception => _exception;

    private PyResult(TObject value)
    {
        _value = value;
        _exception = null;
    }

    private PyResult(PyExceptionObject exception)
    {
        _value = null;
        _exception = exception;
    }

    public static implicit operator PyResult<TObject>(TObject value)
    {
        return FromValue(value);
    }
    public static implicit operator PyResult(PyResult<TObject> result)
    {
        return result.IsSuccessful ? PyResult.FromValue(result.Value) : PyResult.FromException(result.Exception);
    }
    public PyResult<TOtherObject> Of<TOtherObject>() where TOtherObject : PyObject
    {
        if (IsError)
            return PyResult<TOtherObject>.FromException(Exception);

        if (Value is TOtherObject objOfOtherT)
            return objOfOtherT;

        throw new InvalidOperationException();
    }

    public static PyResult<TObject> FromValue(TObject value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new PyResult<TObject>(value);
    }

    public static PyResult<TObject> FromException(PyExceptionObject exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return new PyResult<TObject>(exception);
    }
}