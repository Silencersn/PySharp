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

    [PyExport("getdefaultencoding", nameof(GetDefaultEncodingImpl))]
    public static partial PyBuiltinFunctionOrMethodObject GetDefaultEncoding { get; }

    [PyExport("intern", nameof(InternImpl))]
    public static partial PyBuiltinFunctionOrMethodObject Intern { get; }

    [PyExport("excepthook", nameof(ExcepthookImpl))]
    public static partial PyBuiltinFunctionOrMethodObject Excepthook { get; }

    // The built-in default hook (sys.__excepthook__ in CPython): render the
    // exception the way the interpreter's native reporting does
    [PyFunctionParameters("type", "value", "traceback", "/")]
    private static PyResult ExcepthookImpl(PyCallContext context, PyArguments arguments)
    {
        if (arguments[1] is PyExceptionObject exc)
            PyInterpreter.WriteTopLevelExceptionMessage(context, exc);
        return PyNoneObject.None;
    }

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
    private static PyResult GetDefaultEncodingImpl(PyCallContext context, PyArguments arguments)
    {
        // CPython's default text encoding is UTF-8 (_PySys_InitCore's
        // SET_SYS_FROM_STRING "utf-8")
        return PyStrObject.FromString("utf-8");
    }

    [PyFunctionParameters("s", "/")]
    private static PyResult InternImpl(PyCallContext context, PyArguments arguments)
    {
        // sysmodule.c sys_intern: an exact str is required — a non-str is
        // rejected with its type name, a str subclass with "can't intern"
        var argument = arguments[0];
        if (argument is not PyStrObject str)
            return PyResult.TypeError(PySR.Runtime_Sys_InternMustBeStr, argument.PyType.Name);
        if (str.PyType != PyStrObjectType.Shared)
            return PyResult.TypeError(PySR.Runtime_Sys_InternCantIntern, str.PyType.Name);
        return context.PyEnvironment.InternPool.Intern(str);
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
            return PyResult.OverflowError(PySR.Runtime_Number_Int_TooLargeForCInt);

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
