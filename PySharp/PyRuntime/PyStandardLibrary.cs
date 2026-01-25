using PySharp.PyModules.Builtins;
using PySharp.PyModules.Operator;
using PySharp.PyModules.Queue;
using PySharp.PyModules.Random;
using PySharp.PyModules.Site;
using PySharp.PyModules.This;
using PySharp.PyModules.Threading;
using PySharp.PyModules.Time;
using PySharp.PyRuntime.Calls;


namespace PySharp.PyRuntime;

internal static class PyStandardLibrary
{
    public static PyModuleObject? TryCreateModule(PyCallContext context, string name)
    {
        return name switch
        {
            "builtins" => new PyBuiltinsModuleObject(),
            "site" => new PySiteModuleObject(),
            "operator" => new PyOperatorModuleObject(),
            "time" => new PyTimeModuleObject(),
            "random" => new PyRandomModuleObject(),
            "this" => new PyThisModuleObject(),
            "threading" => new PyThreadingModuleObject(),
            "queue" => new PyQueueModuleObject(),

            _ => null
        };
    }
}
