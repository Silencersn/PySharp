using PySharp.Modules.Builtins;
using PySharp.Modules.Dataclasses;
using PySharp.Modules.Mathematics;
using PySharp.Modules.Operator;
using PySharp.Modules.Queue;
using PySharp.Modules.Random;
using PySharp.Modules.Site;
using PySharp.Modules.Sys;
using PySharp.Modules.This;
using PySharp.Modules.Threading;
using PySharp.Modules.Time;
using PySharp.Modules.Typing;
using PySharp.Modules.Warnings;
using PySharp.Runtime.Calls;


namespace PySharp.Runtime;

internal static class PyStandardLibrary
{
    public static PyModuleObject? TryCreateModule(PyCallContext context, string name)
    {
        PyModuleObject? module = name switch
        {
            "builtins" => new PyBuiltinsModuleObject(),
            "site" => new PySiteModuleObject(),
            "operator" => new PyOperatorModuleObject(),
            "math" => new PyMathModuleObject(),
            "time" => new PyTimeModuleObject(),
            "random" => new PyRandomModuleObject(),
            "this" => new PyThisModuleObject(),
            "dataclasses" => new PyDataclassesModuleObject(),
            "threading" => new PyThreadingModuleObject(),
            "queue" => new PyQueueModuleObject(),
            "typing" => new PyTypingModuleObject(),
            "sys" => new PySysModuleObject(),
            "warnings" => new PyWarningsModuleObject(),

            _ => null
        };

        // C#-implemented stdlib modules are compiled into the interpreter
        // itself — the analog of CPython's statically linked built-ins.
        // Embedded-source modules already carry their "frozen" origin.
        if (module is not null && module.Origin is null)
            module.Origin = "built-in";

        return module;
    }
}
