using PySharp.PyModules.Builtins;

namespace PySharp.PyRuntime;

public class PyRuntimeException : Exception
{
    private readonly PyExceptionObject _exception;

    public PyRuntimeException(PyExceptionObject exception) : base(exception.ToMessage())
    {
        ArgumentNullException.ThrowIfNull(exception);

        _exception = exception;
    }

    public PyExceptionObject PyException => _exception;
}
