using PySharp.Modules.Builtins;
using PySharp.Runtime.Calls;

namespace PySharp.Runtime;

internal static class PyNumber
{
    public static PyResult<PyIntObject> Int(PyCallContext context, PyObject obj)
    {
        var toInt = obj.PyType.Slots.Int;
        if (toInt is null)
            return PyResult.TypeError(PySR.Runtime_Number_Int_WrongArg, obj.PyType.FullName);

        var result = toInt(context, obj);
        if (result.IsError)
            return result.ExceptionResult;

        if (result.Value is not PyIntObject intObj)
            return PyResult.TypeError(PySR.Runtime_Object_SpecialMethodReturnsWrongType,
                PySpecialNames.Int, "int", result.Value.PyType.FullName);

        return intObj;
    }
}
