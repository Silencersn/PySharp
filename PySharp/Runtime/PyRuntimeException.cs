using PySharp.Compilation.CodeAnalysis;
using PySharp.Modules.Builtins;
using PySharp.Runtime.Calls;
using System.Runtime.CompilerServices;

namespace PySharp.Runtime;

public class PyRuntimeException : Exception
{
    private readonly PyExceptionObject _exception;
    private string? _message;
    private readonly ICodeMetaInfoProvider? _compiler;

    public PyRuntimeException(PyCallContext context, PyExceptionObject exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        context.ChainHandledContext(exception);
        _exception = exception.WithTraceback(context);
    }

    internal PyRuntimeException(PyCallContext context, PyExceptionObject exception, ICodeMetaInfoProvider? compiler = null)
    {
        context.ChainHandledContext(exception);
        _exception = exception.WithTraceback(context);
        _compiler = compiler;
    }

    public PyRuntimeException(PyExceptionObject exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        _exception = exception;
    }

    // Exception.Message is a fixed-signature .NET face; mid-run the ambient
    // context renders the message, after the run the CSharpRuntime sentinel
    // keeps the context-free fallback
    public override string Message => _message ??= _exception.ToMessage(PyCallContext.Current ?? PyCallContext.CSharpRuntime);

    public PyExceptionObject PyException => _exception;

    internal ICodeMetaInfoProvider? Compiler => _compiler;
}

internal class PySharpNotSupportedException : NotSupportedException
{
    public PySharpNotSupportedException()
    {
    }

    public PySharpNotSupportedException(string? message) : base(message)
    {
    }

    public static PySharpNotSupportedException ContextNeeded([CallerMemberName] string? memberName = null)
    {
        return new PySharpNotSupportedException($"Context is needed for {memberName}");
    }
}