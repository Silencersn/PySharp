using PySharp.Modules.Builtins;
using PySharp.Runtime.Calls;

namespace PySharp.Runtime;

// CPython _PyObject_FunctionStr (Objects/object.c): the "<module>.<qualname>()"
// form the call machinery names a callable by when a ** mapping clashes on a
// keyword. A module of None or builtins is dropped, and an object without a
// __qualname__ is reported as str(x).
internal static class PyCallableName
{
    internal static string Get(PyCallContext context, PyObject callable)
    {
        string? qualname;
        string? module = null;

        switch (callable)
        {
            case PyMethodObject method:
                return Get(context, method._functionObj);

            case PyFunctionObject function:
                qualname = function.QualName;
                module = ModuleName(function._pyModule);
                break;

            case PyBuiltinFunctionOrMethodObject builtin:
                qualname = builtin.IsMethod && builtin.SelfType is not null
                    ? $"{builtin.SelfType.FullName}.{builtin.Name}"
                    : builtin.Name;
                break;

            case PyTypeObject type:
                qualname = type.QualName;
                module = type.Module;
                break;

            default:
                // no __qualname__ anywhere: CPython falls back to str(x)
                var str = PySpecialMethods.Str(context, callable);
                return str.IsSuccessful ? str.Value.Value : callable.PyType.FullName;
        }

        if (module is null or "builtins")
            return qualname + "()";

        return $"{module}.{qualname}()";
    }

    private static string? ModuleName(PyObject? module)
    {
        return module switch
        {
            null or PyNoneObject => null,
            PyStrObject str => str.Value,
            _ => "<unknown>",
        };
    }
}
