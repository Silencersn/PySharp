using PySharp.PyObjects.Builtins;
using PySharp.PyObjects.Operator;
using PySharp.PyObjects.Random;
using PySharp.PyObjects.Site;
using PySharp.PyObjects.This;
using PySharp.PyObjects.Time;


namespace PySharp.PyRuntime;

internal static class PyStandardLibrary
{
    public static PyModuleObject Builtins => new PyBuiltinsModuleObject();
    public static PyModuleObject Operator => new PyOperatorModuleObject();
    public static PyModuleObject Site => new PySiteModuleObject();
    public static PyModuleObject Time => new PyTimeModuleObject();
    public static PyModuleObject Random => new PyRandomModuleObject();
    public static PyModuleObject This => Execute("this", PyThisModuleObject.Code);

    public static PyModuleObject? TryCreateModule(string name)
    {
        return name switch
        {
            "builtins" => Builtins,
            "site" => Site,
            "operator" => Operator,
            "time" => Time,
            "random" => Random,
            "this" => This,

            _ => null
        };
    }

    private static PyModuleObject Execute(string name, string code)
    {
        return PyInterpreter.RunCode(code, name, PyVirtualMachine.PyEnvironmentAsyncLocal.Value);
    }
}
