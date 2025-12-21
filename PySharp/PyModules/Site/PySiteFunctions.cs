using PySharp.PyModules.Builtins;
using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;
using PySharp.PyRuntime.PyAttributes;

namespace PySharp.PyModules.Site;

public static class PySiteFunctions
{
    public static readonly PyBuiltinFunctionOrMethodObject2 Exit = new("exit", ExitImpl);
    public static readonly PyBuiltinFunctionOrMethodObject2 Quit = Exit;

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

        PyVirtualMachine.Exit(exitCode.Value);
        return PyNoneObject.None;
    }
}
