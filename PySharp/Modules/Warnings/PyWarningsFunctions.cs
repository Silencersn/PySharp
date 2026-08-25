using PySharp.Modules.Builtins;
using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;

namespace PySharp.Modules.Warnings;

public static partial class PyWarningsFunctions
{
    [PyExport("warn", nameof(WarnImpl))]
    public static partial PyBuiltinFunctionOrMethodObject Warn { get; }

    // Emits a warning through the core warning machinery. The message is any object (its str()
    // is used), the category defaults to UserWarning when None, and stacklevel attributes the
    // warning to the caller's frame (1 = the line that invoked warnings.warn).
    [PyFunctionParameters("message", "category=None", "stacklevel=1")]
    private static PyResult WarnImpl(PyCallContext context, PyArguments arguments)
    {
        var message = arguments[0];
        var categoryObj = arguments[1];

        PyTypeObject<PyExceptionObject>? category = null;
        if (categoryObj is not PyNoneObject)
        {
            category = categoryObj as PyTypeObject<PyExceptionObject>;
            if (category is null)
                return PyResult.TypeError($"category must be a Warning subclass, not '{categoryObj.PyType.Name}'");
        }

        if (arguments[2] is not PyIntObject stacklevelObj)
            return PyResult.TypeError($"'{arguments[2].PyType.Name}' object cannot be interpreted as an integer");

        return context.Warn(message, category, stacklevelObj.Int32Value);
    }
}
