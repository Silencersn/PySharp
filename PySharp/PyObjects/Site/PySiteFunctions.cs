using PySharp.PyObjects.Builtins;
using PySharp.PyRuntime;
using PySharp.PyRuntime.PyAttributes;

namespace PySharp.PyObjects.Site;

public static class PySiteFunctions
{
    public static readonly PyBuiltinFunctionOrMethodObject Exit = new("exit", ExitImpl);
    public static readonly PyBuiltinFunctionOrMethodObject Quit = Exit;

    [PyFunctionArgsDef("code=None")]
    private static PyObject? ExitImpl(PyArguments arguments)
    {
        int? exitCode = arguments.Args[0] switch
        {
            PyIntObject intObj => intObj.Int32Value,
            PyNoneObject => 0,
            _ => null
        };

        if (!exitCode.HasValue)
            return PyVirtualMachine.RaiseTypeError(null);

        PyVirtualMachine.Exit(exitCode.Value);
        return PyNoneObject.None;
    }
}
