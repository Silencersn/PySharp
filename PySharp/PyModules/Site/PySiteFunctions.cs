using PySharp.PyModules.Builtins;
using PySharp.PyRuntime.Calls;
using PySharp.PyRuntime.PyAttributes;

namespace PySharp.PyModules.Site;

public static class PySiteFunctions
{
    public static readonly PyBuiltinFunctionOrMethodObject Exit = new("exit", ExitImpl);
    public static readonly PyBuiltinFunctionOrMethodObject Quit = Exit;

    [PyFunctionArgsDef("code=None")]
    private static PyResult ExitImpl(PyCallContext context, PyArguments arguments)
    {
        int? exitCode = arguments.Args[0] switch
        {
            PyIntObject intObj => intObj.Int32Value,
            PyNoneObject => 0,
            _ => null
        };

        if (!exitCode.HasValue)
            return PyResult.RaiseTypeError(null);

        context.Exit(exitCode.Value);
        return PyNoneObject.None;
    }
}
