using PySharp.Modules.Builtins;
using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;

namespace PySharp.Modules.Site;

public static class PySiteFunctions
{
    public static readonly PyBuiltinFunctionOrMethodObject Exit = PyBuiltinFunctionOrMethodObject.CreateFunction("exit", ExitImpl);
    public static readonly PyBuiltinFunctionOrMethodObject Quit = Exit;

    [PyFunctionArgsDef("code=None")]
    private static PyResult ExitImpl(PyCallContext context, PyArguments arguments)
    {
        int? exitCode = arguments[0] switch
        {
            PyIntObject intObj => intObj.Int32Value,
            PyNoneObject => 0,
            _ => null
        };

        if (!exitCode.HasValue)
            return PyResult.TypeError(null);

        context.Exit(exitCode.Value);
        return PyNoneObject.None;
    }
}
