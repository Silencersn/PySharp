using PySharp.Modules.Builtins;
using PySharp.Modules.Mathematics;
using PySharp.Modules.Operator;
using PySharp.Modules.Queue;
using PySharp.Modules.Random;
using PySharp.Modules.Site;
using PySharp.Modules.This;
using PySharp.Modules.Threading;
using PySharp.Modules.Time;
using PySharp.Runtime.Calls;


namespace PySharp.Runtime;

internal static class PyStandardLibrary
{
    public static PyModuleObject? TryCreateModule(PyCallContext context, string name)
    {
        return name switch
        {
            "builtins" => new PyBuiltinsModuleObject(),
            "site" => new PySiteModuleObject(),
            "operator" => new PyOperatorModuleObject(),
            "math" => new PyMathModuleObject(),
            "time" => new PyTimeModuleObject(),
            "random" => new PyRandomModuleObject(),
            "this" => new PyThisModuleObject(),
            "threading" => new PyThreadingModuleObject(),
            "queue" => new PyQueueModuleObject(),

            _ => null
        };
    }
}
