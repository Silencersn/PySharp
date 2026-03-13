using PySharp.Compilation.CodeAnalysis;
using PySharp.Modules.Builtins;
using PySharp.Runtime.Calls;

namespace PySharp.Runtime;

public class PyRuntimeException : Exception
{
    private readonly PyExceptionObject _exception;
    private string? _message;
    private readonly ICodeMetaInfoProvider? _compiler;

    public PyRuntimeException(PyCallContext context, PyExceptionObject exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        _exception = exception.WithTraceback(context);
    }

    internal PyRuntimeException(PyCallContext context, PyExceptionObject exception, ICodeMetaInfoProvider? compiler = null)
    {
        _exception = exception.WithTraceback(context, compiler: compiler);
        _compiler = compiler;
    }

    public PyRuntimeException(PyExceptionObject exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        _exception = exception;
    }

    public override string Message => _message ??= _exception.ToMessage(PyCallContext.CSharpRuntime);

    public PyExceptionObject PyException => _exception;

    internal ICodeMetaInfoProvider? Compiler => _compiler;
}
