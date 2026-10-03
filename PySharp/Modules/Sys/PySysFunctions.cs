using PySharp.Modules.Builtins;
using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;
using PySharp.Utility;

namespace PySharp.Modules.Sys;

internal static partial class PySysFunctions
{
    [PyExport("exit", nameof(ExitImpl))]
    public static partial PyBuiltinFunctionOrMethodObject Exit { get; }

    [PyExport("getrecursionlimit", nameof(GetRecursionLimitImpl))]
    public static partial PyBuiltinFunctionOrMethodObject GetRecursionLimit { get; }

    [PyExport("setrecursionlimit", nameof(SetRecursionLimitImpl))]
    public static partial PyBuiltinFunctionOrMethodObject SetRecursionLimit { get; }

    [PyExport("get_int_max_str_digits", nameof(GetIntMaxStrDigitsImpl))]
    public static partial PyBuiltinFunctionOrMethodObject GetIntMaxStrDigits { get; }

    [PyExport("set_int_max_str_digits", nameof(SetIntMaxStrDigitsImpl))]
    public static partial PyBuiltinFunctionOrMethodObject SetIntMaxStrDigits { get; }

    [PyFunctionParameters("code=None")]
    private static PyResult ExitImpl(PyCallContext context, PyArguments arguments)
    {
        context.ExitWith(arguments[0]);
        return PyNoneObject.None;
    }

    [PyFunctionParameters()]
    private static PyResult GetRecursionLimitImpl(PyCallContext context, PyArguments arguments)
    {
        return PyIntObject.FromInteger(context.PyEnvironment.RecursionLimit);
    }

    [PyFunctionParameters("limit", "/")]
    private static PyResult SetRecursionLimitImpl(PyCallContext context, PyArguments arguments)
    {
        // CPython's "i" argument converter resolves __index__ before the C int conversion.
        var limitResult = PySpecialMethods.Index(context, arguments[0]);
        if (limitResult.IsError)
            return limitResult;
        var limit = limitResult.Value;

        if (!limit.IsInt32)
            return PyResult.OverflowError(PySR.Runtime_Number_Int_MaxDigitsNotInt32);

        // sysmodule.c setrecursionlimit: a limit below 1 is a ValueError,
        // and a limit the current depth already reaches would break the
        // running stack, so it is rejected as a RecursionError
        var newLimit = limit.Int32Value;
        if (newLimit < 1)
            return PyResult.ValueError(PySR.Runtime_Sys_RecursionLimitTooLow);

        var depth = context.FrameState.CurrentFrameCount;
        if (depth >= newLimit)
            return PyResult.RecursionError(PySR.Runtime_Sys_RecursionLimitTooLowAtDepth, newLimit, depth);

        context.PyEnvironment.RecursionLimit = newLimit;
        return PyNoneObject.None;
    }

    [PyFunctionParameters()]
    private static PyResult GetIntMaxStrDigitsImpl(PyCallContext context, PyArguments arguments)
    {
        return PyIntObject.FromInteger(context.PyEnvironment.IntStrDigits.MaxStrDigits);
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

        if (!context.PyEnvironment.IntStrDigits.TrySetMaxStrDigits(maxdigits.Int32Value))
            return PyResult.ValueError(PySR.Runtime_Number_Int_MaxDigitsInvalid, PyIntStrDigitsLimit.MinMaxStrDigits);

        return PyNoneObject.None;
    }
}
