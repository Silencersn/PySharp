using PySharp.Modules.Builtins;

namespace PySharp.Runtime.Calls.Extensions;

internal static class PyResultExtensions
{
    public static PyObject PyUnwrap(this PyResult result, PyCallContext context)
    {
        if (result.IsError)
            throw new PyRuntimeException(context, result.Exception);

        return result.Value;
    }

    public static TObject PyUnwrap<TObject>(this PyResult<TObject> result, PyCallContext context) where TObject : PyObject
    {
        if (result.IsError)
            throw new PyRuntimeException(context, result.Exception);

        return result.Value;
    }
}
