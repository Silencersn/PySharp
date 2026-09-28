using PySharp.Modules.Builtins;
using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;
using PySharp.Utility;

namespace PySharp.Modules.Sys;

internal static partial class PySysFunctions
{
    [PyExport("get_int_max_str_digits", nameof(GetIntMaxStrDigitsImpl))]
    public static partial PyBuiltinFunctionOrMethodObject GetIntMaxStrDigits { get; }

    [PyExport("set_int_max_str_digits", nameof(SetIntMaxStrDigitsImpl))]
    public static partial PyBuiltinFunctionOrMethodObject SetIntMaxStrDigits { get; }

    [PyFunctionParameters()]
    private static PyResult GetIntMaxStrDigitsImpl(PyCallContext context, PyArguments arguments)
    {
        return PyIntObject.FromInteger(PyIntStrDigitsLimit.MaxStrDigits);
    }

    [PyFunctionParameters("maxdigits", "/")]
    private static PyResult SetIntMaxStrDigitsImpl(PyCallContext context, PyArguments arguments)
    {
        // CPython's "i" argument converter resolves __index__ before the C long conversion.
        var maxDigitsResult = PySpecialMethods.Index(context, arguments[0]);
        if (maxDigitsResult.IsError)
            return maxDigitsResult;
        var maxdigits = maxDigitsResult.Value;

        if (!maxdigits.IsInt32)
            return PyResult.OverflowError(PySR.Runtime_Number_Int_MaxDigitsNotInt32);

        if (!PyIntStrDigitsLimit.TrySetMaxStrDigits(maxdigits.Int32Value))
            return PyResult.ValueError(PySR.Runtime_Number_Int_MaxDigitsInvalid, PyIntStrDigitsLimit.MinMaxStrDigits);

        return PyNoneObject.None;
    }
}
