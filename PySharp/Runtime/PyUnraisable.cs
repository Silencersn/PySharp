using PySharp.Modules.Builtins;
using PySharp.Runtime.Calls;

namespace PySharp.Runtime;

// CPython PyErr_FormatUnraisable/PyErr_WriteUnraisable: an exception that no
// caller can handle is written to stderr — the message, the object's repr and
// the traceback, matching the default sys.unraisablehook — and then dropped
// instead of propagating. sys.unraisablehook itself is not exposed yet, so
// the default hook is the only sink.
internal static class PyUnraisable
{
    public static void Write(PyCallContext context, string message, PyObject? target, PyExceptionObject exception)
    {
        if (target is null)
            context.Error.WriteLine($"{message}:");
        else
            context.Error.WriteLine($"{message} {SafeRepr(context, target)}:");

        context.Error.Write(exception.WithTraceback(context, overwriteExisting: false).ToUnraisableMessage(context));
    }

    // CPython falls back to "<object repr() failed>" when the repr cannot be
    // produced, which also keeps the unraisable report from raising itself.
    private static string SafeRepr(PyCallContext context, PyObject target)
    {
        var repr = PySpecialMethods.Repr(context, target);
        return repr.Value is PyStrObject text ? text.Value : "<object repr() failed>";
    }
}
