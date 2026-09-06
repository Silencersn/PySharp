using PySharp.Modules.Builtins;
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
        if (arguments[0] is not PyIntObject maxdigits)
            return PyResult.TypeError(PySR.Runtime_Number_Int_CannotInterpretedAsInt, arguments[0].PyType.FullName);

        if (!maxdigits.IsInt32)
            return PyResult.OverflowError(PySR.Runtime_Number_Int_MaxDigitsNotInt32);

        if (!PyIntStrDigitsLimit.TrySetMaxStrDigits(maxdigits.Int32Value))
            return PyResult.ValueError(PySR.Runtime_Number_Int_MaxDigitsInvalid, PyIntStrDigitsLimit.MinMaxStrDigits);

        return PyNoneObject.None;
    }
}
