using PySharp.PyModules.Builtins;
using PySharp.PyRuntime.Calls;

namespace PySharp.PyRuntime;

public class PyRuntimeException : Exception
{
    private readonly PyExceptionObject _exception;
    private string? _message;

    public PyRuntimeException(PyExceptionObject exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        _exception = exception;
    }

    public override string Message => _message ??= _exception.ToMessage(PyCallContext.Null);

    public PyExceptionObject PyException => _exception;
}
