using PySharp.Modules.Builtins;
using PySharp.Runtime.Calls;

namespace PySharp.Runtime;

// CPython _PyObject_FunctionStr (Objects/object.c): the "<module>.<qualname>()"
// form the call machinery names a callable by when a ** mapping clashes on a
// keyword. A module of None or builtins is dropped, and an object without a
// __qualname__ is reported as str(x).
internal static class PyCallableName
{
    internal static string Get(PyCallContext context, PyObject? callable)
    {
        // the build-class sequence has no callable on the stack; CPython's
        // own kwargs-merge errors there name __build_class__, which the
        // build-class machinery is
        if (callable is null)
            return "__build_class__()";

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
                    ? $"{builtin.SelfType.QualName}.{builtin.Name}"
                    : builtin.Name;
                break;

            case PyTypeObject type:
                qualname = type.QualName;
                module = type.Module;
                break;

            default:
                // CPython resolves __qualname__ and __module__ by attribute
                // lookup, so any callable carrying them is named by them;
                // without a __qualname__ it falls back to str(x)
                var resolved = PyOperators.GetAttr(context, callable, "__qualname__");
                if (!resolved.IsSuccessful)
                {
                    var str = PySpecialMethods.Str(context, callable);
                    return str.IsSuccessful ? str.Value.Value : callable.PyType.QualName;
                }

                qualname = resolved.Value is PyStrObject resolvedName ? resolvedName.Value : null;
                if (qualname is null)
                {
                    var fallback = PySpecialMethods.Str(context, callable);
                    return fallback.IsSuccessful ? fallback.Value.Value : callable.PyType.QualName;
                }

                var moduleResult = PyOperators.GetAttr(context, callable, "__module__");
                module = moduleResult.IsSuccessful ? ModuleName(moduleResult.Value) : null;
                break;
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
